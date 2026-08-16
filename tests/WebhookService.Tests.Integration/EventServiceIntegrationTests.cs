using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Tests.Integration;

public class EventServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventServiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> AuthenticateUserAsync(HttpClient client, string email)
    {
        var request = new RegisterRequest { Email = email, Password = "Password123!" };
        await client.PostAsJsonAsync("/api/auth/register", request);

        var loginRequest = new LoginRequest { Email = email, Password = "Password123!" };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginData!.AccessToken;
    }

    private async Task CreateWebhookAsync(HttpClient client, string token, string name, string url, List<string> eventTypes)
    {
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks");
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var webhookData = new CreateWebhookRequest { Name = name, Url = url, EventTypes = eventTypes };
        createReq.Content = JsonContent.Create(webhookData);
        await client.SendAsync(createReq);
    }

    /// <summary>
    /// Creates a test host with FakeEventRepository replacing the real one.
    /// Each call returns an independent FakeEventRepository instance so that
    /// test state does not leak across tests.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWithFakeEventRepository(
        IWebhookDeliveryQueue? mockQueue = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                if (mockQueue != null)
                {
                    services.AddSingleton(mockQueue);
                }

                // Create a single FakeEventRepository instance scoped to this test host.
                // All scoped IEventRepository resolutions share this instance's uniqueness
                // set, preventing cross-test leakage (unlike a static HashSet).
                var fakeRepo = new FakeEventRepository();

                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEventRepository));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                    services.AddScoped<IEventRepository>(provider =>
                    {
                        var dbContext = provider.GetRequiredService<WebhookServiceDbContext>();
                        return fakeRepo.WithInner(
                            new Infrastructure.Repositories.EventRepository(dbContext));
                    });
                }
            });
        });
    }

    [Fact]
    public async Task ProcessEvent_FirstSubmission_Succeeds_And_DuplicateEventIdSameUser_Returns409()
    {
        using var factory = CreateFactoryWithFakeEventRepository();
        var client = factory.CreateClient();
        var email = $"user_{Guid.NewGuid()}@example.com";
        var token = await AuthenticateUserAsync(client, email);
        
        await CreateWebhookAsync(client, token, "Test Webhook", "http://localhost/test", new List<string> { "test.event" });

        var eventId = "idempotency-key-" + Guid.NewGuid().ToString();
        var requestData = new CreateEventRequest
        {
            EventId = eventId,
            EventType = "test.event",
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}")
        };

        // First submission
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req1.Content = JsonContent.Create(requestData);
        var response1 = await client.SendAsync(req1);
        
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Second submission (Duplicate)
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req2.Content = JsonContent.Create(requestData);
        var response2 = await client.SendAsync(req2);
        
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        var responseString = await response2.Content.ReadAsStringAsync();
        Assert.Contains("has already been processed", responseString);
    }

    /// <summary>
    /// Verifies that two different users can submit events with the same EventId.
    /// Uses FakeEventRepository so the (UserId, EventId) composite uniqueness
    /// is actually exercised (EF Core InMemory ignores unique indexes).
    /// </summary>
    [Fact]
    public async Task ProcessEvent_SameEventIdDifferentUser_Succeeds()
    {
        using var factory = CreateFactoryWithFakeEventRepository();
        var client = factory.CreateClient();
        var user1Token = await AuthenticateUserAsync(client, $"user1_{Guid.NewGuid()}@example.com");
        var user2Token = await AuthenticateUserAsync(client, $"user2_{Guid.NewGuid()}@example.com");

        var eventId = "shared-event-id-" + Guid.NewGuid().ToString();
        var requestData = new CreateEventRequest
        {
            EventId = eventId,
            EventType = "test.event",
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}")
        };

        // User 1 submission
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        req1.Content = JsonContent.Create(requestData);
        var response1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // User 2 submission — same EventId, different user — must succeed
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        req2.Content = JsonContent.Create(requestData);
        var response2 = await client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    /// <summary>
    /// Validates the application-level duplicate handling path:
    ///   HTTP request → EventService → DuplicateEventException → 409 Conflict.
    ///
    /// NOTE: This test uses FakeEventRepository to simulate the (UserId, EventId)
    /// uniqueness constraint because EF Core InMemory does not enforce unique indexes.
    /// It does NOT test the real SQL Server unique constraint or the SqlException
    /// catch path in EventRepository.AddAsync. The real constraint is validated by
    /// the correct migration (IX_Events_UserId_EventId) applied to SQL Server.
    /// </summary>
    [Fact]
    public async Task ProcessEvent_ConcurrencyTest_WithMockedQueue()
    {
        var mockQueue = Substitute.For<IWebhookDeliveryQueue>();
        using var factory = CreateFactoryWithFakeEventRepository(mockQueue);

        var client = factory.CreateClient();
        var email = $"user_{Guid.NewGuid()}@example.com";
        var token = await AuthenticateUserAsync(client, email);
        
        await CreateWebhookAsync(client, token, "Test Webhook", "http://localhost/test", new List<string> { "test.event" });

        var eventId = "concurrent-event-id-" + Guid.NewGuid().ToString();
        var requestData = new CreateEventRequest
        {
            EventId = eventId,
            EventType = "test.event",
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}")
        };

        // Fire two requests concurrently.
        // Note: WebApplicationFactory's test server may serialize these requests,
        // so this validates application-level duplicate handling rather than true
        // database-level race conditions.
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req1.Content = JsonContent.Create(requestData);

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/events");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req2.Content = JsonContent.Create(requestData);

        var task1 = client.SendAsync(req1);
        var task2 = client.SendAsync(req2);

        var results = await Task.WhenAll(task1, task2);

        // Assert: exactly one OK, exactly one Conflict
        var statusCodes = results.Select(r => r.StatusCode).ToList();
        
        Assert.Contains(HttpStatusCode.OK, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);
        
        Assert.Single(statusCodes, HttpStatusCode.OK);
        Assert.Single(statusCodes, HttpStatusCode.Conflict);

        // Verify exactly one event record exists in the database
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebhookServiceDbContext>();
        
        var eventCount = await dbContext.Events.CountAsync(e => e.EventId == eventId);
        Assert.Equal(1, eventCount);

        // Verify the queue received exactly one enqueue call
        await mockQueue.Received(1).EnqueueAsync(Arg.Any<DeliveryWorkItem>(), Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Decorates a real IEventRepository with in-memory (UserId, EventId) uniqueness
/// enforcement, simulating the SQL Server unique index for tests running against
/// EF Core InMemory (which does not enforce unique indexes).
///
/// Instance-scoped: each FakeEventRepository has its own HashSet, preventing
/// state leakage between tests.
/// </summary>
public class FakeEventRepository : IEventRepository
{
    private IEventRepository? _inner;
    private readonly HashSet<string> _insertedKeys = new();
    private readonly object _lock = new();

    /// <summary>
    /// Sets the inner (real) repository that receives the actual DB write
    /// after the uniqueness check passes.
    /// </summary>
    public FakeEventRepository WithInner(IEventRepository inner)
    {
        _inner = inner;
        return this;
    }

    public async Task AddAsync(Event evt, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var key = $"{evt.UserId}_{evt.EventId}";
            if (!_insertedKeys.Add(key))
            {
                throw new WebhookService.Core.Exceptions.DuplicateEventException(
                    $"Event with ID '{evt.EventId}' already exists for this user.");
            }
        }
        await _inner!.AddAsync(evt, ct);
    }
}
