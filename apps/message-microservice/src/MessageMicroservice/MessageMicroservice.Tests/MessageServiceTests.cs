using Xunit;
using Moq;
using MessageMicroservice.Application.Services;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Messaging;
using SharedModels.Events;
using MessageMicroservice.Domain.Entities;
namespace MessageMicroservice.Tests;

public class MessageServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldSaveAndPublishMessage()
    {
        // Arrange
        var repositoryMock = new Mock<IMessageRepository>();
        var messageClientMock = new Mock<IMessageClient>();

        var service = new MessageService(
            repositoryMock.Object,
            messageClientMock.Object
        );

        var message = new Message
        {
            ChannelId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Content = "Hello Unit Test"
        };

        // Act
        var result = await service.CreateAsync(message);

        // Assert

  
        Assert.NotEqual(Guid.Empty, result.Id);


        Assert.NotEqual(default(DateTime), result.CreatedAt);


        repositoryMock.Verify(
     r => r.Add(It.Is<Message>(m =>
         m.Id == result.Id &&
         m.ChannelId == result.ChannelId &&
         m.SenderId == result.SenderId &&
         m.Content == result.Content &&
         m.CreatedAt == result.CreatedAt
     )),
     Times.Once
 );

        messageClientMock.Verify(
     m => m.PublishAsync(
         It.Is<MessagePostedEvent>(e =>
             e.MessageId == result.Id &&
             e.ChannelId == result.ChannelId &&
             e.AuthorId == result.SenderId &&
             e.Content == result.Content &&
             e.PostedAt == result.CreatedAt
         )
     ),
     Times.Once
 );
    }
    [Fact]
    public void Update_ShouldReturnFalse_WhenMessageNotFound()
    {
        // Arrange
        var repositoryMock = new Mock<IMessageRepository>();
        var messageClientMock = new Mock<IMessageClient>();

        var service = new MessageService(
            repositoryMock.Object,
            messageClientMock.Object
        );

        var id = Guid.NewGuid();
        var message = new Message
        {
            Content = "Updated message"
        };

        // Act
        var result = service.Update(id, message);

        // Assert
        Assert.False(result);

        repositoryMock.Verify(
            r => r.Update(It.IsAny<Message>()),
            Times.Never
        );
    }

    [Fact]
    public void Delete_ShouldReturnFalse_WhenMessageNotFound()
    {
        // Arrange
        var repositoryMock = new Mock<IMessageRepository>();
        var messageClientMock = new Mock<IMessageClient>();

        var service = new MessageService(
            repositoryMock.Object,
            messageClientMock.Object
        );

        // Act
        var result = service.Delete(Guid.NewGuid());

        // Assert
        Assert.False(result);

        repositoryMock.Verify(
            r => r.Delete(It.IsAny<Message>()),
            Times.Never
        );
    }
    [Fact]
    public void Update_ShouldUpdateMessage_WhenMessageExists()
    {
        // Arrange
        var repositoryMock = new Mock<IMessageRepository>();
        var messageClientMock = new Mock<IMessageClient>();

        var service = new MessageService(
            repositoryMock.Object,
            messageClientMock.Object
        );

        var id = Guid.NewGuid();

        var existing = new Message
        {
            Id = id,
            Content = "Old message"
        };

        var updated = new Message
        {
            Content = "New message",
            ChannelId = Guid.NewGuid(),
            SenderId = Guid.NewGuid()
        };

        repositoryMock
            .Setup(r => r.GetById(id))
            .Returns(existing);

        // Act
        var result = service.Update(id, updated);

        // Assert
        Assert.True(result);
        Assert.Equal("New message", existing.Content);
        Assert.Equal(updated.ChannelId, existing.ChannelId);
        Assert.Equal(updated.SenderId, existing.SenderId);

        repositoryMock.Verify(
            r => r.Update(existing),
            Times.Once
        );
    }

    [Fact]
    public void Delete_ShouldDeleteMessage_WhenMessageExists()
    {
        // Arrange
        var repositoryMock = new Mock<IMessageRepository>();
        var messageClientMock = new Mock<IMessageClient>();

        var service = new MessageService(
            repositoryMock.Object,
            messageClientMock.Object
        );

        var id = Guid.NewGuid();

        var existing = new Message
        {
            Id = id,
            Content = "Message to delete"
        };

        repositoryMock
            .Setup(r => r.GetById(id))
            .Returns(existing);

        // Act
        var result = service.Delete(id);

        // Assert
        Assert.True(result);

        repositoryMock.Verify(
            r => r.Delete(existing),
            Times.Once
        );
    }

    [Fact]
    public void Message_Create_Throws_WhenContentIsEmpty()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Message.Create(channelId, senderId, "")
        );
    }
    [Fact]
    public void Message_Create_SetsAllFields()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var content = "Hello Bizcord";

        // Act
        var message = Message.Create(
            channelId,
            senderId,
            content
        );

        // Assert
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(channelId, message.ChannelId);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal(content, message.Content);

        Assert.True(
            (DateTime.UtcNow - message.CreatedAt).Duration()
            < TimeSpan.FromSeconds(5)
        );
    }
}