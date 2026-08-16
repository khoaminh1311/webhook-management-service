using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebhookService.Core.DTOs;

namespace WebhookService.Tests.Integration;

public class WebhooksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebhooksControllerTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateWebhookRequest
        {
            Name = "My Webhook",
            Url = "https://example.com/webhook",
            EventTypes = new List<string> { "user.created" }
        };
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var webhook = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(webhook);
        Assert.Equal(request.Name, webhook.Name);
        Assert.Equal(request.Url, webhook.Url);
        Assert.Contains("user.created", webhook.EventTypes);
        Assert.True(webhook.IsActive);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateWebhookRequest
        {
            Name = "My Webhook",
            Url = "https://example.com/webhook",
            EventTypes = new List<string> { "user.created" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/webhooks", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidUrl_ReturnsBadRequest()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateWebhookRequest
        {
            Name = "My Webhook",
            Url = "not-a-valid-url",
            EventTypes = new List<string> { "user.created" }
        };
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyEventTypes_ReturnsBadRequest()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateWebhookRequest
        {
            Name = "My Webhook",
            Url = "https://example.com/webhook",
            EventTypes = new List<string>() // Empty
        };
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyUserWebhooks()
    {
        // Arrange
        var user1Token = await AuthenticateUserAsync($"user1-{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync($"user2-{Guid.NewGuid()}@example.com");

        // Create webhook for user 1
        var createReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        createReq1.Content = JsonContent.Create(new CreateWebhookRequest { Name = "U1 Webhook", Url = "https://example.com", EventTypes = new List<string> { "e1" } });
        await _client.SendAsync(createReq1);

        // Create webhook for user 2
        var createReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        createReq2.Content = JsonContent.Create(new CreateWebhookRequest { Name = "U2 Webhook", Url = "https://example.com", EventTypes = new List<string> { "e2" } });
        await _client.SendAsync(createReq2);

        // Act - User 1 lists webhooks
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/webhooks");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var response = await _client.SendAsync(listReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var webhooks = await response.Content.ReadFromJsonAsync<List<WebhookResponse>>();
        Assert.NotNull(webhooks);
        Assert.Single(webhooks);
        Assert.Equal("U1 Webhook", webhooks[0].Name);
    }

    [Fact]
    public async Task GetById_OwnedWebhook_ReturnsWebhook()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "GetMe", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        // Act
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/webhooks/{createdWebhook!.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(getReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var webhook = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.Equal(createdWebhook.Id, webhook!.Id);
    }

    [Fact]
    public async Task GetById_NotOwnedWebhook_ReturnsNotFound()
    {
        // Arrange
        var user1Token = await AuthenticateUserAsync($"user1-{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync($"user2-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "U1", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        // Act - User 2 tries to get User 1's webhook
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/webhooks/{createdWebhook!.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var response = await _client.SendAsync(getReq);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnedWebhook_ReturnsOk()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "Old Name", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/webhooks/{createdWebhook!.Id}");
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateData = new UpdateWebhookRequest
        {
            Name = "New Name",
            Url = "https://example.org",
            EventTypes = new List<string> { "new.event" },
            IsActive = false
        };
        updateReq.Content = JsonContent.Create(updateData);

        // Act
        var response = await _client.SendAsync(updateReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedWebhook = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.Equal("New Name", updatedWebhook!.Name);
        Assert.False(updatedWebhook.IsActive);
    }

    [Fact]
    public async Task Update_NotOwnedWebhook_ReturnsNotFound()
    {
        // Arrange
        var user1Token = await AuthenticateUserAsync($"user1-{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync($"user2-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "U1", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/webhooks/{createdWebhook!.Id}");
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        updateReq.Content = JsonContent.Create(new UpdateWebhookRequest { Name = "Hacked", Url = "https://example.org", EventTypes = new List<string> { "e" }, IsActive = true });

        // Act - User 2 tries to update User 1's webhook
        var response = await _client.SendAsync(updateReq);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnedWebhook_ReturnsNoContent()
    {
        // Arrange
        var token = await AuthenticateUserAsync($"user-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "DeleteMe", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        // Act
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/webhooks/{createdWebhook!.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(delReq);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        // Verify deletion
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/webhooks/{createdWebhook.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResp = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_NotOwnedWebhook_ReturnsNotFound()
    {
        // Arrange
        var user1Token = await AuthenticateUserAsync($"user1-{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync($"user2-{Guid.NewGuid()}@example.com");
        
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        createReq.Content = JsonContent.Create(new CreateWebhookRequest { Name = "U1", Url = "https://example.com", EventTypes = new List<string> { "e" } });
        var createResp = await _client.SendAsync(createReq);
        var createdWebhook = await createResp.Content.ReadFromJsonAsync<WebhookResponse>();

        // Act - User 2 tries to delete User 1's webhook
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/webhooks/{createdWebhook!.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var response = await _client.SendAsync(delReq);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
