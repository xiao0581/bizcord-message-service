using MessageMicroservice.Messages;

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
        await _messageClient.SubscribeAsync<MessageSent>(
            "message-service",
            async message =>
            {
                Console.WriteLine(
                    $"收到消息: {message.Sender} - {message.Text}"
                );

                await Task.CompletedTask;
            });
    }
}