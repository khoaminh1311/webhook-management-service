using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebhookService.Core.DTOs;
using WebhookService.Core.Interfaces;

namespace WebhookService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<ActionResult<WebhookResponse>> Create(CreateWebhookRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var response = await _webhookService.CreateAsync(userId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<WebhookResponse>>> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _webhookService.ListAsync(userId, ct);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WebhookResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var response = await _webhookService.GetAsync(id, userId, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WebhookResponse>> Update(Guid id, UpdateWebhookRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var response = await _webhookService.UpdateAsync(id, userId, request, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            await _webhookService.DeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private Guid GetUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }
        
        throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
    }
}
