using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebhookService.Core.Entities;

namespace WebhookService.Infrastructure.Data.Configurations;

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("DeliveryAttempts");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AttemptNumber)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.HttpStatusCode);

        builder.Property(d => d.ResponseBody)
            .HasMaxLength(4000);

        builder.Property(d => d.DurationMs)
            .IsRequired();

        builder.Property(d => d.AttemptedAt)
            .IsRequired();

        builder.Property(d => d.IsSuccessful)
            .IsRequired();

        builder.Property(d => d.IsDeadLettered)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(d => d.EventId);

        builder.HasIndex(d => d.WebhookId);

        // Composite index for looking up delivery attempts by Event + Webhook pair
        builder.HasIndex(d => new { d.EventId, d.WebhookId });
    }
}
