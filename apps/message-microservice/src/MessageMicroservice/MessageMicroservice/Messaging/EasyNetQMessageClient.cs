using EasyNetQ;
using Polly;
using Polly.Timeout;
using RabbitMQ.Client.Exceptions;

namespace MessageMicroservice.Messaging;

public class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;
    private readonly IAsyncPolicy _publishPolicy;

    public EasyNetQMessageClient(IBus bus)
    {
        _bus = bus;

        // Request cancellation after 5 seconds for each publish attempt.
        var timeoutPolicy = Policy.TimeoutAsync(
            TimeSpan.FromSeconds(5),
            TimeoutStrategy.Optimistic);

        // Retry transient connection failures and timeouts up to 3 times.
        var retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<AlreadyClosedException>()
            .Or<System.IO.IOException>()
            .Or<TimeoutException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, delay, retryNumber, context) =>
                {
                    Console.WriteLine(
                        $"[Retry] Attempt {retryNumber}/3. " +
                        $"Waiting {delay.TotalSeconds} seconds. " +
                        $"Error: {exception.GetType().Name}: " +
                        exception.Message);
                });

        // Apply retry outside the timeout policy.
        // Each attempt receives its own timeout.
        _publishPolicy = Policy.WrapAsync(
            retryPolicy,
            timeoutPolicy);
    }

    public async Task PublishAsync<T>(T message)
    {
        // Pass the timeout cancellation token to EasyNetQ.
        await _publishPolicy.ExecuteAsync(
            cancellationToken =>
                _bus.PubSub.PublishAsync(
                    message,
                    cancellationToken),
            CancellationToken.None);
    }

    public async Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler)
    {
        await _bus.PubSub.SubscribeAsync<T>(
            subscriptionId,
            handler);
    }
}