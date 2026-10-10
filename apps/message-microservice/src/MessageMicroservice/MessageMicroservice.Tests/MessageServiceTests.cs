using Xunit;
using Moq;
using MessageMicroservice.Application.Services;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Messaging;
using SharedModels.Events;

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
            r => r.Add(It.IsAny<Message>()),
            Times.Once
        );

        messageClientMock.Verify(
            m => m.PublishAsync(
                It.Is<MessagePublished>(e =>
                    e.MessageId == result.Id &&
                    e.Content == result.Content &&
                    e.SenderId == result.SenderId &&
                    e.ChannelId == result.ChannelId
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
}