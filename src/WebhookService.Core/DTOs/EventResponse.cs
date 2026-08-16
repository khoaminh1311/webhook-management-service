namespace WebhookService.Core.DTOs;

public class EventResponse
{
    public Guid Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int MatchedWebhooksCount { get; set; }
}
