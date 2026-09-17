using MessageMicroservice.Domain.Entities;

namespace MessageMicroservice.Application.Interfaces;

public interface IMessageService
{
    IEnumerable<Message> GetAll();
    Message? GetById(Guid id);
    Message Create(Message message);
    bool Update(Guid id, Message message);
    bool Delete(Guid id);
}