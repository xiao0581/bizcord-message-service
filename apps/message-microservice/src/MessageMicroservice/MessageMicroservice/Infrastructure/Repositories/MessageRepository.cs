using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;

namespace MessageMicroservice.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly List<Message> _messages = new();

    public IEnumerable<Message> GetAll()
    {
        return _messages;
    }

    public Message? GetById(Guid id)
    {
        return _messages.FirstOrDefault(m => m.Id == id);
    }

    public void Add(Message message)
    {
        _messages.Add(message);
    }

    public void Update(Message message)
    {
        // The message object is already updated in MessageService.
        // Since this repository uses in-memory object references,
        // no additional update operation is required.
    }

    public void Delete(Message message)
    {
        _messages.Remove(message);
    }
}