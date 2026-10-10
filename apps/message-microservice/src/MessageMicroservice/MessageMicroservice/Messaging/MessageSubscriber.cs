using SharedModels.Events;

namespace MessageMicroservice.Messaging;

public class MessageSubscriber : BackgroundService
{
    private readonly IMessageClient _messageClient;

    public MessageSubscriber(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await _messageClient.SubscribeAsync<MessagePostedEvent>(
     "message-service",
     async message =>
     {
         Console.WriteLine(
             $"收到消息: {message.AuthorId} - {message.Content}"
         );

         await Task.CompletedTask;
     });
    }
}