using System.Net;
using System.Net.Http.Json;
using Xunit;
using MessageMicroservice.Domain.Entities;

namespace MessageMicroservice.Tests;

public class MessageIntegrationTests
{
    [Fact]
    public async Task PostMessage_Returns201_AndPersistsMessage()
    {
        // Arrange
        using var factory = new MessageApiFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello World"
        };

        // Act 1: POST - Create message
        var response = await client.PostAsJsonAsync(
            "/api/messages",
            request
        );

        // Assert 1: Check HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Read created message from response
        var body = await response.Content.ReadFromJsonAsync<Message>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Act 2: GET - Retrieve the same message
        var getResponse = await client.GetAsync(
            $"/api/messages/{body.Id}"
        );

        // Assert 2: Check HTTP 200
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetMessage_ReturnsCorrectMessage()
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
            Content = "Testing GET API"
        };

        // Step 1: Create a message
        var postResponse = await client.PostAsJsonAsync(
            "/api/messages",
            request
        );

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var createdMessage =
            await postResponse.Content.ReadFromJsonAsync<Message>();

        Assert.NotNull(createdMessage);

        // Step 2: GET the created message
        var getResponse = await client.GetAsync(
            $"/api/messages/{createdMessage.Id}"
        );

        // Step 3: Verify HTTP 200
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Step 4: Read the GET response
        var retrievedMessage =
            await getResponse.Content.ReadFromJsonAsync<Message>();

        Assert.NotNull(retrievedMessage);

        // Step 5: Verify the returned data
        Assert.Equal(createdMessage.Id, retrievedMessage.Id);
        Assert.Equal(channelId, retrievedMessage.ChannelId);
        Assert.Equal(authorId, retrievedMessage.SenderId);
        Assert.Equal("Testing GET API", retrievedMessage.Content);
    }
}