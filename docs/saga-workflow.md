# File Upload Saga – Workflow Design and Implementation

## 1. Overview

This document describes the implementation of the Saga Pattern in the Bizcord Message Microservice.

The selected scenario is:

**A user sends a file that turns out to be too large.**

The implementation demonstrates how a distributed business workflow can coordinate multiple local operations and recover from partial failures using compensating transactions.

The solution uses:

- C# and ASP.NET Core (.NET 8)
- RabbitMQ and EasyNetQ
- An existing `IMessageClient` abstraction
- A custom Saga Orchestrator
- JSON-based Saga state persistence
- Simulated File, Storage Quota, Message, and Channel services

No dedicated Saga framework is used.

## 2. Business Scenario

When a user uploads a file to a Bizcord channel, several operations must succeed:

1. Store the file temporarily.
2. Reserve storage quota for the user.
3. Create a message containing the file reference.
4. Update the channel activity.

If all operations succeed, the Saga completes.

If an operation fails, the Saga coordinates compensating transactions to reverse the business effects of the previously completed operations.

For this implementation, each user has a simulated storage quota of **10 MiB (10,485,760 bytes)**.

A file larger than the remaining quota is rejected.

## 3. Architecture and Service Responsibilities

| Component | Responsibility | Compensation |
|---|---|---|
| File Service | Temporarily store file metadata | Delete the temporary file |
| Storage Quota Service | Reserve storage quota | Release reserved quota |
| Message Service | Create a message with an attachment reference | Retract the message |
| Channel Service | Update channel activity | Revert the channel update |
| Saga Orchestrator | Coordinate workflow steps and failures | Trigger compensation commands |

The four service handlers run inside the Message Microservice application for this laboratory exercise. They represent separate business responsibilities but are not deployed as independent microservices.

RabbitMQ provides asynchronous communication between the handlers and the Orchestrator.

## 4. Saga Pattern Selection

### Selected Pattern: Orchestration

The implementation uses **Saga Orchestration**.

A central `FileUploadSagaOrchestrator` determines which command to publish next based on the events received.

For example:

1. The Orchestrator publishes `StoreTemporaryFile`.
2. The File Handler publishes `TemporaryFileStored`.
3. The Orchestrator receives that event and publishes `ReserveStorageQuota`.
4. The workflow continues according to the result.

If a service reports failure, the Orchestrator determines the required compensation sequence.

### Why Orchestration Instead of Choreography?

Orchestration was selected because:

- The workflow contains several dependent steps.
- The compensation order must be controlled.
- A central component makes the workflow easier to understand and debug.
- Saga state and failure handling can be monitored in one place.
- Additional failure-handling rules can be implemented without distributing workflow decisions across all services.

In a choreography-based implementation, services would react to events and decide what to do next without a central coordinator.

Choreography can reduce central coordination, but it may become more difficult to follow a workflow involving several compensating transactions.

## 5. Happy Path – Successful File Upload

The normal workflow executes the following steps:

| Step | Command | Success Event |
|---|---|---|
| 1 | `StoreTemporaryFile` | `TemporaryFileStored` |
| 2 | `ReserveStorageQuota` | `StorageQuotaReserved` |
| 3 | `CreateAttachmentMessage` | `AttachmentMessageCreated` |
| 4 | `UpdateChannelActivity` | `ChannelActivityUpdated` |

The Orchestrator waits for the success event of each step before sending the next command.

When all four steps succeed, the Saga state becomes:

**Status: `Completed`**

No compensation is required.

## 6. Failure Scenario A – File Exceeds Storage Quota

The main assignment scenario is a user attempting to upload a file larger than the available storage quota.

The failure workflow is:

1. `StoreTemporaryFile` succeeds.
2. The File Handler publishes `TemporaryFileStored`.
3. The Orchestrator publishes `ReserveStorageQuota`.
4. The Storage Quota Handler detects insufficient quota.
5. The Storage Quota Handler publishes `StorageQuotaRejected`.
6. The Orchestrator changes the Saga status to `Compensating`.
7. The Orchestrator publishes `DeleteTemporaryFile`.
8. The File Handler removes the temporary file record.
9. The File Handler publishes `TemporaryFileDeleted`.
10. The Orchestrator changes the final Saga status to `Compensated`.

In this scenario, no quota was successfully reserved and no message was created. Therefore, quota release and message retraction are not required.

**Final status: `Compensated`**

## 7. Failure Scenario B – Channel Update Failure

A second scenario tests failure after several business operations have already succeeded.

The forward operations are:

1. File storage succeeds.
2. Storage quota reservation succeeds.
3. Message creation succeeds.
4. Channel activity is modified, but the Channel Handler reports a simulated failure.

The Channel Handler publishes `ChannelActivityFailed`.

The Orchestrator then performs compensations in reverse business order.

| Order | Compensation Command | Success Event |
|---|---|---|
| 1 | `RevertChannelActivity` | `ChannelActivityReverted` |
| 2 | `RetractAttachmentMessage` | `AttachmentMessageRetracted` |
| 3 | `ReleaseStorageQuota` | `StorageQuotaReleased` |
| 4 | `DeleteTemporaryFile` | `TemporaryFileDeleted` |

The Orchestrator moves to the next compensation only after the previous compensation reports success.

When all required compensations succeed:

**Final status: `Compensated`**

This scenario demonstrates how Saga handles a partial failure occurring after multiple operations have already completed.

## 8. Saga Messages and Correlation

The message contracts are defined in:

`SharedModels/Sagas/FileUploadSagaMessages.cs`

All Saga commands and events inherit from `SagaMessage`.

Each message contains a `SagaId`.

The `SagaId` is used to:

- Correlate messages belonging to the same workflow.
- Load the correct persisted Saga state.
- Match responses with the expected workflow step.
- Support duplicate-message handling.

The Orchestrator also checks the expected `CurrentStep` and Saga status before processing events.

This helps avoid applying events that do not match the current workflow state.

## 9. Saga State Persistence

Saga state is represented by `FileUploadSagaState`.

The implementation records:

- `SagaId`
- User, channel, file, and message identifiers
- The current workflow step
- The current Saga status
- Completed forward transactions
- Completed compensation transactions
- Error information
- Start and completion timestamps

Saga state is saved through `IFileUploadSagaStateRepository`.

The implementation uses `JsonFileUploadSagaStateRepository`, which writes Saga state to local JSON files.

This allows the Orchestrator to retrieve previously saved workflow progress.

The JSON repository is intended for this laboratory exercise. It is not a production-grade distributed transaction store.

## 10. Saga Statuses

| Status | Meaning |
|---|---|
| `Started` | Initial state before processing begins |
| `Running` | Forward transactions are being executed |
| `Compensating` | The Saga is executing recovery operations |
| `Completed` | All business operations succeeded |
| `Compensated` | Required compensation operations completed |
| `CompensationFailed` | A compensation failed and requires recovery |

A successful business operation results in `Completed`.

A failed business operation with successful compensation results in `Compensated`.

## 11. Idempotency

RabbitMQ messages may be delivered more than once, so handlers should avoid repeating business effects.

Examples implemented in the exercise include:

- Preventing duplicate file storage records.
- Avoiding duplicate storage quota reservations.
- Avoiding duplicate message creation for the same Saga.
- Preventing duplicate channel activity increments.
- Treating repeated deletion of an already absent file as successful.
- Avoiding repeated quota release.

These protections use in-memory data structures and are suitable for demonstrating the principle.

They do not provide complete duplicate-message protection across application restarts or multiple instances.

## 12. API Endpoints

The application exposes the following endpoints:

| Endpoint | Purpose |
|---|---|
| `POST /api/sagas/file-upload` | Publish a new Saga start command |
| `GET /api/sagas/file-upload/{sagaId}` | Retrieve persisted Saga status |
| `POST /api/sagas/testing/fail-next-channel-update` | Simulate a Channel update failure in Development |

The POST endpoint returns HTTP `202 Accepted` when the start command has been published.

Because the workflow is asynchronous, this response does not guarantee business completion.

Clients use the returned `SagaId` to check the final status.

## 13. Manual Test Results

Three scenarios were tested using Swagger, RabbitMQ, and the running ASP.NET Core application on 15 November 2026.

### Test 1 – Successful Upload

**Input:** `photo.jpg`, 4 MiB (4,194,304 bytes)

**SagaId:** `acdb978a-f7d1-43a7-9647-9583bca33c36`

**Observed results:**

| Property | Actual Value |
|---|---|
| `status` | `Completed` |
| `fileStored` | `true` |
| `quotaReserved` | `true` |
| `messageCreated` | `true` |
| `channelUpdated` | `true` |
| `lastError` | `null` |

**Result: PASS**

All forward transactions succeeded.

### Test 2 – File Exceeds Quota

**Input:** `large-video.mp4`, 20 MiB (20,971,520 bytes)

**SagaId:** `150c9416-a898-4a15-b0d9-26ad2709106b`

**Observed results:**

| Property | Actual Value |
|---|---|
| `status` | `Compensated` |
| `fileStored` | `true` |
| `quotaReserved` | `false` |
| `messageCreated` | `false` |
| `fileDeleted` | `true` |
| `lastError` | `Storage quota exceeded.` |

**Result: PASS**

The storage quota was rejected, and the temporary file deletion compensation succeeded.

### Test 3 – Channel Update Failure

**Input:** `channel-failure-test.jpg`, 2 MiB (2,097,152 bytes)

**SagaId:** `d3ea6696-32af-44d4-9e95-8f22e7350fdd`

**Observed results:**

| Property | Actual Value |
|---|---|
| `status` | `Compensated` |
| `fileStored` | `true` |
| `quotaReserved` | `true` |
| `messageCreated` | `true` |
| `channelUpdated` | `false` |
| `channelReverted` | `true` |
| `messageRetracted` | `true` |
| `quotaReleased` | `true` |
| `fileDeleted` | `true` |
| `lastError` | `Simulated failure after channel update.` |

**Result: PASS**

All four compensation steps reported successful completion.

These are manual integration test observations, not automated test results. Additional assertions against service data would further strengthen the tests.

## 14. Limitations and Future Improvements

The implementation demonstrates Saga orchestration and compensation but has several limitations:

1. **Simulated services:** File, Storage Quota, and Channel operations are simulated inside the Message Microservice process.

2. **In-memory business data:** Handler data and message storage can be lost when the application restarts.

3. **Local JSON persistence:** Saga progress is saved locally, but this storage is not designed for multiple application instances.

4. **No Transactional Outbox:** Saving Saga state and publishing RabbitMQ messages are separate operations. A failure between them can leave the workflow waiting for a message that was never published.

5. **Limited restart recovery:** Persisted Saga state alone does not guarantee complete recovery of all business operations.

6. **Limited idempotency guarantees:** In-memory deduplication does not survive process restarts.

7. **Simplified attachments and unread counts:** Files are represented by metadata, and attachment references are stored in message content. Channel unread counts are simulated rather than maintained per user.

8. **Development-only failure simulation:** The testing endpoint is restricted to Development but should not be deployed as a production management interface.

Future improvements could include PostgreSQL persistence, transactional outbox/inbox patterns, durable command processing, automated recovery workers, retries with backoff, and automated integration tests.

## 15. Conclusion

The implementation demonstrates the Saga Pattern using a central Orchestrator and asynchronous RabbitMQ communication.

Three manually verified scenarios succeeded:

1. Successful file upload.
2. Storage quota rejection followed by file cleanup.
3. Channel update failure followed by four compensating operations.

The project demonstrates how a distributed workflow can recover from partial business failures without relying on a single database transaction across all services.

The main lesson is that **a Saga coordinates local transactions and compensating actions to achieve business-level eventual consistency**.