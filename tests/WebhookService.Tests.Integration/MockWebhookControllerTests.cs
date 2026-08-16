using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Settings;

namespace WebhookService.Tests.Integration;

public class MockWebhookControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MockWebhookControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid webhookId, string secret)> SetupWebhookAsync()
    {
        var client = _factory.CreateClient();
        
        var registerRequest = new RegisterRequest
        {
            Email = $"mocktest_{Guid.NewGuid()}@example.com",
            Password = "Password123!"
        };
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registerRequest.Email, Password = registerRequest.Password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        var createWebhookRequest = new CreateWebhookRequest
        {
            Name = "Test Webhook",
            Url = "http://localhost/api/mock-webhook", // not actually used in this test since we hit the endpoint directly
            EventTypes = new List<string> { "test.event" }
        };
        
        var webhookResponse = await client.PostAsJsonAsync("/api/webhooks", createWebhookRequest);
        webhookResponse.EnsureSuccessStatusCode();
        var webhookResult = await webhookResponse.Content.ReadFromJsonAsync<WebhookResponse>();
        
        // We need the secret, but it's not returned by the API for security.
        // So we grab it from the database directly for the test.
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var webhook = await repo.GetByIdAsync(webhookResult!.Id, CancellationToken.None);

        return (client, webhook!.Id, webhook.Secret);
    }

    [Fact]
    public async Task ReceiveWebhook_ValidSignatureAndTimestamp_ReturnsOk()
    {
        // Arrange
        var (client, webhookId, secret) = await SetupWebhookAsync();
        
        using var scope = _factory.Services.CreateScope();
        var signatureService = scope.ServiceProvider.GetRequiredService<IWebhookSignatureService>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var payload = "{\"data\":\"test\"}";
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();
        var signature = signatureService.GenerateSignature(secret, timestamp, payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mock-webhook/{webhookId}");
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature", signature);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingSignature_ReturnsUnauthorized()
    {
        // Arrange
        var (client, webhookId, _) = await SetupWebhookAsync();
        
        using var scope = _factory.Services.CreateScope();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var payload = "{\"data\":\"test\"}";
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mock-webhook/{webhookId}");
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        // Missing signature

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        var (client, webhookId, secret) = await SetupWebhookAsync();
        
        using var scope = _factory.Services.CreateScope();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var payload = "{\"data\":\"test\"}";
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mock-webhook/{webhookId}");
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature", "invalid-signature-1234");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_StaleTimestamp_ReturnsUnauthorized()
    {
        // Arrange
        var (client, webhookId, secret) = await SetupWebhookAsync();
        
        using var scope = _factory.Services.CreateScope();
        var signatureService = scope.ServiceProvider.GetRequiredService<IWebhookSignatureService>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<WebhookSecuritySettings>>().Value;

        var payload = "{\"data\":\"test\"}";
        
        // Create a timestamp that is just outside the tolerance window
        var staleTimestamp = timeProvider.GetUtcNow().AddSeconds(-(settings.TimestampToleranceSeconds + 10)).ToUnixTimeSeconds().ToString();
        var signature = signatureService.GenerateSignature(secret, staleTimestamp, payload);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/mock-webhook/{webhookId}");
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("X-Webhook-Timestamp", staleTimestamp);
        request.Headers.Add("X-Webhook-Signature", signature);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
