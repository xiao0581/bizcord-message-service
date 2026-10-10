namespace SharedModels.Events;

public class MessagePublished
{
	public Guid MessageId { get; set; }

	public Guid ChannelId { get; set; }

	public Guid SenderId { get; set; }

	public string Content { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}