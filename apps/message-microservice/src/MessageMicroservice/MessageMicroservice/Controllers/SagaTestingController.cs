
using MessageMicroservice.Sagas.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageMicroservice.Controllers;

// Provides development-only endpoints for testing Saga failures.
[ApiController]
[Route("api/sagas/testing")]
public sealed class SagaTestingController : ControllerBase
{
    private readonly ChannelSagaHandler _channelHandler;
    private readonly IHostEnvironment _environment;

    public SagaTestingController(
        ChannelSagaHandler channelHandler,
        IHostEnvironment environment)
    {
        _channelHandler = channelHandler;
        _environment = environment;
    }

    // Simulates failure after the next channel update is applied.
    [HttpPost("fail-next-channel-update")]
    public IActionResult FailNextChannelUpdate()
    {
        // Do not expose failure simulation in production.
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        // The next new channel update will report a failure.
        _channelHandler.SimulateNextUpdateFailure();

        return Ok(new
        {
            Message = "The next channel update will fail after applying changes.",
            SimulationEnabled = true
        });
    }
}
