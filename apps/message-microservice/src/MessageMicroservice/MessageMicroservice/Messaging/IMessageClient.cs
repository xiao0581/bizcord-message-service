namespace MessageMicroservice.Messaging;

public interface IMessageClient
{
    Task PublishAsync<T>(T message);

    Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler
    );
}