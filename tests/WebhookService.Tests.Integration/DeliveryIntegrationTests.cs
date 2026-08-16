using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Tests.Integration;

public class DeliveryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DeliveryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateEvent_WithMatchingWebhook_SuccessfullyDeliversToMockEndpoint()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // 1. Register a user and get JWT
        var registerRequest = new RegisterRequest
        {
            Email = $"delivery_test_{Guid.NewGuid()}@example.com",
            Password = "Password123!"
        };
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        
        var loginRequest = new LoginRequest { Email = registerRequest.Email, Password = registerRequest.Password };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var token = loginResult!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Start a real HttpListener to act as the webhook endpoint
        var port = 50505 + new Random().Next(1000); // Avoid port conflicts
        var mockEndpointUrl = $"http://localhost:{port}/webhook/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(mockEndpointUrl);
        listener.Start();
        
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = 200;
            context.Response.Close();
        });

        var createWebhookRequest = new CreateWebhookRequest
        {
            Name = "Integration Test Webhook",
            Url = mockEndpointUrl,
            EventTypes = new List<string> { "delivery.test.event" }
        };
        var webhookResponse = await client.PostAsJsonAsync("/api/webhooks", createWebhookRequest);
        webhookResponse.EnsureSuccessStatusCode();
        var webhookResult = await webhookResponse.Content.ReadFromJsonAsync<WebhookResponse>();

        // 3. Create the event
        var createEventRequest = new CreateEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "delivery.test.event",
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}")
        };

        // Act
        var eventResponse = await client.PostAsJsonAsync("/api/events", createEventRequest);
        eventResponse.EnsureSuccessStatusCode();
        var eventResult = await eventResponse.Content.ReadFromJsonAsync<EventResponse>();

        // Wait for the background worker to process the queue (up to 5 seconds)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookServiceDbContext>();
        
        List<DeliveryAttempt> attempts = new();
        for (int i = 0; i < 50; i++)
        {
            attempts = await dbContext.DeliveryAttempts
                .Where(a => a.EventId == eventResult!.Id && a.WebhookId == webhookResult!.Id)
                .ToListAsync();

            if (attempts.Any())
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.Single(attempts);
        var attempt = attempts.First();
        
        Assert.True(attempt.IsSuccessful);
        Assert.Equal(200, attempt.HttpStatusCode);
        Assert.Equal(1, attempt.AttemptNumber);
    }
}
