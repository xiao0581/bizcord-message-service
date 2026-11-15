
namespace MessageMicroservice.Sagas;

// Represents the current lifecycle status of a file upload Saga.
public enum FileUploadSagaStatus
{
    Started,
    Running,
    Compensating,
    Completed,
    Compensated,
    CompensationFailed
}

// Stores the progress of a single file upload Saga.
public sealed class FileUploadSagaState
{
    // Saga identity
    public Guid SagaId { get; set; }

    // Business information
    public Guid UserId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid FileId { get; set; }
    public Guid? MessageId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Tracks completed business transactions
    public bool FileStored { get; set; }
    public bool QuotaReserved { get; set; }
    public bool MessageCreated { get; set; }
    public bool ChannelUpdated { get; set; }

    // Tracks compensation progress
    public bool FileDeleted { get; set; }
    public bool QuotaReleased { get; set; }
    public bool MessageRetracted { get; set; }
    public bool ChannelReverted { get; set; }

    // Records whether a channel update was attempted
    public bool ChannelUpdateStarted { get; set; }

    // Current Saga progress
    public FileUploadSagaStatus Status { get; set; }
        = FileUploadSagaStatus.Started;

    public string CurrentStep { get; set; } = "NotStarted";

    public string? LastError { get; set; }

    // Timestamps
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
