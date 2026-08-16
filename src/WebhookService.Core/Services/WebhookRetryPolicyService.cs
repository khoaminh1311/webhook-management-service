using Microsoft.Extensions.Options;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Settings;

namespace WebhookService.Core.Services;

public class WebhookRetryPolicyService : IRetryPolicyService
{
    private readonly RetrySettings _settings;

    public WebhookRetryPolicyService(IOptions<RetrySettings> options)
    {
        _settings = options.Value;
    }

    public bool ShouldRetry(int? statusCode, Exception? exception)
    {
        if (exception is OperationCanceledException opCanceledEx && opCanceledEx.CancellationToken.IsCancellationRequested)
        {
            // The worker itself is being shut down, so we do not retry
            return false;
        }

        if (exception is HttpRequestException || exception is TaskCanceledException)
        {
            // TaskCanceledException without the token being cancelled means an HTTP timeout
            return true;
        }

        if (statusCode.HasValue)
        {
            return statusCode.Value >= 500 && statusCode.Value <= 599;
        }

        return false;
    }

    public TimeSpan CalculateDelay(int attemptNumber)
    {
        // Exponential backoff: InitialDelaySeconds * 2^(attemptNumber - 1)
        var delaySeconds = _settings.InitialDelaySeconds * Math.Pow(2, attemptNumber - 1);
        return TimeSpan.FromSeconds(delaySeconds);
    }
}
