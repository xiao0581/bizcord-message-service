using MessageMicroservice.Domain.Entities;

namespace MessageMicroservice.Domain.Repositories;

public interface IMessageRepository
{
    IEnumerable<Message> GetAll();
    Message? GetById(Guid id);
    void Add(Message message);
    void Update(Message message);
    void Delete(Message message);
}