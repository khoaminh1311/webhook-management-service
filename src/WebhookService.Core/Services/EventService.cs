using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Exceptions;
using WebhookService.Core.Instrumentation;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;

namespace WebhookService.Core.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IWebhookRepository _webhookRepository;
    private readonly IWebhookDeliveryQueue _queue;
    private readonly ILogger<EventService> _logger;
    private readonly WebhookMetrics _metrics;

    public EventService(
        IEventRepository eventRepository,
        IWebhookRepository webhookRepository,
        IWebhookDeliveryQueue queue,
        ILogger<EventService> logger,
        WebhookMetrics metrics)
    {
        _eventRepository = eventRepository;
        _webhookRepository = webhookRepository;
        _queue = queue;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<EventResponse> ProcessEventAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            throw new ArgumentException("EventId is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new ArgumentException("EventType is required and cannot be empty.");
        }

        _metrics.RecordEventReceived();
        _logger.LogInformation(
            "Event received. ClientEventId: {ClientEventId}, EventType: {EventType}",
            request.EventId, request.EventType);

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventId = request.EventId,
            EventType = request.EventType,
            Payload = JsonSerializer.Serialize(request.Payload),
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _eventRepository.AddAsync(evt, ct);
        }
        catch (DuplicateEventException)
        {
            _metrics.RecordEventDuplicate();
            _logger.LogWarning(
                "Duplicate event rejected. ClientEventId: {ClientEventId}, EventType: {EventType}",
                request.EventId, request.EventType);
            throw;
        }

        _logger.LogInformation(
            "Event persisted. EventId: {EventId}, ClientEventId: {ClientEventId}, EventType: {EventType}",
            evt.Id, evt.EventId, evt.EventType);

        // Match active webhooks for this user and event type
        var matchedWebhooks = await _webhookRepository.GetActiveByUserIdAndEventTypeAsync(userId, request.EventType, ct);

        if (matchedWebhooks.Any())
        {
            var workItem = new DeliveryWorkItem
            {
                EventId = evt.Id,
                ClientEventId = evt.EventId,
                EventType = evt.EventType,
                Payload = evt.Payload,
                TargetWebhookIds = matchedWebhooks.Select(w => w.Id).ToList(),
                CorrelationId = Guid.NewGuid()
            };

            await _queue.EnqueueAsync(workItem, ct);

            _logger.LogInformation(
                "Work item enqueued. EventId: {EventId}, CorrelationId: {CorrelationId}, WebhookCount: {WebhookCount}",
                evt.Id, workItem.CorrelationId, matchedWebhooks.Count);
        }

        return new EventResponse
        {
            Id = evt.Id,
            EventId = evt.EventId,
            EventType = evt.EventType,
            CreatedAt = evt.CreatedAt,
            MatchedWebhooksCount = matchedWebhooks.Count
        };
    }
}
