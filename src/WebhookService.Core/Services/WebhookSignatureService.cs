using System.Security.Cryptography;
using System.Text;
using WebhookService.Core.Interfaces;

namespace WebhookService.Core.Services;

public class WebhookSignatureService : IWebhookSignatureService
{
    private readonly TimeProvider _timeProvider;

    public WebhookSignatureService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string GenerateSignature(string secret, string timestamp, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifySignature(string secret, string timestamp, string payload, string signature)
    {
        var expectedSignature = GenerateSignature(secret, timestamp, payload);
        
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(signature ?? string.Empty);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public bool IsTimestampValid(string timestamp, int toleranceSeconds)
    {
        if (!long.TryParse(timestamp, out var timestampSeconds))
        {
            return false;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        var currentTime = _timeProvider.GetUtcNow();
        var difference = Math.Abs((currentTime - requestTime).TotalSeconds);

        return difference <= toleranceSeconds;
    }
}
