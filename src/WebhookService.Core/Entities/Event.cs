namespace WebhookService.Core.Entities;

/// <summary>
/// Represents a triggered event that may be delivered to one or more webhooks.
/// The Event ID also serves as the idempotency key.
/// </summary>
public class Event
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; set; } = new List<DeliveryAttempt>();
}
