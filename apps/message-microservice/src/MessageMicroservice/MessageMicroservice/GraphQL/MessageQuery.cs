
using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Domain.Entities;

namespace MessageMicroservice.GraphQL;

// GraphQL presentation layer for reading messages.
// Reuses the existing application service without duplicating business logic.
public sealed class MessageQuery
{
    // Returns all messages through the existing service layer.
    public IEnumerable<Message> GetMessages(
        IMessageService messageService)
    {
        return messageService.GetAll();
    }

    // Returns a single message by its unique identifier.
    public Message? GetMessage(
        Guid id,
        IMessageService messageService)
    {
        return messageService.GetById(id);
    }
}
