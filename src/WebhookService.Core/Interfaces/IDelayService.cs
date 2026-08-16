namespace WebhookService.Core.Interfaces;

public interface IDelayService
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
