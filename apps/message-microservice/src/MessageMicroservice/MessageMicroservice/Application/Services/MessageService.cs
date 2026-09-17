using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;

namespace MessageMicroservice.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _repository;

    public MessageService(IMessageRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<Message> GetAll()
    {
        return _repository.GetAll();
    }

    public Message? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public Message Create(Message message)
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