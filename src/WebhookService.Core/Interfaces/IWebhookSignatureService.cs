namespace WebhookService.Core.Interfaces;

public interface IWebhookSignatureService
{
    string GenerateSignature(string secret, string timestamp, string payload);
    bool VerifySignature(string secret, string timestamp, string payload, string signature);
    bool IsTimestampValid(string timestamp, int toleranceSeconds);
}
