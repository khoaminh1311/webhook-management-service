using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IWebhookRepository
{
    Task<Webhook?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Webhook?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<List<Webhook>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Webhook>> GetActiveByUserIdAndEventTypeAsync(Guid userId, string eventType, CancellationToken ct = default);
    Task AddAsync(Webhook webhook, CancellationToken ct = default);
    Task UpdateAsync(Webhook webhook, CancellationToken ct = default);
    Task DeleteAsync(Webhook webhook, CancellationToken ct = default);
}
