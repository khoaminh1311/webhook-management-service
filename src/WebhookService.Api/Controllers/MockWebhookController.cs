using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Settings;

namespace WebhookService.Api.Controllers;

/// <summary>
/// Development/Testing endpoint only. Not for production use.
/// </summary>
[ApiController]
[Route("api/mock-webhook/{webhookId:guid}")]
public class MockWebhookController : ControllerBase
{
    private readonly IWebhookRepository _webhookRepository;
    private readonly IWebhookSignatureService _signatureService;
    private readonly WebhookSecuritySettings _securitySettings;
    private readonly ILogger<MockWebhookController> _logger;

    public MockWebhookController(
        IWebhookRepository webhookRepository,
        IWebhookSignatureService signatureService,
        IOptions<WebhookSecuritySettings> securitySettings,
        ILogger<MockWebhookController> logger)
    {
        _webhookRepository = webhookRepository;
        _signatureService = signatureService;
        _securitySettings = securitySettings.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook(Guid webhookId)
    {
        var webhook = await _webhookRepository.GetByIdAsync(webhookId, HttpContext.RequestAborted);
        if (webhook == null)
        {
            return NotFound();
        }

        var timestamp = Request.Headers["X-Webhook-Timestamp"].ToString();
        var signature = Request.Headers["X-Webhook-Signature"].ToString();

        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing signature or timestamp headers for Webhook {WebhookId}", webhookId);
            return Unauthorized(new { message = "Missing signature or timestamp." });
        }

        if (!_signatureService.IsTimestampValid(timestamp, _securitySettings.TimestampToleranceSeconds))
        {
            _logger.LogWarning("Stale or invalid timestamp {Timestamp} for Webhook {WebhookId}", timestamp, webhookId);
            return Unauthorized(new { message = "Stale or invalid timestamp." });
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();

        if (!_signatureService.VerifySignature(webhook.Secret, timestamp, payload, signature))
        {
            _logger.LogWarning("Invalid signature for Webhook {WebhookId}", webhookId);
            return Unauthorized(new { message = "Invalid signature." });
        }

        var eventType = Request.Headers["X-Webhook-Event"].ToString();
        var eventId = Request.Headers["X-Event-Id"].ToString();
        var correlationId = Request.Headers["X-Correlation-Id"].ToString();

        _logger.LogInformation(
            "Mock webhook received successfully. EventType: {EventType}, EventId: {EventId}, CorrelationId: {CorrelationId}",
            eventType, eventId, correlationId);

        return Ok(new { message = "Webhook received successfully." });
    }
}
