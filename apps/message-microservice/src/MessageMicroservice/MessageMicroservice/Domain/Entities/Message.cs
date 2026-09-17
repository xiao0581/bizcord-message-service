namespace MessageMicroservice.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid ChannelId { get; set; }

    public Guid SenderId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}