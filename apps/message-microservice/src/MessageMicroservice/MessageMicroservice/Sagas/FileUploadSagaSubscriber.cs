
using MessageMicroservice.Messaging;
using MessageMicroservice.Sagas.Services;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas;

// Connects Saga commands and events to RabbitMQ subscribers.
public sealed class FileUploadSagaSubscriber : BackgroundService
{
    private readonly IMessageClient _messageClient;
    private readonly FileUploadSagaOrchestrator _orchestrator;
    private readonly FileStorageSagaHandler _fileHandler;
    private readonly StorageQuotaSagaHandler _quotaHandler;
    private readonly AttachmentMessageSagaHandler _messageHandler;
    private readonly ChannelSagaHandler _channelHandler;
    private readonly ILogger<FileUploadSagaSubscriber> _logger;

    public FileUploadSagaSubscriber(
        IMessageClient messageClient,
        FileUploadSagaOrchestrator orchestrator,
        FileStorageSagaHandler fileHandler,
        StorageQuotaSagaHandler quotaHandler,
        AttachmentMessageSagaHandler messageHandler,
        ChannelSagaHandler channelHandler,
        ILogger<FileUploadSagaSubscriber> logger)
    {
        _messageClient = messageClient;
        _orchestrator = orchestrator;
        _fileHandler = fileHandler;
        _quotaHandler = quotaHandler;
        _messageHandler = messageHandler;
        _channelHandler = channelHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Subscribe the orchestrator to workflow events.
        // Subscribe service handlers to their commands.
        await Task.WhenAll(

            // Start command.
            SubscribeSagaAsync<StartFileUploadSaga>(),

            // File storage events.
            SubscribeSagaAsync<TemporaryFileStored>(),
            SubscribeSagaAsync<TemporaryFileFailed>(),

            // Storage quota events.
            SubscribeSagaAsync<StorageQuotaReserved>(),
            SubscribeSagaAsync<StorageQuotaRejected>(),

            // Message creation events.
            SubscribeSagaAsync<AttachmentMessageCreated>(),
            SubscribeSagaAsync<AttachmentMessageFailed>(),

            // Channel activity events.
            SubscribeSagaAsync<ChannelActivityUpdated>(),
            SubscribeSagaAsync<ChannelActivityFailed>(),

            // Channel compensation events.
            SubscribeSagaAsync<ChannelActivityReverted>(),
            SubscribeSagaAsync<ChannelActivityRevertFailed>(),

            // Message compensation events.
            SubscribeSagaAsync<AttachmentMessageRetracted>(),
            SubscribeSagaAsync<AttachmentRetractionFailed>(),

            // Quota compensation events.
            SubscribeSagaAsync<StorageQuotaReleased>(),
            SubscribeSagaAsync<StorageQuotaReleaseFailed>(),

            // File compensation events.
            SubscribeSagaAsync<TemporaryFileDeleted>(),
            SubscribeSagaAsync<TemporaryFileDeletionFailed>(),

            // File service commands.
            SubscribeHandlerAsync<StoreTemporaryFile>(
                _fileHandler.HandleStoreAsync),

            SubscribeHandlerAsync<DeleteTemporaryFile>(
                _fileHandler.HandleDeleteAsync),

            // Storage service commands.
            SubscribeHandlerAsync<ReserveStorageQuota>(
                _quotaHandler.HandleReserveAsync),

            SubscribeHandlerAsync<ReleaseStorageQuota>(
                _quotaHandler.HandleReleaseAsync),

            // Message service commands.
            SubscribeHandlerAsync<CreateAttachmentMessage>(
                _messageHandler.HandleCreateAsync),

            SubscribeHandlerAsync<RetractAttachmentMessage>(
                _messageHandler.HandleRetractAsync),

            // Channel service commands.
            SubscribeHandlerAsync<UpdateChannelActivity>(
                _channelHandler.HandleUpdateAsync),

            SubscribeHandlerAsync<RevertChannelActivity>(
                _channelHandler.HandleRevertAsync)
        );

        _logger.LogInformation(
            "File upload Saga subscriptions registered successfully.");

        // Keep the background service active until shutdown.
        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "File upload Saga subscriber stopped.");
        }
    }

    // Subscribes the orchestrator to a specific Saga message type.
    private Task SubscribeSagaAsync<T>()
        where T : SagaMessage
    {
        string subscriptionId =
            $"bizcord-w46-saga-{typeof(T).Name}";

        return _messageClient.SubscribeAsync<T>(
            subscriptionId,
            message => _orchestrator.HandleAsync(message)
        );
    }

    // Subscribes a service handler to a specific command type.
    private Task SubscribeHandlerAsync<T>(
        Func<T, Task> handler)
    {
        string subscriptionId =
            $"bizcord-w46-worker-{typeof(T).Name}";

        return _messageClient.SubscribeAsync<T>(
            subscriptionId,
            handler
        );
    }
}
