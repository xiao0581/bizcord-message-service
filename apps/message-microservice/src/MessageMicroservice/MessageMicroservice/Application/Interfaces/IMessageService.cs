using MessageMicroservice.Domain.Entities;

namespace MessageMicroservice.Application.Interfaces;

public interface IMessageService
{
    IEnumerable<Message> GetAll();
    Message? GetById(Guid id);
    Task<Message> CreateAsync(Message message);
    bool Update(Guid id, Message message);
    bool Delete(Guid id);
}