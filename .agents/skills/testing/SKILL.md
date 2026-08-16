---
name: testing
description: |
  Skill for writing unit tests and integration tests using xUnit and ASP.NET Core testing utilities.
  Covers test organization, meaningful test selection, mocking with NSubstitute or Moq,
  WebApplicationFactory for integration tests, and test naming conventions.
---

# Testing Skill

## Test Project Structure

```
WebhookService.Tests.Unit         → Pure unit tests, no database or HTTP
WebhookService.Tests.Integration  → Full API + database integration tests
```

## Unit Test Guidelines

### What to Unit Test

- **Retry calculation**: Verify exponential backoff produces correct delays.
- **HMAC signature generation**: Verify signature matches expected output for known inputs.
- **Idempotency logic**: Verify duplicate event detection works correctly.
- **Business rules**: Verify webhook matching, event type filtering, validation logic.

### What NOT to Unit Test

- EF Core DbContext configuration (tested by integration tests)
- ASP.NET Core middleware plumbing (tested by integration tests)
- Trivial getters/setters

### Naming Convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
```csharp
[Fact]
public void CalculateBackoffDelay_AttemptTwo_ReturnsFourSeconds()

[Fact]
public void GenerateSignature_ValidPayload_ReturnsCorrectHmac()

[Fact]
public async Task CreateEvent_DuplicateEventId_ReturnsExistingEvent()
```

### Test Structure (Arrange-Act-Assert)

```csharp
[Fact]
public void CalculateBackoffDelay_AttemptThree_ReturnsEightSeconds()
{
    // Arrange
    var retryService = new RetryService(Options.Create(new RetrySettings { BaseDelaySeconds = 2 }));

    // Act
    var delay = retryService.CalculateBackoffDelay(attemptNumber: 3);

    // Assert
    Assert.Equal(TimeSpan.FromSeconds(8), delay);
}
```

### Mocking

Use a mocking library (NSubstitute or Moq) for isolating dependencies:

```csharp
var mockRepo = Substitute.For<IWebhookRepository>();
mockRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult<Webhook?>(testWebhook));

var service = new WebhookService(mockRepo, _logger);
```

## Integration Test Guidelines

### WebApplicationFactory Setup

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with in-memory database for tests
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<WebhookDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<WebhookDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
}
```

### What to Integration Test

- **Authentication flow**: Register → Login → Use token → Access protected endpoint.
- **Webhook CRUD**: Create, read, update, delete webhooks via API.
- **Event triggering**: POST event → verify it's queued and processed.
- **Delivery flow**: End-to-end event → webhook delivery (using mock endpoint).
- **Error cases**: Invalid input (400), unauthorized (401), not found (404).

### Test Helpers

Create helper methods for common operations:

```csharp
protected async Task<string> AuthenticateAsync(HttpClient client)
{
    // Register and login, return JWT token
}

protected async Task<HttpResponseMessage> CreateWebhookAsync(
    HttpClient client, string token, CreateWebhookRequest request)
{
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);
    return await client.PostAsJsonAsync("/api/webhooks", request);
}
```

## Test Data

- Use deterministic test data with known expected outputs.
- Avoid random data in tests unless testing randomness itself.
- Create test fixtures for shared setup across related tests.
