using MessageMicroservice.Messaging;
using SharedModels.Events;

namespace MessageMicroservice.Tests;

public class FakeMessageClient : IMessageClient
{
    public MessagePostedEvent? PublishedEvent { get; private set; }

    public Task PublishAsync<T>(T message)
    {
        if (message is MessagePostedEvent evt)
        {
            PublishedEvent = evt;
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler)
    {
        return Task.CompletedTask;
    }
}