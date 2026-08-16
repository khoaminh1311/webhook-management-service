using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IDeliveryAttemptRepository
{
    Task AddAsync(DeliveryAttempt attempt, CancellationToken ct = default);
}
