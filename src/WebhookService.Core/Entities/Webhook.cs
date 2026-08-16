namespace WebhookService.Core.Entities;

/// <summary>
/// Represents a webhook registration that subscribes to specific event types
/// and delivers payloads to a configured URL.
/// </summary>
public class Webhook
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// List of event types this webhook is subscribed to.
    /// Stored as a JSON array in the database to avoid a fifth table.
    /// Example: ["payment.created", "payment.completed"]
    /// </summary>
    public List<string> EventTypes { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;

    public ICollection<DeliveryAttempt> DeliveryAttempts { get; set; } = new List<DeliveryAttempt>();
}
