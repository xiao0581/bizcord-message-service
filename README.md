# Bizcord – Message Microservice

The Message Microservice is part of the Bizcord system, a Discord-like platform based on a microservice architecture.

It provides REST API endpoints for creating, retrieving, updating, and deleting messages. It also uses RabbitMQ for asynchronous event-driven communication between services.

## Technologies

- ASP.NET Core (.NET 8) and C#
- RabbitMQ with EasyNetQ
- Docker and Docker Compose
- xUnit and Moq
- ASP.NET Core WebApplicationFactory for API testing

## Architecture

The Message Microservice follows a layered architecture:

- **Controller:** Handles HTTP requests, responses, and request mapping.
- **Application Service:** Implements application logic and publishes messaging events.
- **Domain:** Defines the Message entity, domain validation, and repository interface.
- **Infrastructure:** Implements message storage using an in-memory repository.
- **Messaging:** Publishes and subscribes to RabbitMQ events using EasyNetQ.
- **SharedModels:** Defines the shared `MessagePostedEvent` event contract.

The application uses dependency injection to separate business logic from infrastructure and messaging implementations.

Messages are currently stored in memory and are lost when the application restarts.

## Running the Application

From the `apps/message-microservice` directory, run:

```bash
docker compose up --build
```

Once the containers are running, the following interfaces are available:

- **Swagger API:** http://localhost:8080/swagger
- **RabbitMQ Management:** http://localhost:15672

The Docker Compose configuration starts the API and RabbitMQ containers and waits for RabbitMQ to become healthy before starting the message service.

## Message Flow

1. A client sends a POST request to `/api/messages`.
2. `MessagesController` passes the request to `MessageService`.
3. `MessageService` creates a valid Message using the domain model.
4. The message is saved through `IMessageRepository`.
5. The service publishes a `MessagePostedEvent` through `IMessageClient`.
6. RabbitMQ distributes the event to subscribed consumers.
7. `MessageSubscriber` receives the event and logs the message information.

## Messaging Events

The main message creation workflow publishes a `MessagePostedEvent` defined in the `SharedModels` project.

The event contains:

- `MessageId`
- `ChannelId`
- `AuthorId`
- `Content`
- `PostedAt`

The event is published through EasyNetQ and RabbitMQ.

The `MessageSubscriber` subscribes to `MessagePostedEvent` and logs received events.

The earlier `MessageSent` event is retained for demonstration purposes.

## Testing

The project contains **10 automated tests** using xUnit, Moq, and ASP.NET Core WebApplicationFactory.

### Unit Tests (7)

The unit tests verify:

1. Creating a message, saving it through the repository, and publishing a `MessagePostedEvent`.
2. Updating a message that does not exist.
3. Deleting a message that does not exist.
4. Updating an existing message.
5. Deleting an existing message.
6. Rejecting message creation when the content is empty.
7. Creating a valid Message with the expected fields and timestamp.

Moq is used to isolate `MessageService` from the real repository and RabbitMQ client.

### Provider Contract Test (1)

The provider contract test verifies that a POST request creates a message and publishes a `MessagePostedEvent` containing the expected fields and values.

### Integration Tests (2)

The integration tests use `WebApplicationFactory` to test the ASP.NET Core API.

They verify:

- A POST request returns HTTP 201 Created and the created message can be retrieved using GET.
- A GET request returns the correct message data after creation.

The automated API tests replace the RabbitMQ client with a fake implementation, so a running RabbitMQ server is not required.

### Running Tests

From the `apps/message-microservice` directory:

```bash
dotnet test src/MessageMicroservice/MessageMicroservice.sln
```

The REST API and RabbitMQ publish/subscribe workflow have also been manually verified using Swagger and Docker Compose.

