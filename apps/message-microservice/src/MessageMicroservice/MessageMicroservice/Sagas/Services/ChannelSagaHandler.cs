
using MessageMicroservice.Messaging;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas.Services;

// Simulates channel activity management for the Saga lab.
public sealed class ChannelSagaHandler
{
    private readonly IMessageClient _messageClient;

    // Stores the current state of each channel.
    private readonly Dictionary<Guid, ChannelState> _channels = new();

    // Tracks channel updates by SagaId for idempotency.
    private readonly Dictionary<Guid, ChannelUpdateRecord> _updates = new();

    // Protects shared in-memory state.
    private readonly object _sync = new();

    // Allows testing a failure after a channel update.
    private bool _failNextUpdateAfterApplying;

    public ChannelSagaHandler(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    // Configures the next new channel update to fail after applying changes.
    public void SimulateNextUpdateFailure()
    {
        lock (_sync)
        {
            _failNextUpdateAfterApplying = true;
        }
    }

    // Handles the command to update channel activity.
    public async Task HandleUpdateAsync(UpdateChannelActivity command)
    {
        string? failureReason = null;

        lock (_sync)
        {
            // Validate the incoming command.
            if (command.SagaId == Guid.Empty ||
                command.ChannelId == Guid.Empty ||
                command.MessageId == Guid.Empty)
            {
                failureReason = "Invalid channel update request.";
            }
            else if (_updates.TryGetValue(
                command.SagaId,
                out var existing))
            {
                // Prevent duplicate channel updates.
                if (existing.ChannelId != command.ChannelId ||
                    existing.MessageId != command.MessageId)
                {
                    failureReason =
                        "SagaId already belongs to another channel update.";
                }
                else
                {
                    Console.WriteLine(
                        $"Channel already updated for Saga: {command.SagaId}");
                }
            }
            else
            {
                // Create a channel state if it does not exist.
                if (!_channels.TryGetValue(
                    command.ChannelId,
                    out var channel))
                {
                    channel = new ChannelState();

                    _channels.Add(command.ChannelId, channel);
                }

                // Record the new activity.
                DateTime updatedAt = DateTime.UtcNow;

                _updates.Add(
                    command.SagaId,
                    new ChannelUpdateRecord(
                        command.ChannelId,
                        command.MessageId,
                        updatedAt
                    )
                );

                // Update the simplified unread counter.
                channel.UnreadCount++;

                // Update the latest activity timestamp.
                channel.LastActivityAtUtc = updatedAt;

                Console.WriteLine(
                    $"Channel updated: {command.ChannelId}");

                Console.WriteLine(
                    $"Unread count: {channel.UnreadCount}");

                // Simulate a partial failure for testing.
                if (_failNextUpdateAfterApplying)
                {
                    _failNextUpdateAfterApplying = false;

                    failureReason =
                        "Simulated failure after channel update.";
                }
            }
        }

        // Report a failure to the Saga Orchestrator.
        if (failureReason is not null)
        {
            Console.WriteLine(
                $"Channel update failed: {failureReason}");

            await _messageClient.PublishAsync(
                new ChannelActivityFailed
                {
                    SagaId = command.SagaId,
                    Reason = failureReason
                });

            return;
        }

        // Confirm successful channel update.
        await _messageClient.PublishAsync(
            new ChannelActivityUpdated
            {
                SagaId = command.SagaId
            });
    }

    // Handles the compensating command to revert channel activity.
    public async Task HandleRevertAsync(RevertChannelActivity command)
    {
        string? failureReason = null;

        lock (_sync)
        {
            if (command.SagaId == Guid.Empty ||
                command.ChannelId == Guid.Empty ||
                command.MessageId == Guid.Empty)
            {
                failureReason = "Invalid channel compensation request.";
            }
            else if (_updates.TryGetValue(
                command.SagaId,
                out var existing))
            {
                // Verify ownership of the original update.
                if (existing.ChannelId != command.ChannelId ||
                    existing.MessageId != command.MessageId)
                {
                    failureReason =
                        "Channel update does not match the Saga.";
                }
                else
                {
                    // Remove only the update created by this Saga.
                    _updates.Remove(command.SagaId);

                    if (_channels.TryGetValue(
                        command.ChannelId,
                        out var channel))
                    {
                        // Revert the simplified unread counter.
                        channel.UnreadCount =
                            Math.Max(0, channel.UnreadCount - 1);

                        // Recalculate the latest remaining activity.
                        DateTime? latestActivity = null;

                        foreach (var update in _updates.Values)
                        {
                            if (update.ChannelId != command.ChannelId)
                                continue;

                            if (latestActivity is null ||
                                update.UpdatedAtUtc > latestActivity.Value)
                            {
                                latestActivity = update.UpdatedAtUtc;
                            }
                        }

                        channel.LastActivityAtUtc = latestActivity;

                        Console.WriteLine(
                            $"COMPENSATION: Channel reverted: {command.ChannelId}");

                        Console.WriteLine(
                            $"Unread count restored: {channel.UnreadCount}");
                    }
                }
            }
            else
            {
                // Reverting an already absent update is successful.
                Console.WriteLine(
                    $"Channel update already absent for Saga: {command.SagaId}");
            }
        }

        // Report unsuccessful compensation.
        if (failureReason is not null)
        {
            await _messageClient.PublishAsync(
                new ChannelActivityRevertFailed
                {
                    SagaId = command.SagaId,
                    Reason = failureReason
                });

            return;
        }

        // Confirm successful compensation.
        await _messageClient.PublishAsync(
            new ChannelActivityReverted
            {
                SagaId = command.SagaId
            });
    }

    // Returns a snapshot of the current channel state.
    public ChannelActivitySnapshot GetChannelSnapshot(Guid channelId)
    {
        lock (_sync)
        {
            if (!_channels.TryGetValue(channelId, out var channel))
            {
                return new ChannelActivitySnapshot(
                    channelId,
                    0,
                    null
                );
            }

            return new ChannelActivitySnapshot(
                channelId,
                channel.UnreadCount,
                channel.LastActivityAtUtc
            );
        }
    }

    // Represents the current state of a channel.
    private sealed class ChannelState
    {
        public long UnreadCount { get; set; }

        public DateTime? LastActivityAtUtc { get; set; }
    }

    // Identifies a channel update created by a Saga.
    private sealed record ChannelUpdateRecord(
        Guid ChannelId,
        Guid MessageId,
        DateTime UpdatedAtUtc
    );
}

// Exposes channel information for tests.
public sealed record ChannelActivitySnapshot(
    Guid ChannelId,
    long UnreadCount,
    DateTime? LastActivityAtUtc
);
