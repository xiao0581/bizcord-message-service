
namespace MessageMicroservice.Sagas;

// Defines how Saga state is loaded and persisted.
public interface IFileUploadSagaStateRepository
{
    // Loads an existing Saga by its unique identifier.
    Task<FileUploadSagaState?> GetByIdAsync(
        Guid sagaId,
        CancellationToken cancellationToken = default
    );

    // Saves the current state of a Saga.
    Task SaveAsync(
        FileUploadSagaState state,
        CancellationToken cancellationToken = default
    );
}
