
using System.Collections.Concurrent;
using MessageMicroservice.Messaging;
using SharedModels.Sagas;

namespace MessageMicroservice.Sagas.Services;

// Simulates temporary file storage for the Saga lab.
public sealed class FileStorageSagaHandler
{
    private readonly IMessageClient _messageClient;

    // Stores file metadata in memory.
    private readonly ConcurrentDictionary<Guid, StoredFile> _files = new();

    public FileStorageSagaHandler(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    // Handles the command to store a temporary file.
    public async Task HandleStoreAsync(StoreTemporaryFile command)
    {
        try
        {
            if (command.SagaId == Guid.Empty ||
                command.FileId == Guid.Empty ||
                command.FileSizeBytes <= 0 ||
                string.IsNullOrWhiteSpace(command.FileName))
            {
                throw new InvalidOperationException(
                    "Invalid file storage request.");
            }

            var file = new StoredFile(
                command.SagaId,
                command.FileName,
                command.FileSizeBytes
            );

            // Make repeated storage requests idempotent.
            if (!_files.TryAdd(command.FileId, file))
            {
                var existing = _files[command.FileId];

                if (existing != file)
                {
                    throw new InvalidOperationException(
                        "FileId already belongs to another upload.");
                }

                Console.WriteLine(
                    $"File already stored: {command.FileId}");
            }
            else
            {
                Console.WriteLine(
                    $"Temporary file stored: {command.FileName}");
            }
        }
        catch (Exception ex)
        {
            // Report the failure to the Saga.
            await _messageClient.PublishAsync(
                new TemporaryFileFailed
                {
                    SagaId = command.SagaId,
                    Reason = ex.Message
                });

            return;
        }

        // Report successful storage to the Saga.
        await _messageClient.PublishAsync(
            new TemporaryFileStored
            {
                SagaId = command.SagaId,
                FileId = command.FileId
            });
    }

    // Handles the compensating command to delete a file.
    public async Task HandleDeleteAsync(DeleteTemporaryFile command)
    {
        try
        {
            if (command.SagaId == Guid.Empty ||
                command.FileId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Invalid file deletion request.");
            }

            if (_files.TryGetValue(command.FileId, out var file))
            {
                if (file.SagaId != command.SagaId)
                {
                    throw new InvalidOperationException(
                        "The file belongs to another Saga.");
                }

                _files.TryRemove(command.FileId, out _);

                Console.WriteLine(
                    $"COMPENSATION: Temporary file deleted: {command.FileId}");
            }
            else
            {
                // Deleting an already missing file is successful.
                Console.WriteLine(
                    $"Temporary file already absent: {command.FileId}");
            }
        }
        catch (Exception ex)
        {
            await _messageClient.PublishAsync(
                new TemporaryFileDeletionFailed
                {
                    SagaId = command.SagaId,
                    Reason = ex.Message
                });

            return;
        }

        // Confirm that the file no longer exists.
        await _messageClient.PublishAsync(
            new TemporaryFileDeleted
            {
                SagaId = command.SagaId
            });
    }

    // Represents the stored file metadata.
    private sealed record StoredFile(
        Guid SagaId,
        string FileName,
        long FileSizeBytes
    );
}
