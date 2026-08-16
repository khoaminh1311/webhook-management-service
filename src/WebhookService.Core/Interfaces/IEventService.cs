using WebhookService.Core.DTOs;

namespace WebhookService.Core.Interfaces;

public interface IEventService
{
    Task<EventResponse> ProcessEventAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default);
}
