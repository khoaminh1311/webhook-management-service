namespace WebhookService.Core.Entities;

/// <summary>
/// Represents the status of a delivery attempt.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>Delivery is pending (queued or waiting for retry).</summary>
    Pending,

    /// <summary>Delivery succeeded (received 2xx response).</summary>
    Succeeded,

    /// <summary>Delivery failed (non-2xx response, timeout, or connection error).</summary>
    Failed,

    /// <summary>Delivery has exhausted all retry attempts and is dead-lettered.</summary>
    DeadLettered
}
