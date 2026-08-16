namespace WebhookService.Core.Interfaces;

public interface IRetryPolicyService
{
    bool ShouldRetry(int? statusCode, Exception? exception);
    TimeSpan CalculateDelay(int attemptNumber);
}
