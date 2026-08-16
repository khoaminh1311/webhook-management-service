using Microsoft.EntityFrameworkCore;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Repositories;

public class WebhookRepository : IWebhookRepository
{
    private readonly WebhookServiceDbContext _dbContext;

    public WebhookRepository(WebhookServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Webhook?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Webhooks.FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Webhook?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Webhooks
            .SingleOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
    }

    public async Task<List<Webhook>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Webhooks
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Webhook>> GetActiveByUserIdAndEventTypeAsync(Guid userId, string eventType, CancellationToken ct = default)
    {
        // JSON collection querying in EF Core
        var allUserWebhooks = await _dbContext.Webhooks
            .Where(w => w.UserId == userId && w.IsActive)
            .ToListAsync(ct);

        return allUserWebhooks
            .Where(w => w.EventTypes.Contains(eventType))
            .ToList();
    }

    public async Task AddAsync(Webhook webhook, CancellationToken ct = default)
    {
        _dbContext.Webhooks.Add(webhook);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Webhook webhook, CancellationToken ct = default)
    {
        _dbContext.Webhooks.Update(webhook);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Webhook webhook, CancellationToken ct = default)
    {
        _dbContext.Webhooks.Remove(webhook);
        await _dbContext.SaveChangesAsync(ct);
    }
}
