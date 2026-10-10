namespace MessageMicroservice.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid ChannelId { get; set; }

    public Guid SenderId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public static Message Create(
    Guid channelId,
    Guid senderId,
    string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Message content cannot be empty.",
                nameof(content)
            );
        }

        return new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = senderId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }
}