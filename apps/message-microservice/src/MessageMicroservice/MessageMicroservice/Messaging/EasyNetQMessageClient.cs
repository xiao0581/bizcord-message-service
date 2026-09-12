using EasyNetQ;

namespace MessageMicroservice.Messaging;

public class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;

    public EasyNetQMessageClient(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync<T>(T message)
    {
        await _bus.PubSub.PublishAsync(message);
    }

    public async Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler)
    {
        await _bus.PubSub.SubscribeAsync<T>(
            subscriptionId,
            handler
        );
    }
}