using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebhookService.Core.DTOs;

namespace WebhookService.Tests.Integration;

public class EventsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> AuthenticateUserAsync(string email)
    {
        var request = new RegisterRequest { Email = email, Password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        var loginRequest = new LoginRequest { Email = email, Password = "Password123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginData!.AccessToken;
    }

    private async Task CreateWebhookAsync(string token, string name, string url, List<string> eventTypes, bool isActive = true)
    {
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var webhookData = new CreateWebhookRequest { Name = name, Url = url, EventTypes = eventTypes };
        createReq.Content = JsonContent.Create(webhookData);
        var response = await _client.SendAsync(createReq);
        var webhook = await response.Content.ReadFromJsonAsync<WebhookResponse>();

        if (!isActive && webhook != null)
        {
            var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/webhooks/{webhook.Id}");
            updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var updateData = new UpdateWebhookRequest
            {
                Name = name,
                Url = url,
                EventTypes = eventTypes,
                IsActive = false
            };
            updateReq.Content = JsonContent.Create(updateData);
            await _client.SendAsync(updateReq);
        }
    }

    [Fact]
    public async Task CreateEvent_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.created",
            Payload = JsonDocument.Parse("{\"orderId\": 123}").RootElement
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "", // Invalid
            Payload = JsonDocument.Parse("{\"orderId\": 123}").RootElement
        };
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ValidEvent_PersistsAndMatchesCorrectWebhooks()
    {
        // Arrange
        var user1Token = await AuthenticateUserAsync($"user1-{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync($"user2-{Guid.NewGuid()}@example.com");

        // User 1 webhooks
        await CreateWebhookAsync(user1Token, "U1-WH1", "https://example.com/1", new List<string> { "order.created", "order.updated" }); // Match
        await CreateWebhookAsync(user1Token, "U1-WH2", "https://example.com/2", new List<string> { "order.deleted" }); // No match
        await CreateWebhookAsync(user1Token, "U1-WH3", "https://example.com/3", new List<string> { "order.created" }, isActive: false); // Inactive - no match

        // User 2 webhooks
        await CreateWebhookAsync(user2Token, "U2-WH1", "https://example.com/4", new List<string> { "order.created" }); // Different user - no match

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var request = new CreateEventRequest
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "order.created",
            Payload = JsonDocument.Parse("{\"orderId\": 123}").RootElement
        };
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var eventResponse = await response.Content.ReadFromJsonAsync<EventResponse>();
        
        Assert.NotNull(eventResponse);
        Assert.NotEqual(Guid.Empty, eventResponse.Id);
        Assert.Equal("order.created", eventResponse.EventType);
        // Only U1-WH1 should match (U1-WH2 wrong event, U1-WH3 inactive, U2-WH1 wrong user)
        Assert.Equal(1, eventResponse.MatchedWebhooksCount);
    }
}
