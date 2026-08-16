using WebhookService.Core.Interfaces;

namespace WebhookService.Core.Services;

public class DelayService : IDelayService
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
