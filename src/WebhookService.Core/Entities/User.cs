namespace WebhookService.Core.Entities;

/// <summary>
/// Represents a registered user who can manage webhooks and trigger events.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
}
