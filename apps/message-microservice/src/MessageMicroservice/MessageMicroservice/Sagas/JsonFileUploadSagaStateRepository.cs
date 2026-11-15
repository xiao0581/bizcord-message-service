
using System.Text.Json;

namespace MessageMicroservice.Sagas;

// Persists Saga state as JSON files for the local lab.
public sealed class JsonFileUploadSagaStateRepository
    : IFileUploadSagaStateRepository
{
    private readonly string _folderPath;

    // Serializes file operations within this repository instance.
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        Directory.CreateDirectory(_folderPath);
    }

    // Loads a previously saved Saga state.
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
            Directory.CreateDirectory(_folderPath);

            string filePath = GetFilePath(state.SagaId);

            // Write to a temporary file before replacing the state file.
            string temporaryPath =
                filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                string json = JsonSerializer.Serialize(
                    state,
                    JsonOptions
                );

                await File.WriteAllTextAsync(
                    temporaryPath,
                    json,
                    cancellationToken
                );

                File.Move(
                    temporaryPath,
                    filePath,
                    overwrite: true
                );
            }
            finally
            {
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

    // Returns the JSON file path for a specific Saga.
    private string GetFilePath(Guid sagaId)
    {
        return Path.Combine(
            _folderPath,
            $"{sagaId:N}.json"
        );
    }
}
