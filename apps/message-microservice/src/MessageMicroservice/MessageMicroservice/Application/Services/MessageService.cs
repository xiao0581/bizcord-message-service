
using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Messaging;
using SharedModels.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageMicroservice.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _repository;
    private readonly IMessageClient _messageClient;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository repository,
        IMessageClient messageClient,
        ILogger<MessageService>? logger = null)
    {
        _repository = repository;
        _messageClient = messageClient;
        _logger = logger ?? NullLogger<MessageService>.Instance;
    }

    public IEnumerable<Message> GetAll()
    {
        _logger.LogInformation("Fetching all messages.");

        return _repository.GetAll();
    }

    public Message? GetById(Guid id)
    {
        _logger.LogInformation(
            "Fetching message {MessageId}.", id);

        return _repository.GetById(id);
    }

    public async Task<Message> CreateAsync(Message message)
    {
        // Create and validate the message.
        var newMessage = Message.Create(
            message.ChannelId,
            message.SenderId,
            message.Content
        );

        // Save the message.
        _repository.Add(newMessage);

        _logger.LogInformation(
            "Message {MessageId} saved in channel {ChannelId}.",
            newMessage.Id,
            newMessage.ChannelId
        );

        // Publish the message event through RabbitMQ.
        await _messageClient.PublishAsync(new MessagePostedEvent
        {
            MessageId = newMessage.Id,
            ChannelId = newMessage.ChannelId,
            AuthorId = newMessage.SenderId,
            Content = newMessage.Content,
            PostedAt = newMessage.CreatedAt
        });

        _logger.LogInformation(
            "Message {MessageId} published successfully.",
            newMessage.Id
        );

        return newMessage;
    }

    public bool Update(Guid id, Message message)
    {
        var existing = _repository.GetById(id);

        if (existing == null)
        {
            _logger.LogWarning(
                "Message {MessageId} not found for update.", id);

            return false;
        }

        existing.ChannelId = message.ChannelId;
        existing.SenderId = message.SenderId;
        existing.Content = message.Content;

        _repository.Update(existing);

        _logger.LogInformation(
            "Message {MessageId} updated successfully.", id);

        return true;
    }

    public bool Delete(Guid id)
    {
        var existing = _repository.GetById(id);

        if (existing == null)
        {
            _logger.LogWarning(
                "Message {MessageId} not found for deletion.", id);

            return false;
        }

        _repository.Delete(existing);

        _logger.LogInformation(
            "Message {MessageId} deleted successfully.", id);

        return true;
    }
}
