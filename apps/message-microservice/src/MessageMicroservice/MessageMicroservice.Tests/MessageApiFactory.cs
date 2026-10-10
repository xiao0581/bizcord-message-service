using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MessageMicroservice.Messaging;

namespace MessageMicroservice.Tests;

public class MessageApiFactory : WebApplicationFactory<Program>
{
    public FakeMessageClient FakeClient { get; } = new();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace the real RabbitMQ message client
            services.RemoveAll<IMessageClient>();

            services.AddSingleton<IMessageClient>(FakeClient);

            // Disable the real RabbitMQ subscriber
            services.RemoveAll<
                Microsoft.Extensions.Hosting.IHostedService>();
        });
    }
}