
using MessageMicroservice.Messaging;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas;

// Coordinates the file upload workflow through commands and events.
public sealed class FileUploadSagaOrchestrator
{
    private readonly IMessageClient _messageClient;
    private readonly IFileUploadSagaStateRepository _stateRepository;

    // Prevents concurrent handling within one orchestrator instance.
    private readonly SemaphoreSlim _handlerLock = new(1, 1);

    public FileUploadSagaOrchestrator(
        IMessageClient messageClient,
        IFileUploadSagaStateRepository stateRepository)
    {
        _messageClient = messageClient;
        _stateRepository = stateRepository;
    }

    // Entry point for all Saga events and the start command.
    public async Task HandleAsync(SagaMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.SagaId == Guid.Empty)
        {
            throw new ArgumentException("SagaId is required.");
        }

        await _handlerLock.WaitAsync();

        try
        {
            switch (message)
            {
                // Start the workflow.
                case StartFileUploadSaga m:
                    await StartAsync(m);
                    break;

                // File storage results.
                case TemporaryFileStored m:
                    await ForwardSucceededAsync(
                        m.SagaId,
                        "StoringFile",
                        s => s.FileStored = true,
                        s => PublishStepAsync(
                            s,
                            "ReservingQuota",
                            new ReserveStorageQuota
                            {
                                SagaId = s.SagaId,
                                UserId = s.UserId,
                                FileId = s.FileId,
                                FileSizeBytes = s.FileSizeBytes
                            }));
                    break;

                case TemporaryFileFailed m:
                    await FileStorageFailedAsync(m);
                    break;

                // Storage quota results.
                case StorageQuotaReserved m:
                    await ForwardSucceededAsync(
                        m.SagaId,
                        "ReservingQuota",
                        s => s.QuotaReserved = true,
                        s => PublishStepAsync(
                            s,
                            "CreatingMessage",
                            new CreateAttachmentMessage
                            {
                                SagaId = s.SagaId,
                                UserId = s.UserId,
                                ChannelId = s.ChannelId,
                                FileId = s.FileId,
                                Content = s.Content
                            }));
                    break;

                case StorageQuotaRejected m:
                    await ForwardFailedAsync(
                        m.SagaId,
                        "ReservingQuota",
                        m.Reason);
                    break;

                // Message creation results.
                case AttachmentMessageCreated m:
                    await ForwardSucceededAsync(
                        m.SagaId,
                        "CreatingMessage",
                        s =>
                        {
                            s.MessageCreated = true;
                            s.MessageId = m.MessageId;
                            s.ChannelUpdateStarted = true;
                        },
                        s => PublishStepAsync(
                            s,
                            "UpdatingChannel",
                            new UpdateChannelActivity
                            {
                                SagaId = s.SagaId,
                                ChannelId = s.ChannelId,
                                MessageId = m.MessageId
                            }));
                    break;

                case AttachmentMessageFailed m:
                    await ForwardFailedAsync(
                        m.SagaId,
                        "CreatingMessage",
                        m.Reason);
                    break;

                // Channel activity results.
                case ChannelActivityUpdated m:
                    await ForwardSucceededAsync(
                        m.SagaId,
                        "UpdatingChannel",
                        s =>
                        {
                            s.ChannelUpdated = true;
                            s.Status = FileUploadSagaStatus.Completed;
                            s.CurrentStep = "Completed";
                            s.CompletedAtUtc = DateTime.UtcNow;
                        },
                        s => _stateRepository.SaveAsync(s));
                    break;

                case ChannelActivityFailed m:
                    await ForwardFailedAsync(
                        m.SagaId,
                        "UpdatingChannel",
                        m.Reason);
                    break;

                // Channel compensation results.
                case ChannelActivityReverted m:
                    await CompensationSucceededAsync(
                        m.SagaId,
                        "RevertingChannel",
                        s => s.ChannelReverted = true);
                    break;

                case ChannelActivityRevertFailed m:
                    await CompensationFailedAsync(
                        m.SagaId,
                        "RevertingChannel",
                        m.Reason);
                    break;

                // Message compensation results.
                case AttachmentMessageRetracted m:
                    await CompensationSucceededAsync(
                        m.SagaId,
                        "RetractingMessage",
                        s => s.MessageRetracted = true);
                    break;

                case AttachmentRetractionFailed m:
                    await CompensationFailedAsync(
                        m.SagaId,
                        "RetractingMessage",
                        m.Reason);
                    break;

                // Quota compensation results.
                case StorageQuotaReleased m:
                    await CompensationSucceededAsync(
                        m.SagaId,
                        "ReleasingQuota",
                        s => s.QuotaReleased = true);
                    break;

                case StorageQuotaReleaseFailed m:
                    await CompensationFailedAsync(
                        m.SagaId,
                        "ReleasingQuota",
                        m.Reason);
                    break;

                // File compensation results.
                case TemporaryFileDeleted m:
                    await CompensationSucceededAsync(
                        m.SagaId,
                        "DeletingFile",
                        s => s.FileDeleted = true);
                    break;

                case TemporaryFileDeletionFailed m:
                    await CompensationFailedAsync(
                        m.SagaId,
                        "DeletingFile",
                        m.Reason);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported Saga message: {message.GetType().Name}");
            }
        }
        finally
        {
            _handlerLock.Release();
        }
    }

    // Creates a new Saga instance and starts temporary file storage.
    private async Task StartAsync(StartFileUploadSaga message)
    {
        if (message.UserId == Guid.Empty ||
            message.ChannelId == Guid.Empty ||
            message.FileId == Guid.Empty ||
            message.FileSizeBytes <= 0 ||
            string.IsNullOrWhiteSpace(message.FileName))
        {
            throw new ArgumentException("Invalid file upload request.");
        }

        var existing = await _stateRepository.GetByIdAsync(
            message.SagaId);

        // Ignore duplicate start messages.
        if (existing is not null)
            return;

        var state = new FileUploadSagaState
        {
            SagaId = message.SagaId,
            UserId = message.UserId,
            ChannelId = message.ChannelId,
            FileId = message.FileId,
            FileName = message.FileName,
            FileSizeBytes = message.FileSizeBytes,
            Content = message.Content,
            Status = FileUploadSagaStatus.Running
        };

        await PublishStepAsync(
            state,
            "StoringFile",
            new StoreTemporaryFile
            {
                SagaId = state.SagaId,
                FileId = state.FileId,
                FileName = state.FileName,
                FileSizeBytes = state.FileSizeBytes
            });
    }

    // Processes a successful forward transaction.
    private async Task ForwardSucceededAsync(
        Guid sagaId,
        string expectedStep,
        Action<FileUploadSagaState> updateState,
        Func<FileUploadSagaState, Task> nextAction)
    {
        var state = await GetExpectedStateAsync(
            sagaId,
            expectedStep,
            FileUploadSagaStatus.Running);

        if (state is null)
            return;

        updateState(state);
        await nextAction(state);
    }

    // Starts compensation after a forward transaction fails.
    private async Task ForwardFailedAsync(
        Guid sagaId,
        string expectedStep,
        string reason)
    {
        var state = await GetExpectedStateAsync(
            sagaId,
            expectedStep,
            FileUploadSagaStatus.Running);

        if (state is null)
            return;

        state.LastError = reason;
        state.Status = FileUploadSagaStatus.Compensating;

        await ContinueCompensationAsync(state);
    }

    // Cleans up temporary files even when storage failed.
    private async Task FileStorageFailedAsync(
        TemporaryFileFailed message)
    {
        var state = await GetExpectedStateAsync(
            message.SagaId,
            "StoringFile",
            FileUploadSagaStatus.Running);

        if (state is null)
            return;

        state.LastError = message.Reason;
        state.Status = FileUploadSagaStatus.Compensating;

        await PublishStepAsync(
            state,
            "DeletingFile",
            new DeleteTemporaryFile
            {
                SagaId = state.SagaId,
                FileId = state.FileId
            });
    }

    // Records successful compensation and continues the rollback flow.
    private async Task CompensationSucceededAsync(
        Guid sagaId,
        string expectedStep,
        Action<FileUploadSagaState> updateState)
    {
        var state = await GetExpectedStateAsync(
            sagaId,
            expectedStep,
            FileUploadSagaStatus.Compensating);

        if (state is null)
            return;

        updateState(state);
        await ContinueCompensationAsync(state);
    }

    // Records a failed compensation for later retry.
    private async Task CompensationFailedAsync(
        Guid sagaId,
        string expectedStep,
        string reason)
    {
        var state = await GetExpectedStateAsync(
            sagaId,
            expectedStep,
            FileUploadSagaStatus.Compensating);

        if (state is null)
            return;

        state.Status = FileUploadSagaStatus.CompensationFailed;
        state.LastError = $"Compensation failed: {reason}";

        await _stateRepository.SaveAsync(state);
    }

    // Executes compensations in reverse business order.
    private async Task ContinueCompensationAsync(
        FileUploadSagaState state)
    {
        state.Status = FileUploadSagaStatus.Compensating;

        // Revert channel activity if the update may have started.
        if (state.ChannelUpdateStarted && !state.ChannelReverted)
        {
            if (state.MessageId is null)
            {
                throw new InvalidOperationException(
                    "MessageId is required for channel compensation.");
            }

            await PublishStepAsync(
                state,
                "RevertingChannel",
                new RevertChannelActivity
                {
                    SagaId = state.SagaId,
                    ChannelId = state.ChannelId,
                    MessageId = state.MessageId.Value
                });

            return;
        }

        // Retract the previously created message.
        if (state.MessageCreated && !state.MessageRetracted)
        {
            if (state.MessageId is null)
            {
                throw new InvalidOperationException(
                    "MessageId is required for message compensation.");
            }

            await PublishStepAsync(
                state,
                "RetractingMessage",
                new RetractAttachmentMessage
                {
                    SagaId = state.SagaId,
                    MessageId = state.MessageId.Value
                });

            return;
        }

        // Restore previously reserved storage quota.
        if (state.QuotaReserved && !state.QuotaReleased)
        {
            await PublishStepAsync(
                state,
                "ReleasingQuota",
                new ReleaseStorageQuota
                {
                    SagaId = state.SagaId,
                    UserId = state.UserId,
                    FileId = state.FileId,
                    FileSizeBytes = state.FileSizeBytes
                });

            return;
        }

        // Delete the uploaded file or clean up a failed upload.
        if ((state.FileStored || state.CurrentStep == "DeletingFile")
            && !state.FileDeleted)
        {
            await PublishStepAsync(
                state,
                "DeletingFile",
                new DeleteTemporaryFile
                {
                    SagaId = state.SagaId,
                    FileId = state.FileId
                });

            return;
        }

        // All required compensations have completed.
        state.Status = FileUploadSagaStatus.Compensated;
        state.CurrentStep = "Compensated";
        state.CompletedAtUtc = DateTime.UtcNow;

        await _stateRepository.SaveAsync(state);
    }

    // Saves the Saga state before publishing the next command.
    private async Task PublishStepAsync<T>(
        FileUploadSagaState state,
        string step,
        T command)
    {
        state.CurrentStep = step;

        await _stateRepository.SaveAsync(state);
        await _messageClient.PublishAsync(command);
    }

    // Loads a Saga only when it is waiting for the expected event.
    private async Task<FileUploadSagaState?> GetExpectedStateAsync(
        Guid sagaId,
        string expectedStep,
        FileUploadSagaStatus expectedStatus)
    {
        var state = await _stateRepository.GetByIdAsync(sagaId);

        if (state is null)
            return null;

        if (state.Status != expectedStatus ||
            state.CurrentStep != expectedStep)
        {
            return null;
        }

        return state;
    }

    // Allows an operator or recovery worker to retry compensation.
    public async Task RetryCompensationAsync(Guid sagaId)
    {
        await _handlerLock.WaitAsync();

        try
        {
            var state = await _stateRepository.GetByIdAsync(sagaId);

            if (state is null ||
                state.Status != FileUploadSagaStatus.CompensationFailed)
            {
                return;
            }

            await ContinueCompensationAsync(state);
        }
        finally
        {
            _handlerLock.Release();
        }
    }
}
