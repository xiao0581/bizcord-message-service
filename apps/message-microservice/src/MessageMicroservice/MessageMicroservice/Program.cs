
using EasyNetQ;
using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Application.Services;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Infrastructure.Repositories;
using MessageMicroservice.Messaging;
using MessageMicroservice.Sagas;
using MessageMicroservice.Sagas.Services;
using MessageMicroservice.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// REST API configuration
// ============================================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// GraphQL configuration - Week 47
// ============================================================

// Register HotChocolate and the existing MessageQuery resolver.
// GraphQL reuses IMessageService instead of duplicating business logic.
builder.Services
    .AddGraphQLServer()
    .AddQueryType<MessageQuery>();

// ============================================================
// RabbitMQ configuration
// ============================================================

// Register the existing RabbitMQ bus.
builder.Services.AddSingleton<IBus>(
    RabbitHutch.CreateBus(
        $"host={builder.Configuration["RabbitMQ:Host"] ?? "localhost"}"
    )
);

// Register the existing message client.
builder.Services.AddSingleton<
    IMessageClient,
    EasyNetQMessageClient>();

// Keep the existing message subscriber.
builder.Services.AddHostedService<MessageSubscriber>();

// ============================================================
// Existing Message Microservice
// ============================================================

// Register the existing message repository and service.
builder.Services.AddSingleton<
    IMessageRepository,
    MessageRepository>();

builder.Services.AddScoped<
    IMessageService,
    MessageService>();

// ============================================================
// Saga State Repository
// ============================================================

// Store Saga state outside the Git repository.
// Use System.IO.Path to avoid conflicts with HotChocolate.Path.
string sagaStoragePath = System.IO.Path.Combine(
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData),
    "Bizcord",
    "W46SagaState"
);

// Persist Saga state as JSON files.
builder.Services.AddSingleton<
    IFileUploadSagaStateRepository>(
    _ => new JsonFileUploadSagaStateRepository(
        sagaStoragePath
    )
);

// ============================================================
// Saga Orchestrator
// ============================================================

// Register the existing Saga workflow coordinator.
builder.Services.AddSingleton<
    FileUploadSagaOrchestrator>();

// ============================================================
// Saga Business Handlers
// ============================================================

// File storage and compensation.
builder.Services.AddSingleton<
    FileStorageSagaHandler>();

// Quota reservation and compensation.
builder.Services.AddSingleton<
    StorageQuotaSagaHandler>();

// Message creation and retraction.
builder.Services.AddSingleton<
    AttachmentMessageSagaHandler>();

// Channel updates and compensation.
builder.Services.AddSingleton<
    ChannelSagaHandler>();

// ============================================================
// Saga RabbitMQ Subscriber
// ============================================================

// Connect Saga events and commands to their handlers.
builder.Services.AddHostedService<
    FileUploadSagaSubscriber>();

// ============================================================
// Application startup
// ============================================================

var app = builder.Build();

// Enable Swagger in Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Keep the existing REST API endpoints.
app.MapControllers();

// Register the GraphQL endpoint at /graphql.
app.MapGraphQL();

app.Run();

// Keep Program accessible to integration tests.
public partial class Program { }
