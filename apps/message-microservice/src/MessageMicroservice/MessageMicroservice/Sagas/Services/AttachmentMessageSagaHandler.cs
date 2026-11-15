
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Messaging;
using SharedModels.Events;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas.Services;

// Handles message creation and compensation for the file upload Saga.
public sealed class AttachmentMessageSagaHandler
{
    private readonly IMessageRepository _repository;
    private readonly IMessageClient _messageClient;

    // Tracks messages created by each Saga in this lab.
    private readonly Dictionary<Guid, MessageProgress> _messagesBySaga = new();

    // Prevents a compensated Saga from recreating its message.
    private readonly HashSet<Guid> _retractedSagas = new();

    // Serializes operations within this handler instance.
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AttachmentMessageSagaHandler(
        IMessageRepository repository,
        IMessageClient messageClient)
    {
        _repository = repository;
        _messageClient = messageClient;
    }

    // Handles the command to create an attachment message.
    public async Task HandleCreateAsync(
        CreateAttachmentMessage command)
    {
        await _lock.WaitAsync();

        try
        {
            // Validate the incoming command.
            if (command.SagaId == Guid.Empty ||
                command.UserId == Guid.Empty ||
                command.ChannelId == Guid.Empty ||
                command.FileId == Guid.Empty)
            {
                await PublishCreationFailureAsync(
                    command.SagaId,
                    "Invalid attachment message request.");

                return;
            }

            // Do not recreate a message after compensation.
            if (_retractedSagas.Contains(command.SagaId))
            {
                await PublishCreationFailureAsync(
                    command.SagaId,
                    "The Saga message has already been retracted.");

                return;
            }

            // Check whether this Saga already created a message.
            if (!_messagesBySaga.TryGetValue(
                command.SagaId,
                out var progress))
            {
                try
                {
                    // The current Message entity has no attachment field.
                    // Store a file reference in the content for this lab.
                    string content = string.IsNullOrWhiteSpace(
                        command.Content)
                        ? $"[Attachment: {command.FileId}]"
                        : $"{command.Content}\n[Attachment: {command.FileId}]";

                    // Create a message using the existing domain model.
                    var message = Message.Create(
                        command.ChannelId,
                        command.UserId,
                        content
                    );

                    // Save the message in the existing repository.
                    _repository.Add(message);

                    // Remember the message to prevent duplicate creation.
                    progress = new MessageProgress(message.Id);

                    _messagesBySaga.Add(command.SagaId, progress);

                    Console.WriteLine(
                        $"Saga message created: {message.Id}");
                }
                catch (Exception ex)
                {
                    await PublishCreationFailureAsync(
                        command.SagaId,
                        ex.Message);

                    return;
                }
            }
            else
            {
                Console.WriteLine(
                    $"Saga message already exists: {progress.MessageId}");
            }

            var savedMessage = _repository.GetById(
                progress.MessageId);

            if (savedMessage is null)
            {
                await PublishCreationFailureAsync(
                    command.SagaId,
                    "The previously created message was not found.");

                return;
            }

            // Publish the existing message event only when needed.
            if (!progress.PostedEventPublished)
            {
                await _messageClient.PublishAsync(
                    new MessagePostedEvent
                    {
                        MessageId = savedMessage.Id,
                        ChannelId = savedMessage.ChannelId,
                        AuthorId = savedMessage.SenderId,
                        Content = savedMessage.Content,
                        PostedAt = savedMessage.CreatedAt
                    });

                progress.PostedEventPublished = true;
            }

            // Confirm message creation to the Saga Orchestrator.
            await _messageClient.PublishAsync(
                new AttachmentMessageCreated
                {
                    SagaId = command.SagaId,
                    MessageId = savedMessage.Id
                });
        }
        finally
        {
            _lock.Release();
        }
    }

    // Handles the compensating command to retract a message.
    public async Task HandleRetractAsync(
        RetractAttachmentMessage command)
    {
        await _lock.WaitAsync();

        try
        {
            if (command.SagaId == Guid.Empty ||
                command.MessageId == Guid.Empty)
            {
                await PublishRetractionFailureAsync(
                    command.SagaId,
                    "Invalid message retraction request.");

                return;
            }

            // Repeated retraction requests are safe.
            if (_retractedSagas.Contains(command.SagaId))
            {
                await PublishRetractionSuccessAsync(
                    command.SagaId);

                return;
            }

            // Verify that the message belongs to this Saga.
            if (!_messagesBySaga.TryGetValue(
                    command.SagaId,
                    out var progress) ||
                progress.MessageId != command.MessageId)
            {
                await PublishRetractionFailureAsync(
                    command.SagaId,
                    "The message does not belong to this Saga.");

                return;
            }

            try
            {
                var message = _repository.GetById(
                    command.MessageId);

                if (message is not null)
                {
                    // Compensation: remove the created message.
                    _repository.Delete(message);

                    Console.WriteLine(
                        $"COMPENSATION: Message retracted: {command.MessageId}");
                }

                // Remember that this Saga has been compensated.
                _retractedSagas.Add(command.SagaId);
            }
            catch (Exception ex)
            {
                await PublishRetractionFailureAsync(
                    command.SagaId,
                    ex.Message);

                return;
            }

            // Confirm successful compensation.
            await PublishRetractionSuccessAsync(
                command.SagaId);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Publishes a failed creation event.
    private Task PublishCreationFailureAsync(
        Guid sagaId,
        string reason)
    {
        Console.WriteLine(
            $"Attachment message creation failed: {reason}");

        return _messageClient.PublishAsync(
            new AttachmentMessageFailed
            {
                SagaId = sagaId,
                Reason = reason
            });
    }

    // Publishes a successful compensation event.
    private Task PublishRetractionSuccessAsync(Guid sagaId)
    {
        return _messageClient.PublishAsync(
            new AttachmentMessageRetracted
            {
                SagaId = sagaId
            });
    }

    // Publishes a failed compensation event.
    private Task PublishRetractionFailureAsync(
        Guid sagaId,
        string reason)
    {
        Console.WriteLine(
            $"Message retraction failed: {reason}");

        return _messageClient.PublishAsync(
            new AttachmentRetractionFailed
            {
                SagaId = sagaId,
                Reason = reason
            });
    }

    // Tracks message creation and event publication for one Saga.
    private sealed class MessageProgress
    {
        public Guid MessageId { get; }

        public bool PostedEventPublished { get; set; }

        public MessageProgress(Guid messageId)
        {
            MessageId = messageId;
        }
    }
}
