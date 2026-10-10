namespace MessageMicroservice.Contracts.Requests;

public class CreateMessageRequest
{
    public Guid ChannelId { get; set; }

    public Guid AuthorId { get; set; }

    public required string Content { get; set; }
}