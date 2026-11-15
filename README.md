# Bizcord – Message Microservice

The Message Microservice is part of Bizcord, a Discord-like system built using a microservice architecture.

It provides REST endpoints for creating, retrieving, updating, and deleting messages, and uses RabbitMQ for asynchronous communication.

The project also includes a **File Upload Saga**, implemented as part of the W46 System Integration assignment.

## Technologies

- C# and ASP.NET Core (.NET 8)
- RabbitMQ with EasyNetQ
- Docker and Docker Compose
- xUnit and Moq
- ASP.NET Core WebApplicationFactory
- JSON-based Saga state persistence

## Architecture

The Message Microservice follows a layered architecture:

- **Controllers:** Handle incoming HTTP requests.
- **Application Services:** Implement application logic.
- **Domain:** Defines entities, validation, and repository interfaces.
- **Infrastructure:** Implements data storage.
- **Messaging:** Publishes and subscribes to RabbitMQ messages.
- **SharedModels:** Defines shared message contracts.
- **Sagas:** Coordinates file upload transactions and compensations.

The application uses dependency injection to separate business logic from infrastructure and messaging implementations.

The original Message Repository uses in-memory storage.

## Standard Message Workflow

1. A client sends `POST /api/messages`.
2. `MessagesController` receives the request.
3. `MessageService` creates a Message entity.
4. The Message Repository stores the message.
5. `MessageService` publishes `MessagePostedEvent`.
6. RabbitMQ distributes the event.
7. `MessageSubscriber` receives and logs the event.

The shared `MessagePostedEvent` contains:

- `MessageId`
- `ChannelId`
- `AuthorId`
- `Content`
- `PostedAt`

## W46 – File Upload Saga Pattern

The selected assignment scenario is:

**A user sends a file that turns out to be too large.**

The Saga coordinates a file upload workflow involving four simulated services.

| Service | Forward Transaction | Compensation |
|---|---|---|
| File Service | Store temporary file | Delete temporary file |
| Storage Quota Service | Reserve storage quota | Release storage quota |
| Message Service | Create attachment message | Retract message |
| Channel Service | Update channel activity | Revert channel activity |

### Saga Design

The implementation uses **Saga Orchestration**.

A central `FileUploadSagaOrchestrator` determines which operation should execute next.

All commands and events are transmitted through RabbitMQ using the existing `IMessageClient`.

Every Saga message contains a `SagaId` to identify the workflow.

The Orchestrator persists Saga progress through `IFileUploadSagaStateRepository`.

The laboratory implementation uses JSON files to store Saga state.

The four business handlers are simulated inside the same .NET application rather than deployed as independent microservices.

### Happy Path

The successful workflow is:

1. `StoreTemporaryFile`
2. `ReserveStorageQuota`
3. `CreateAttachmentMessage`
4. `UpdateChannelActivity`
5. Saga status becomes `Completed`

### Compensation Path

If the Channel Service fails after the previous operations succeeded, the Orchestrator performs compensations in reverse order:

1. `RevertChannelActivity`
2. `RetractAttachmentMessage`
3. `ReleaseStorageQuota`
4. `DeleteTemporaryFile`
5. Saga status becomes `Compensated`

If storage quota reservation fails, the Saga compensates only the operations that require cleanup.

For example, an oversized file is deleted without creating a message.

### Saga Statuses

| Status | Meaning |
|---|---|
| `Started` | Initial state |
| `Running` | Forward operations are in progress |
| `Compensating` | Recovery operations are in progress |
| `Completed` | All forward operations succeeded |
| `Compensated` | All required compensations succeeded |
| `CompensationFailed` | A compensation requires recovery |

## Running the Saga Locally

The Saga has been tested with RabbitMQ running in Docker and the .NET application running locally.

### 1. Start RabbitMQ

Start Docker Desktop.

If the existing laboratory container is available:

```powershell
docker start bizcord-saga-rabbitmq
```

For a new local setup, create a RabbitMQ container:

```powershell
docker run -d --name bizcord-saga-rabbitmq -p 5673:5672 -p 15673:15672 rabbitmq:3-management
```

Use the `docker run` command only when that container does not already exist.

RabbitMQ Management:

http://localhost:15673

Default local credentials:

- Username: `guest`
- Password: `guest`

### 2. Start the Message Microservice

Open PowerShell in:

`apps/message-microservice/src/MessageMicroservice`

Set the connection configuration:

```powershell
$env:RabbitMQ__Host = "localhost:5673"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Build the solution:

```powershell
dotnet build .\MessageMicroservice.sln
```

Run the application:

```powershell
dotnet run --project .\MessageMicroservice\MessageMicroservice.csproj --no-launch-profile
```

Open the Swagger endpoint using the HTTP port displayed in the application's console output.

The repository also contains a Docker Compose configuration used for other integration exercises. Its current Vault-related requirements must be configured separately when running the full Compose stack.

## Saga API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/sagas/file-upload` | Start a file upload Saga |
| GET | `/api/sagas/file-upload/{sagaId}` | Retrieve Saga progress |
| POST | `/api/sagas/testing/fail-next-channel-update` | Simulate a Channel failure in Development |

Starting a Saga returns HTTP `202 Accepted`.

The workflow runs asynchronously, so the client checks completion using the generated `SagaId`.

### Example Request

```json
{
  "userId": "11111111-1111-4111-8111-111111111111",
  "channelId": "22222222-2222-4222-8222-222222222222",
  "fileName": "photo.jpg",
  "fileSizeBytes": 4194304,
  "content": "My first Saga test"
}
```

The simulated storage quota is 10 MiB per user.

## Testing

### Existing Automated Tests

The original Message Microservice includes 10 automated tests:

- 7 unit tests
- 1 provider contract test
- 2 integration tests

The tests use xUnit, Moq, and ASP.NET Core WebApplicationFactory.

Run the tests from `apps/message-microservice/src/MessageMicroservice`:

```powershell
dotnet test .\MessageMicroservice.sln
```

### Saga Manual Tests

Three Saga scenarios were manually tested on 15 November 2026 using Swagger and RabbitMQ.

| Scenario | Expected Status | Observed Status | Result |
|---|---|---|---|
| Upload a 4 MiB file | `Completed` | `Completed` | PASS |
| Upload a 20 MiB file | `Compensated` | `Compensated` | PASS |
| Simulate Channel update failure | `Compensated` | `Compensated` | PASS |

The Channel failure scenario confirmed that all four compensation operations reported success:

- Channel activity reverted
- Message retracted
- Storage quota released
- Temporary file deleted

These are manual integration checks. Automated Saga tests have not yet been added.

## Implementation Limitations

The Saga implementation is designed for learning and demonstration.

Current limitations include:

- File storage is simulated using in-memory metadata.
- Storage quota and channel activity use in-memory state.
- The attachment reference is stored in message content.
- Saga state is persisted locally as JSON files.
- Handler idempotency records do not survive application restarts.
- There is no Transactional Outbox or durable inbox implementation.
- Distributed recovery and multi-instance coordination are not production-ready.

Future improvements include durable database storage, automated Saga integration tests, reliable retries, Transactional Outbox, and recovery workers.

## Documentation

Detailed Saga workflow design, compensation sequences, implementation decisions, and manual test results are available in:

[File Upload Saga Workflow](docs/saga-workflow.md)

Additional architectural documentation is available in:

[Analysis and Design](docs/analysis-and-design.md)

## Key Learning Outcomes

This project demonstrates:

- Event-driven communication using RabbitMQ
- Microservice boundaries and shared contracts
- Saga Orchestration
- Forward and compensating transactions
- Partial failure handling
- Eventual consistency
- Idempotency
- Saga state persistence
- Asynchronous API processing

The main principle is that **Saga coordinates local business transactions and compensating actions instead of relying on a single distributed database rollback**.