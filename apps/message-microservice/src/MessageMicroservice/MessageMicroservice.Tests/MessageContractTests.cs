using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MessageMicroservice.Tests;

public class MessageContractTests
{
    [Fact]
    public async Task PostMessage_PublishedEvent_MeetsContract()
    {
        // Arrange
        using var factory = new MessageApiFactory();
        using var client = factory.CreateClient();

        var channelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var request = new
        {
            ChannelId = channelId,
            AuthorId = authorId,
            Content = "Hello Bizcord"
        };

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/messages",
            request
        );

        // Assert: HTTP response
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert: Published event
        var evt = factory.FakeClient.PublishedEvent;

        Assert.NotNull(evt);
        Assert.NotEqual(Guid.Empty, evt.MessageId);
        Assert.Equal(channelId, evt.ChannelId);
        Assert.Equal(authorId, evt.AuthorId);
        Assert.Equal("Hello Bizcord", evt.Content);

        Assert.True(
            (DateTime.UtcNow - evt.PostedAt).Duration()
            < TimeSpan.FromSeconds(5)
        );
    }
}