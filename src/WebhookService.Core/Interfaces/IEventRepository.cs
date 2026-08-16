using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IEventRepository
{
    Task AddAsync(Event evt, CancellationToken ct = default);
}
