using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebhookService.Core.Entities;

namespace WebhookService.Infrastructure.Data.Configurations;

public class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.ToTable("Webhooks");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .IsRequired();

        // Store EventTypes as a JSON array to avoid a fifth table.
        builder.Property(w => w.EventTypes)
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            )
            .HasColumnType("nvarchar(max)")
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
            ));

        builder.HasIndex(w => w.UserId);

        // Relationship: Webhook 1:N DeliveryAttempts
        builder.HasMany(w => w.DeliveryAttempts)
            .WithOne(d => d.Webhook)
            .HasForeignKey(d => d.WebhookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
