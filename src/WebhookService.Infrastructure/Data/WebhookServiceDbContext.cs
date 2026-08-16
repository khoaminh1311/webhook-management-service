using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WebhookService.Core.Entities;

namespace WebhookService.Infrastructure.Data;

public class WebhookServiceDbContext : DbContext
{
    public WebhookServiceDbContext(DbContextOptions<WebhookServiceDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
