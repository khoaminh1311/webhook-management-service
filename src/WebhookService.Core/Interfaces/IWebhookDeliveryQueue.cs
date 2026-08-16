using WebhookService.Core.Models;

namespace WebhookService.Core.Interfaces;

public interface IWebhookDeliveryQueue
{
    ValueTask EnqueueAsync(DeliveryWorkItem workItem, CancellationToken ct = default);
    ValueTask<DeliveryWorkItem> DequeueAsync(CancellationToken ct = default);
}
