# Reliability Measures — Message Microservice

## Architecture & Design

Retry and timeout policies are implemented in the
EasyNetQMessageClient class, in Messaging/EasyNetQMessageClient.cs.

This class handles communication with RabbitMQ, so it is the
appropriate infrastructure boundary for these policies.

The policies wrap only the publish operation, not the entire
MessageService.CreateAsync() method. This avoids repeating the
repository insertion during a publish retry.

## Policy for Mitigating Failures

### Retry

Polly retries selected connection failures and timeout exceptions
up to three times, with delays of 2, 4 and 8 seconds.

This gives temporary failures a chance to recover without
retrying indefinitely.

### Timeout

Each publish attempt has a five-second optimistic timeout.
The cancellation token is passed to EasyNetQ so that the
underlying operation can respond to cancellation.

The retry policy wraps the timeout policy, so each attempt
receives its own timeout.

## Testing / Simulation

### 1. Normal operation

With RabbitMQ running, a POST request was sent to /api/messages.

Observed result: the subscriber logged the message.

### 2. RabbitMQ unavailable

RabbitMQ was stopped while the message service remained running.
Another POST request was sent to /api/messages.

Observed result: logs showed three retries with delays of
2, 4 and 8 seconds, triggered by TimeoutRejectedException.
After retries were exhausted, the request ended with an
unhandled TimeoutRejectedException.

### 3. Recovery

RabbitMQ was restarted and a new POST request was sent.

Observed result: the API returned the newly created message,
including its ID and content.

This verifies handling of new requests after recovery.
It does not verify recovery during an active retry sequence.

## Remaining Limitations

- A message is saved before its event is published. If all
  publish attempts fail, the stored message remains without
  guaranteed event delivery.
- Failed events are not automatically resent after retries
  have been exhausted.
- Publishing retries may cause duplicate event delivery.
  Consumer-side deduplication is not implemented.
- The final publishing exception is not explicitly handled
  by the controller and currently results in an HTTP 500 response.
- In-memory data loss and repository concurrency risks remain.