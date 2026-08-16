using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebhookService.Core.Entities;
using WebhookService.Core.Exceptions;
using WebhookService.Core.Interfaces;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Infrastructure.Repositories;

public class EventRepository : IEventRepository
{
    private readonly WebhookServiceDbContext _dbContext;

    public EventRepository(WebhookServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Event evt, CancellationToken ct = default)
    {
        _dbContext.Events.Add(evt);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            throw new DuplicateEventException($"Event with ID {evt.EventId} already exists.", ex);
        }
    }
}
