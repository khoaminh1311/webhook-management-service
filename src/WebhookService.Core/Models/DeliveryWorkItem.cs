namespace WebhookService.Core.Models;

public class DeliveryWorkItem
{
    public Guid EventId { get; set; }
    
    public string ClientEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public List<Guid> TargetWebhookIds { get; set; } = new List<Guid>();
    public Guid CorrelationId { get; set; }
}
