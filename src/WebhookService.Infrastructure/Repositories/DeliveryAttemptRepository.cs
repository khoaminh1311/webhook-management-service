using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Repositories;

public class DeliveryAttemptRepository : IDeliveryAttemptRepository
{
    private readonly WebhookServiceDbContext _context;

    public DeliveryAttemptRepository(WebhookServiceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeliveryAttempt attempt, CancellationToken ct = default)
    {
        await _context.DeliveryAttempts.AddAsync(attempt, ct);
        await _context.SaveChangesAsync(ct);
    }
}
