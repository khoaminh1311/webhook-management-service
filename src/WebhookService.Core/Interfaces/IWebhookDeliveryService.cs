using WebhookService.Core.Models;

namespace WebhookService.Core.Interfaces;

public interface IWebhookDeliveryService
{
    Task ProcessDeliveryAsync(DeliveryWorkItem workItem, CancellationToken ct = default);
}
