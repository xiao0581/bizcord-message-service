
using Microsoft.AspNetCore.Mvc;
using MessageMicroservice.Messaging;
using MessageMicroservice.Sagas;
using SharedModels.Sagas;

namespace MessageMicroservice.Controllers;

[ApiController]
[Route("api/sagas/file-upload")]
public sealed class FileUploadSagaController : ControllerBase
{
    private readonly IMessageClient _messageClient;
    private readonly IFileUploadSagaStateRepository _sagaRepository;

    public FileUploadSagaController(
        IMessageClient messageClient,
        IFileUploadSagaStateRepository sagaRepository)
    {
        _messageClient = messageClient;
        _sagaRepository = sagaRepository;
    }

    // Starts a new file upload Saga.
    [HttpPost]
    public async Task<IActionResult> Start(
        [FromBody] StartFileUploadRequest request)
    {
        // Validate the incoming request.
        if (request.UserId == Guid.Empty)
        {
            return BadRequest("UserId is required.");
        }

        if (request.ChannelId == Guid.Empty)
        {
            return BadRequest("ChannelId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("FileName is required.");
        }

        if (request.FileSizeBytes <= 0)
        {
            return BadRequest(
                "FileSizeBytes must be greater than zero.");
        }

        // Generate identifiers for this workflow.
        Guid sagaId = Guid.NewGuid();
        Guid fileId = Guid.NewGuid();

        // Create the command that starts the Saga.
        var command = new StartFileUploadSaga
        {
            SagaId = sagaId,
            UserId = request.UserId,
            ChannelId = request.ChannelId,
            FileId = fileId,
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            Content = request.Content ?? string.Empty
        };

        // Publish the command through RabbitMQ.
        // The Saga Orchestrator processes the command asynchronously.
        await _messageClient.PublishAsync(command);

        Console.WriteLine(
            $"File upload Saga start command published: {sagaId}");

        // Return 202 because the workflow is asynchronous.
        return AcceptedAtAction(
            nameof(GetStatus),
            new { sagaId },
            new
            {
                SagaId = sagaId,
                FileId = fileId,
                Status = "Accepted",
                Message = "Saga start command published."
            }
        );
    }

    // Returns the current state of a Saga.
    [HttpGet("{sagaId:guid}")]
    public async Task<IActionResult> GetStatus(Guid sagaId)
    {
        if (sagaId == Guid.Empty)
        {
            return BadRequest("SagaId is required.");
        }

        // Load the persisted Saga state.
        var state = await _sagaRepository.GetByIdAsync(sagaId);

        if (state is null)
        {
            return NotFound(new
            {
                SagaId = sagaId,
                Message = "Saga state not found."
            });
        }

        // Return the workflow status and compensation progress.
        return Ok(new
        {
            state.SagaId,
            state.UserId,
            state.ChannelId,
            state.FileId,
            state.MessageId,
            state.FileName,
            state.FileSizeBytes,

            Status = state.Status.ToString(),
            state.CurrentStep,

            state.FileStored,
            state.QuotaReserved,
            state.MessageCreated,
            state.ChannelUpdated,

            state.FileDeleted,
            state.QuotaReleased,
            state.MessageRetracted,
            state.ChannelReverted,

            state.LastError,
            state.StartedAtUtc,
            state.CompletedAtUtc
        });
    }
}

// Request model for starting a file upload Saga.
public sealed class StartFileUploadRequest
{
    public Guid UserId { get; set; }

    public Guid ChannelId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string? Content { get; set; }
}
