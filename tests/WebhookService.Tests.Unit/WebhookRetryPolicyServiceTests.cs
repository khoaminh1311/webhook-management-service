using Microsoft.Extensions.Options;
using WebhookService.Core.Services;
using WebhookService.Core.Settings;

namespace WebhookService.Tests.Unit;

public class WebhookRetryPolicyServiceTests
{
    private readonly WebhookRetryPolicyService _service;

    public WebhookRetryPolicyServiceTests()
    {
        var options = Options.Create(new RetrySettings
        {
            InitialDelaySeconds = 2,
            MaxAttempts = 3
        });
        _service = new WebhookRetryPolicyService(options);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void CalculateDelay_ReturnsExponentialBackoff(int attempt, int expectedSeconds)
    {
        // Act
        var delay = _service.CalculateDelay(attempt);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public void ShouldRetry_With2xxOr4xxStatusCode_ReturnsFalse(int statusCode)
    {
        // Act
        var result = _service.ShouldRetry(statusCode, null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void ShouldRetry_With5xxStatusCode_ReturnsTrue(int statusCode)
    {
        // Act
        var result = _service.ShouldRetry(statusCode, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetry_WithHttpRequestException_ReturnsTrue()
    {
        // Act
        var result = _service.ShouldRetry(null, new HttpRequestException("Network error"));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetry_WithTaskCanceledException_ReturnsTrue()
    {
        // Act
        var result = _service.ShouldRetry(null, new TaskCanceledException());

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRetry_WithCancelledToken_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ex = new OperationCanceledException(cts.Token);

        // Act
        var result = _service.ShouldRetry(null, ex);

        // Assert
        Assert.False(result);
    }
}
