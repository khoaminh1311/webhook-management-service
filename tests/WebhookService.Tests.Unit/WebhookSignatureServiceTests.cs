using Microsoft.Extensions.Time.Testing;
using WebhookService.Core.Services;

namespace WebhookService.Tests.Unit;

public class WebhookSignatureServiceTests
{
    [Fact]
    public void GenerateSignature_IsDeterministic()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";

        // Act
        var signature1 = service.GenerateSignature(secret, timestamp, payload);
        var signature2 = service.GenerateSignature(secret, timestamp, payload);

        // Assert
        Assert.NotNull(signature1);
        Assert.Equal(signature1, signature2);
    }

    [Fact]
    public void GenerateSignature_AlteredPayload_ReturnsDifferentSignature()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "secret";
        var timestamp = "1672531200";

        // Act
        var sig1 = service.GenerateSignature(secret, timestamp, "{\"event\":\"test1\"}");
        var sig2 = service.GenerateSignature(secret, timestamp, "{\"event\":\"test2\"}");

        // Assert
        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";
        var validSignature = service.GenerateSignature(secret, timestamp, payload);

        // Act
        var result = service.VerifySignature(secret, timestamp, payload, validSignature);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_ModifiedSignature_ReturnsFalse()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";
        var validSignature = service.GenerateSignature(secret, timestamp, payload);
        var invalidSignature = validSignature.Replace('a', 'b').Replace('0', '1');

        // Act
        var result = service.VerifySignature(secret, timestamp, payload, invalidSignature);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_ModifiedPayload_ReturnsFalse()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";
        var validSignature = service.GenerateSignature(secret, timestamp, payload);

        var modifiedPayload = "{\"event\":\"test-modified\"}";

        // Act
        var result = service.VerifySignature(secret, timestamp, modifiedPayload, validSignature);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_ModifiedTimestamp_ReturnsFalse()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";
        var validSignature = service.GenerateSignature(secret, timestamp, payload);

        var modifiedTimestamp = "1672531201";

        // Act
        var result = service.VerifySignature(secret, modifiedTimestamp, payload, validSignature);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_WrongSecret_ReturnsFalse()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);
        var secret = "my-super-secret-key-123";
        var timestamp = "1672531200";
        var payload = "{\"event\":\"test\"}";
        var validSignature = service.GenerateSignature(secret, timestamp, payload);

        var wrongSecret = "wrong-secret";

        // Act
        var result = service.VerifySignature(wrongSecret, timestamp, payload, validSignature);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTimestampValid_WithinTolerance_ReturnsTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var currentTime = DateTimeOffset.UtcNow;
        fakeTimeProvider.SetUtcNow(currentTime);
        
        var service = new WebhookSignatureService(fakeTimeProvider);
        var timestamp = currentTime.ToUnixTimeSeconds().ToString();

        // Act
        var result = service.IsTimestampValid(timestamp, 300);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTimestampValid_OlderThanTolerance_ReturnsFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var currentTime = DateTimeOffset.UtcNow;
        fakeTimeProvider.SetUtcNow(currentTime);
        
        var service = new WebhookSignatureService(fakeTimeProvider);
        // Timestamp is 301 seconds older than current time
        var staleTimestamp = currentTime.AddSeconds(-301).ToUnixTimeSeconds().ToString();

        // Act
        var result = service.IsTimestampValid(staleTimestamp, 300);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTimestampValid_NewerThanTolerance_ReturnsFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var currentTime = DateTimeOffset.UtcNow;
        fakeTimeProvider.SetUtcNow(currentTime);
        
        var service = new WebhookSignatureService(fakeTimeProvider);
        // Timestamp is 301 seconds into the future
        var futureTimestamp = currentTime.AddSeconds(301).ToUnixTimeSeconds().ToString();

        // Act
        var result = service.IsTimestampValid(futureTimestamp, 300);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTimestampValid_InvalidFormat_ReturnsFalse()
    {
        // Arrange
        var service = new WebhookSignatureService(TimeProvider.System);

        // Act
        var result = service.IsTimestampValid("not-a-number", 300);

        // Assert
        Assert.False(result);
    }
}
