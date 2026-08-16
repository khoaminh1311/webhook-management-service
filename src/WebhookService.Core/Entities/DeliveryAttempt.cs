namespace WebhookService.Core.Entities;

/// <summary>
/// Represents a single attempt to deliver an event payload to a webhook endpoint.
/// Multiple attempts may exist for the same Event/Webhook pair due to retries.
/// </summary>
public class DeliveryAttempt
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid WebhookId { get; set; }

    /// <summary>
    /// The attempt number for this Event/Webhook pair (1-based).
    /// </summary>
    public int AttemptNumber { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>
    /// HTTP status code returned by the webhook endpoint. Null if the request failed before receiving a response.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Truncated response body from the webhook endpoint.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Duration of the HTTP request in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    public DateTimeOffset AttemptedAt { get; set; }

    public bool IsSuccessful { get; set; }

    public bool IsDeadLettered { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;

    public Webhook Webhook { get; set; } = null!;
}
