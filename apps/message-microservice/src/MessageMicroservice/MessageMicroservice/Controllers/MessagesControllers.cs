using Microsoft.AspNetCore.Mvc;
using MessageMicroservice.Messaging;
using MessageMicroservice.Messages;

namespace MessageMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesControllers : ControllerBase
{
    private readonly IMessageClient _messageClient;

    public MessagesControllers(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Message service is running");
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(MessageSent message)
    {
        await _messageClient.PublishAsync(message);

        return Ok(new
        {
            status = "published",
            message
        });
    }
}