namespace WebhookService.Core.Settings;

public class RetrySettings
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 2;
}
