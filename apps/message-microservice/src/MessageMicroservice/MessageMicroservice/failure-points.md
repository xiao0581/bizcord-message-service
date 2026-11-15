# Potential Failure Points — Message Microservice

## 1. RabbitMQ connection or publishing failure

Location: `EasyNetQMessageClient.PublishAsync<T>()`

If RabbitMQ is unavailable or the network connection fails,
publishing a MessagePostedEvent may fail.

The application does not explicitly define a retry policy or
timeout configuration for this operation. EasyNetQ's built-in
behaviour should be verified before adding additional policies.

## 2. Message saved but event not published

Location: `MessageService.CreateAsync()`

The message is added to the repository before the event is published.
If publishing fails, the message remains stored, but other services
may not receive the event.

Saving the message and publishing the event are not atomic,
and there is no application-level mechanism to resend the event.

## 3. Duplicate messages after request retries

Location: `MessageService.CreateAsync()`

If a message is saved but the request fails afterwards, or the
response is lost, the client may retry the request.

Each call creates a new message. There is no request-level
idempotency mechanism to prevent duplicate creation.

## 4. Data loss after a service restart

Location: `MessageRepository` — `_messages` field

Messages are stored in an in-memory List<Message>.
All stored messages are lost if the process crashes or restarts.

The service currently has no persistent database.

## 5. Concurrent access to shared in-memory storage

Location: `MessageRepository.Add()`, `Delete()` and `GetAll()`;
singleton registration in `Program.cs`.

Multiple requests share the same List<Message>, which is not
protected against concurrent reads and writes.

Concurrent modification and enumeration may cause exceptions
or inconsistent results.