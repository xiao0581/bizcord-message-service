
using System;

namespace SharedModels.Sagas;

// Base class for all Saga commands and events.
public abstract class SagaMessage
{
    public Guid SagaId { get; set; }
}

// ============================================================
// 1. Start the Saga
// ============================================================

public sealed class StartFileUploadSaga : SagaMessage
{
    public Guid UserId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Content { get; set; } = string.Empty;
}

// ============================================================
// 2. File Storage
// ============================================================

// Command: Store a file temporarily.
public sealed class StoreTemporaryFile : SagaMessage
{
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

// Event: Temporary file storage succeeded.
public sealed class TemporaryFileStored : SagaMessage
{
    public Guid FileId { get; set; }
}

// Event: Temporary file storage failed.
public sealed class TemporaryFileFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 3. Storage Quota
// ============================================================

// Command: Reserve storage quota.
public sealed class ReserveStorageQuota : SagaMessage
{
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
    public long FileSizeBytes { get; set; }
}

// Event: Storage quota was reserved.
public sealed class StorageQuotaReserved : SagaMessage
{
    public Guid FileId { get; set; }
}

// Event: Storage quota reservation failed.
public sealed class StorageQuotaRejected : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 4. Message Creation
// ============================================================

// Command: Create a message with a file attachment.
public sealed class CreateAttachmentMessage : SagaMessage
{
    public Guid UserId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid FileId { get; set; }
    public string Content { get; set; } = string.Empty;
}

// Event: Attachment message was created.
public sealed class AttachmentMessageCreated : SagaMessage
{
    public Guid MessageId { get; set; }
}

// Event: Attachment message creation failed.
public sealed class AttachmentMessageFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 5. Channel Activity
// ============================================================

// Command: Update channel activity and unread counters.
public sealed class UpdateChannelActivity : SagaMessage
{
    public Guid ChannelId { get; set; }
    public Guid MessageId { get; set; }
}

// Event: Channel activity was updated.
public sealed class ChannelActivityUpdated : SagaMessage
{
}

// Event: Channel activity update failed.
public sealed class ChannelActivityFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 6. Message Compensation
// ============================================================

// Command: Retract a previously created message.
public sealed class RetractAttachmentMessage : SagaMessage
{
    public Guid MessageId { get; set; }
}

// Event: Message retraction succeeded.
public sealed class AttachmentMessageRetracted : SagaMessage
{
}

// Event: Message retraction failed.
public sealed class AttachmentRetractionFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 7. Storage Quota Compensation
// ============================================================

// Command: Release previously reserved quota.
public sealed class ReleaseStorageQuota : SagaMessage
{
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
    public long FileSizeBytes { get; set; }
}

// Event: Storage quota was released.
public sealed class StorageQuotaReleased : SagaMessage
{
}

// Event: Storage quota release failed.
public sealed class StorageQuotaReleaseFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 8. File Compensation
// ============================================================

// Command: Delete a temporarily stored file.
public sealed class DeleteTemporaryFile : SagaMessage
{
    public Guid FileId { get; set; }
}

// Event: Temporary file deletion succeeded.
public sealed class TemporaryFileDeleted : SagaMessage
{
}

// Event: Temporary file deletion failed.
public sealed class TemporaryFileDeletionFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 9. Channel Compensation
// ============================================================

// Command: Revert or recalculate channel activity.
public sealed class RevertChannelActivity : SagaMessage
{
    public Guid ChannelId { get; set; }
    public Guid MessageId { get; set; }
}

// Event: Channel activity compensation succeeded.
public sealed class ChannelActivityReverted : SagaMessage
{
}

// Event: Channel activity compensation failed.
public sealed class ChannelActivityRevertFailed : SagaMessage
{
    public string Reason { get; set; } = string.Empty;
}
