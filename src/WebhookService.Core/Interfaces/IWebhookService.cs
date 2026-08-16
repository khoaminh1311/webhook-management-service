using WebhookService.Core.DTOs;

namespace WebhookService.Core.Interfaces;

public interface IWebhookService
{
    Task<WebhookResponse> CreateAsync(Guid userId, CreateWebhookRequest request, CancellationToken ct = default);
    Task<List<WebhookResponse>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<WebhookResponse> GetAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<WebhookResponse> UpdateAsync(Guid id, Guid userId, UpdateWebhookRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
