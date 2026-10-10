using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Messaging;
using MessageMicroservice.Messages;
using SharedModels.Events;

namespace MessageMicroservice.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _repository;
    private readonly IMessageClient _messageClient;

    public MessageService(
        IMessageRepository repository,
        IMessageClient messageClient)
    {
        _repository = repository;
        _messageClient = messageClient;
    }

    public IEnumerable<Message> GetAll()
    {
        return _repository.GetAll();
    }

    public Message? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public async Task<Message> CreateAsync(Message message)
    {
        // 1. Create and validate
        var newMessage = Message.Create(
            message.ChannelId,
            message.SenderId,
            message.Content
        );

        // 2. Save message
        _repository.Add(newMessage);

        // 3. Publish event
        await _messageClient.PublishAsync(new MessagePostedEvent
        {
            MessageId = newMessage.Id,
            ChannelId = newMessage.ChannelId,
            AuthorId = newMessage.SenderId,
            Content = newMessage.Content,
            PostedAt = newMessage.CreatedAt
        });

        return newMessage;
    }

    public bool Update(Guid id, Message message)
    {
        var existing = _repository.GetById(id);

        if (existing == null)
            return false;

        existing.ChannelId = message.ChannelId;
        existing.SenderId = message.SenderId;
        existing.Content = message.Content;

        _repository.Update(existing);

        return true;
    }

    public bool Delete(Guid id)
    {
        var existing = _repository.GetById(id);

        if (existing == null)
            return false;

        _repository.Delete(existing);

        return true;
    }
}