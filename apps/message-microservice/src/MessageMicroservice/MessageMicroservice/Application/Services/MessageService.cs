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
        if (message.Id == Guid.Empty)
        {
            message.Id = Guid.NewGuid();
        }

        if (message.CreatedAt == default)
        {
            message.CreatedAt = DateTime.UtcNow;
        }

       
        _repository.Add(message);


        await _messageClient.PublishAsync(new MessagePublished
        {
            MessageId = message.Id,
            ChannelId = message.ChannelId,
            SenderId = message.SenderId,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        });

        return message;
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