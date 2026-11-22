
using System.Text.Json;

namespace MessageMicroservice.Sagas;

// Persists Saga state as JSON files for the local lab.
public sealed class JsonFileUploadSagaStateRepository
    : IFileUploadSagaStateRepository
{
    private readonly string _folderPath;

    // Protects file operations from concurrent access.
    private readonly SemaphoreSlim _lock = new(1, 1);

    // JSON serialization settings.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ============================================================
    // Constructor
    // ============================================================

    public JsonFileUploadSagaStateRepository(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException(
                "The Saga storage path is required.",
                nameof(folderPath)
            );
        }

        _folderPath = folderPath;

        // Ensure the storage directory exists.
        Directory.CreateDirectory(_folderPath);
    }

    // ============================================================
    // Get Saga State
    // ============================================================

    // Loads an existing Saga state from a JSON file.
    public async Task<FileUploadSagaState?> GetByIdAsync(
        Guid sagaId,
        CancellationToken cancellationToken = default)
    {
        if (sagaId == Guid.Empty)
        {
            throw new ArgumentException(
                "SagaId cannot be empty.",
                nameof(sagaId)
            );
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            string filePath = GetFilePath(sagaId);

            if (!File.Exists(filePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(filePath);

            return await JsonSerializer.DeserializeAsync<FileUploadSagaState>(
                stream,
                JsonOptions,
                cancellationToken
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    // ============================================================
    // Save Saga State
    // ============================================================

    // Saves the current Saga state to a JSON file.
    public async Task SaveAsync(
        FileUploadSagaState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.SagaId == Guid.Empty)
        {
            throw new ArgumentException(
                "SagaId cannot be empty.",
                nameof(state)
            );
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            // Ensure the storage directory exists.
            Directory.CreateDirectory(_folderPath);

            string filePath = GetFilePath(state.SagaId);

            // Create a temporary file for safe writing.
            string temporaryPath =
                filePath + "." +
                Guid.NewGuid().ToString("N") +
                ".tmp";

            try
            {
                // Serialize the Saga state to JSON.
                string json = JsonSerializer.Serialize(
                    state,
                    JsonOptions
                );

                // Write the state to the temporary file.
                await File.WriteAllTextAsync(
                    temporaryPath,
                    json,
                    cancellationToken
                );

                // Replace the existing state file.
                File.Move(
                    temporaryPath,
                    filePath,
                    overwrite: true
                );
            }
            finally
            {
                // Remove any remaining temporary file.
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ============================================================
    // File Path
    // ============================================================

    // Returns the JSON file path for a specific Saga.
    private string GetFilePath(Guid sagaId)
    {
        // Explicit namespace avoids the HotChocolate.Path conflict.
        return System.IO.Path.Combine(
            _folderPath,
            $"{sagaId:N}.json"
        );
    }
}
