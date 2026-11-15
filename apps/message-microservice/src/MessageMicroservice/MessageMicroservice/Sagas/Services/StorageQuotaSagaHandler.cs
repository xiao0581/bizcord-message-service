
using MessageMicroservice.Messaging;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas.Services;

// Simulates storage quota management for the Saga lab.
public sealed class StorageQuotaSagaHandler
{
    private readonly IMessageClient _messageClient;

    // Each user has 10 MB of storage.
    public const long QuotaLimitBytes = 10L * 1024 * 1024;

    // Tracks storage usage for each user.
    private readonly Dictionary<Guid, long> _usedBytes = new();

    // Tracks reservations by FileId.
    private readonly Dictionary<Guid, StorageReservation> _reservations = new();

    // Protects quota checks and updates from concurrent requests.
    private readonly object _sync = new();

    public StorageQuotaSagaHandler(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    // Handles the command to reserve storage quota.
    public async Task HandleReserveAsync(ReserveStorageQuota command)
    {
        string? rejectionReason = null;

        lock (_sync)
        {
            // Validate the incoming command.
            if (command.SagaId == Guid.Empty ||
                command.UserId == Guid.Empty ||
                command.FileId == Guid.Empty ||
                command.FileSizeBytes <= 0)
            {
                rejectionReason = "Invalid quota reservation request.";
            }
            else if (_reservations.TryGetValue(
                command.FileId,
                out var existing))
            {
                // Repeated commands must not reserve quota twice.
                if (existing.SagaId != command.SagaId ||
                    existing.UserId != command.UserId ||
                    existing.FileSizeBytes != command.FileSizeBytes)
                {
                    rejectionReason =
                        "FileId already belongs to another reservation.";
                }
                else
                {
                    Console.WriteLine(
                        $"Quota already reserved for file: {command.FileId}");
                }
            }
            else
            {
                long used = GetUsedBytesInternal(command.UserId);

                // Reject files exceeding the remaining quota.
                if (command.FileSizeBytes > QuotaLimitBytes - used)
                {
                    rejectionReason = "Storage quota exceeded.";
                }
                else
                {
                    // Reserve the storage space.
                    _usedBytes[command.UserId] =
                        used + command.FileSizeBytes;

                    _reservations.Add(
                        command.FileId,
                        new StorageReservation(
                            command.SagaId,
                            command.UserId,
                            command.FileSizeBytes
                        )
                    );

                    Console.WriteLine(
                        $"Quota reserved: {command.FileSizeBytes} bytes");
                }
            }
        }

        // Send a failure event if the reservation was rejected.
        if (rejectionReason is not null)
        {
            Console.WriteLine(
                $"Quota reservation rejected: {rejectionReason}");

            await _messageClient.PublishAsync(
                new StorageQuotaRejected
                {
                    SagaId = command.SagaId,
                    Reason = rejectionReason
                });

            return;
        }

        // Report successful quota reservation.
        await _messageClient.PublishAsync(
            new StorageQuotaReserved
            {
                SagaId = command.SagaId,
                FileId = command.FileId
            });
    }

    // Handles the compensating command to release reserved quota.
    public async Task HandleReleaseAsync(ReleaseStorageQuota command)
    {
        string? failureReason = null;

        lock (_sync)
        {
            if (command.SagaId == Guid.Empty ||
                command.UserId == Guid.Empty ||
                command.FileId == Guid.Empty ||
                command.FileSizeBytes <= 0)
            {
                failureReason = "Invalid quota release request.";
            }
            else if (_reservations.TryGetValue(
                command.FileId,
                out var reservation))
            {
                // Verify that the reservation belongs to this Saga.
                if (reservation.SagaId != command.SagaId ||
                    reservation.UserId != command.UserId ||
                    reservation.FileSizeBytes != command.FileSizeBytes)
                {
                    failureReason = "Reservation does not match the Saga.";
                }
                else
                {
                    long used = GetUsedBytesInternal(command.UserId);

                    if (used < reservation.FileSizeBytes)
                    {
                        failureReason = "Inconsistent storage usage.";
                    }
                    else
                    {
                        // Restore the reserved storage space.
                        long remainingUsed =
                            used - reservation.FileSizeBytes;

                        _usedBytes[command.UserId] = remainingUsed;

                        // Remove the completed reservation.
                        _reservations.Remove(command.FileId);

                        Console.WriteLine(
                            $"COMPENSATION: Released {reservation.FileSizeBytes} bytes");
                    }
                }
            }
            else
            {
                // Repeated release commands must be safe.
                Console.WriteLine(
                    $"Quota already released for file: {command.FileId}");
            }
        }

        // Report an unsuccessful compensation.
        if (failureReason is not null)
        {
            await _messageClient.PublishAsync(
                new StorageQuotaReleaseFailed
                {
                    SagaId = command.SagaId,
                    Reason = failureReason
                });

            return;
        }

        // Confirm successful compensation.
        await _messageClient.PublishAsync(
            new StorageQuotaReleased
            {
                SagaId = command.SagaId
            });
    }

    // Returns the total reserved storage for a user.
    public long GetUsedBytes(Guid userId)
    {
        lock (_sync)
        {
            return GetUsedBytesInternal(userId);
        }
    }

    // Returns the remaining storage for a user.
    public long GetRemainingBytes(Guid userId)
    {
        lock (_sync)
        {
            return QuotaLimitBytes - GetUsedBytesInternal(userId);
        }
    }

    // Reads storage usage while the lock is held.
    private long GetUsedBytesInternal(Guid userId)
    {
        return _usedBytes.TryGetValue(userId, out long used)
            ? used
            : 0;
    }

    // Represents a storage reservation owned by one Saga.
    private sealed record StorageReservation(
        Guid SagaId,
        Guid UserId,
        long FileSizeBytes
    );
}
