using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebhookService.Core.Entities;

namespace WebhookService.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.EventId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(e => new { e.UserId, e.EventId }).IsUnique();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.EventType);

        // Relationship: Event 1:N DeliveryAttempts
        builder.HasMany(e => e.DeliveryAttempts)
            .WithOne(d => d.Event)
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
