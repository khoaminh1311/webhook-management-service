using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebhookService.Core.Entities;
using WebhookService.Core.Instrumentation;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Core.Settings;

namespace WebhookService.Core.Services;

public class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookRepository _webhookRepository;
    private readonly IDeliveryAttemptRepository _deliveryAttemptRepository;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IDelayService _delayService;
    private readonly IWebhookSignatureService _signatureService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private readonly WebhookMetrics _metrics;
    private readonly RetrySettings _retrySettings;

    public WebhookDeliveryService(
        IHttpClientFactory httpClientFactory,
        IWebhookRepository webhookRepository,
        IDeliveryAttemptRepository deliveryAttemptRepository,
        IRetryPolicyService retryPolicyService,
        IDelayService delayService,
        IWebhookSignatureService signatureService,
        TimeProvider timeProvider,
        IOptions<RetrySettings> retrySettings,
        ILogger<WebhookDeliveryService> logger,
        WebhookMetrics metrics)
    {
        _httpClientFactory = httpClientFactory;
        _webhookRepository = webhookRepository;
        _deliveryAttemptRepository = deliveryAttemptRepository;
        _retryPolicyService = retryPolicyService;
        _delayService = delayService;
        _signatureService = signatureService;
        _timeProvider = timeProvider;
        _logger = logger;
        _metrics = metrics;
        _retrySettings = retrySettings.Value;
    }

    public async Task ProcessDeliveryAsync(DeliveryWorkItem workItem, CancellationToken ct = default)
    {
        foreach (var webhookId in workItem.TargetWebhookIds)
        {
            try
            {
                await DeliverToWebhookAsync(webhookId, workItem, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error processing delivery. WebhookId: {WebhookId}, EventId: {EventId}, CorrelationId: {CorrelationId}",
                    webhookId, workItem.EventId, workItem.CorrelationId);
            }
        }
    }

    private async Task DeliverToWebhookAsync(Guid webhookId, DeliveryWorkItem workItem, CancellationToken ct)
    {
        var webhook = await _webhookRepository.GetByIdAsync(webhookId, ct);
        if (webhook == null)
        {
            _logger.LogWarning("Webhook {WebhookId} not found during delivery processing.", webhookId);
            return;
        }

        var httpClient = _httpClientFactory.CreateClient("WebhookClient");
        var maxAttempts = _retrySettings.MaxAttempts;
        var attemptNumber = 1;

        while (attemptNumber <= maxAttempts)
        {
            _metrics.RecordDeliveryAttempt();

            _logger.LogInformation(
                "Delivery started. WebhookId: {WebhookId}, EventId: {EventId}, ClientEventId: {ClientEventId}, " +
                "EventType: {EventType}, AttemptNumber: {AttemptNumber}, CorrelationId: {CorrelationId}",
                webhookId, workItem.EventId, workItem.ClientEventId,
                workItem.EventType, attemptNumber, workItem.CorrelationId);

            var attempt = new DeliveryAttempt
            {
                Id = Guid.NewGuid(),
                WebhookId = webhookId,
                EventId = workItem.EventId,
                AttemptNumber = attemptNumber,
                AttemptedAt = DateTimeOffset.UtcNow,
                IsDeadLettered = false
            };

            var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();
            var signature = _signatureService.GenerateSignature(webhook.Secret, timestamp, workItem.Payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url);
            request.Content = new StringContent(workItem.Payload, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Event", workItem.EventType);
            request.Headers.Add("X-Event-Id", workItem.ClientEventId);
            request.Headers.Add("X-Correlation-Id", workItem.CorrelationId.ToString());
            request.Headers.Add("X-Webhook-Timestamp", timestamp);
            request.Headers.Add("X-Webhook-Signature", signature);

            var stopwatch = Stopwatch.StartNew();
            Exception? caughtException = null;

            try
            {
                using var response = await httpClient.SendAsync(request, ct);
                stopwatch.Stop();

                attempt.HttpStatusCode = (int)response.StatusCode;
                attempt.ResponseBody = await response.Content.ReadAsStringAsync(ct);
                attempt.DurationMs = (int)stopwatch.ElapsedMilliseconds;
                attempt.IsSuccessful = response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                caughtException = ex;
                stopwatch.Stop();
                attempt.IsSuccessful = false;
                attempt.ResponseBody = ex.Message;
                attempt.DurationMs = (int)stopwatch.ElapsedMilliseconds;
                _logger.LogWarning(ex,
                    "HTTP request failed. WebhookId: {WebhookId}, EventId: {EventId}, AttemptNumber: {AttemptNumber}, " +
                    "DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                    webhookId, workItem.EventId, attemptNumber, attempt.DurationMs, workItem.CorrelationId);
            }

            _metrics.RecordDeliveryDuration(attempt.DurationMs);

            var shouldRetry = _retryPolicyService.ShouldRetry(attempt.HttpStatusCode, caughtException) && attemptNumber < maxAttempts;

            // Determine if this is a final failed attempt (dead letter)
            if (!attempt.IsSuccessful && !shouldRetry)
            {
                attempt.IsDeadLettered = true;
            }

            await _deliveryAttemptRepository.AddAsync(attempt, ct);

            if (attempt.IsSuccessful)
            {
                _metrics.RecordDeliverySuccess();
                _logger.LogInformation(
                    "Delivery succeeded. WebhookId: {WebhookId}, EventId: {EventId}, ClientEventId: {ClientEventId}, " +
                    "AttemptNumber: {AttemptNumber}, HttpStatusCode: {HttpStatusCode}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                    webhookId, workItem.EventId, workItem.ClientEventId,
                    attemptNumber, attempt.HttpStatusCode, attempt.DurationMs, workItem.CorrelationId);
                break;
            }

            // Failed attempt
            _metrics.RecordDeliveryFailure();
            _logger.LogWarning(
                "Delivery failed. WebhookId: {WebhookId}, EventId: {EventId}, ClientEventId: {ClientEventId}, " +
                "AttemptNumber: {AttemptNumber}, HttpStatusCode: {HttpStatusCode}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                webhookId, workItem.EventId, workItem.ClientEventId,
                attemptNumber, attempt.HttpStatusCode, attempt.DurationMs, workItem.CorrelationId);

            if (shouldRetry)
            {
                _metrics.RecordDeliveryRetry();
                var delay = _retryPolicyService.CalculateDelay(attemptNumber);
                _logger.LogInformation(
                    "Retry scheduled. WebhookId: {WebhookId}, EventId: {EventId}, AttemptNumber: {AttemptNumber}, " +
                    "DelaySeconds: {DelaySeconds}, CorrelationId: {CorrelationId}",
                    webhookId, workItem.EventId, attemptNumber, delay.TotalSeconds, workItem.CorrelationId);

                try
                {
                    await _delayService.DelayAsync(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    // Worker is shutting down during the delay, stop retrying
                    _logger.LogInformation(
                        "Retry delay cancelled due to shutdown. WebhookId: {WebhookId}, EventId: {EventId}, CorrelationId: {CorrelationId}",
                        webhookId, workItem.EventId, workItem.CorrelationId);
                    break;
                }
            }
            else
            {
                _metrics.RecordDeliveryDeadLetter();
                _logger.LogWarning(
                    "Dead-lettered. WebhookId: {WebhookId}, EventId: {EventId}, ClientEventId: {ClientEventId}, " +
                    "AttemptNumber: {AttemptNumber}, CorrelationId: {CorrelationId}",
                    webhookId, workItem.EventId, workItem.ClientEventId,
                    attemptNumber, workItem.CorrelationId);
                break;
            }

            attemptNumber++;
        }
    }
}
