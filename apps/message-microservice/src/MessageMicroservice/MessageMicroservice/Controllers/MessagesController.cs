using Microsoft.AspNetCore.Mvc;
using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Domain.Entities;
using MessageMicroservice.Messaging;
using MessageMicroservice.Messages;

namespace MessageMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IMessageClient _messageClient;

    public MessagesController(
        IMessageService messageService,
        IMessageClient messageClient)
    {
        _messageService = messageService;
        _messageClient = messageClient;
    }

    // GET: api/messages
    [HttpGet]
    public IActionResult GetAll()
    {
        var messages = _messageService.GetAll();

        return Ok(messages);
    }

    // GET: api/messages/{id}
    [HttpGet("{id}")]
    public IActionResult GetById(Guid id)
    {
        var message = _messageService.GetById(id);

        if (message == null)
        {
            return NotFound();
        }

        return Ok(message);
    }

    // POST: api/messages
    [HttpPost]
    public async Task<IActionResult> Create(Message message)
    {
        var createdMessage = await _messageService.CreateAsync(message);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdMessage.Id },
            createdMessage
        );
    }

    // PUT: api/messages/{id}
    [HttpPut("{id}")]
    public IActionResult Update(Guid id, Message message)
    {
        var updated = _messageService.Update(id, message);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: api/messages/{id}
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        var deleted = _messageService.Delete(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    // POST: api/messages/publish
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(MessageSent message)
    {
        await _messageClient.PublishAsync(message);

        return Ok(new
        {
            status = "published",
            message
        });
    }
}