# Bizcord – Message Microservice

The Message Microservice is part of the Bizcord system, a Discord-like platform based on microservice architecture.

It provides REST API endpoints for creating, retrieving, updating and deleting messages, and uses RabbitMQ for asynchronous event-driven communication.

## Technologies

- ASP.NET Core (.NET 8)
- C#
- RabbitMQ with EasyNetQ
- Docker and Docker Compose
- xUnit and Moq

## Architecture

The Messaging Microservice follows a layered architecture:

- **Controller:** Handles HTTP requests and responses.
- **Application Service:** Implements business logic and publishes events.
- **Domain:** Defines the message entity and repository interface.
- **Repository:** Manages message storage using an in-memory implementation.
- **Messaging:** Publishes and subscribes to RabbitMQ events using EasyNetQ.
- **SharedModels:** Defines the shared `MessagePublished` event contract.

Messages are currently stored in memory and are lost when the application restarts.

## Running the Application

From the `apps/message-microservice` directory, run:

```bash
docker compose up --build
```

Once the containers are running, the following interfaces are available:

- **Swagger API:** http://localhost:8080/swagger
- **RabbitMQ Management:** http://localhost:15672

## Message Flow

1. A client sends a POST request to `/api/messages`.
2. `MessageService` creates the message and saves it using the repository.
3. The service publishes a `MessagePublished` event through `IMessageClient`.
4. RabbitMQ distributes the event to subscribed consumers.
5. `MessageSubscriber` receives the event and logs the message.

## Messaging Events

The main message creation workflow publishes a `MessagePublished` event defined in the `SharedModels` project.

The event contains the message ID, channel ID, sender ID, content and creation timestamp.

A separate `/api/messages/publish` endpoint uses the earlier `MessageSent` event for demonstration purposes.

The `MessageSubscriber` currently subscribes to `MessagePublished` events. Other microservices could subscribe to these events in the future.

## Testing

The project includes five unit tests using **xUnit and Moq**:

1. Create a message and verify that a `MessagePublished` event is published.
2. Update a non-existing message.
3. Delete a non-existing message.
4. Update an existing message.
5. Delete an existing message.

The tests use mocked repository and messaging dependencies, so RabbitMQ does not need to be running.

Run the tests using Visual Studio Test Explorer or execute the following command from the `apps/message-microservice` directory:

```bash
dotnet test src/MessageMicroservice/MessageMicroservice.sln
```

The REST API and RabbitMQ Publish/Subscribe workflow have also been manually verified using Swagger and Docker Compose.

## Known Limitations

- Message storage is currently in-memory and does not persist after application restarts.
- The current subscriber logs received events rather than performing additional business operations.
- Message storage and event publishing are not transactional. If event publishing fails, a message may already have been stored.

These limitations are acceptable for the current educational implementation and can be addressed in future development.