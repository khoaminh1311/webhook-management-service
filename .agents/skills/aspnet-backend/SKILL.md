---
name: aspnet-backend
description: |
  Skill for building ASP.NET Core Web API backend services. Covers controller design,
  service layer patterns, dependency injection, middleware, configuration, authentication
  (JWT + API Key), Channel<T> queuing, BackgroundService workers, and structured logging.
---

# ASP.NET Core Backend Skill

## Architecture Layers

This project uses a clean layered architecture:

```
WebhookService.Api            → Controllers, middleware, configuration, Program.cs
WebhookService.Core           → Domain models, interfaces, DTOs, business logic services
WebhookService.Infrastructure → EF Core DbContext, repositories, external service implementations
```

## Controller Guidelines

- Controllers are thin: validate input, call a service, return a result.
- Use `[ApiController]` attribute for automatic model validation.
- Use `ActionResult<T>` return types for proper Swagger documentation.
- Group endpoints logically (Auth, Webhooks, Events, Deliveries, MockWebhook).
- Use `[Authorize]` for JWT-protected endpoints.
- Use custom `[ApiKeyAuth]` attribute for API-key-protected endpoints.

Example pattern:
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<ActionResult<WebhookResponse>> Create(
        CreateWebhookRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _webhookService.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
```

## Service Layer

- All business logic resides in service classes in `WebhookService.Core`.
- Services are registered via extension methods for clean DI setup.
- Services accept and propagate `CancellationToken`.

## Channel<T> Queue Pattern

```csharp
// Registration
builder.Services.AddSingleton(Channel.CreateUnbounded<WebhookDeliveryMessage>(
    new UnboundedChannelOptions { SingleReader = false, SingleWriter = false }));

// Producer (in service layer)
await _channel.Writer.WriteAsync(message, ct);

// Consumer (BackgroundService)
await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
{
    await ProcessDeliveryAsync(message, stoppingToken);
}
```

## BackgroundService Worker

- Reads from Channel<T> in a loop.
- Handles retry scheduling with exponential backoff.
- Logs every delivery attempt with correlation ID.
- Catches and logs exceptions without crashing.

## Authentication

### JWT Authentication
- Used for management API endpoints (webhooks CRUD, deliveries, auth).
- Token contains user ID and claims.
- Configure in `Program.cs` with `AddAuthentication().AddJwtBearer()`.

### API Key Authentication
- Used for event-triggering endpoints.
- Implemented as a custom authentication handler or action filter.
- API key validated against stored keys.

## Middleware

- **Correlation ID Middleware**: Generates or reads `X-Correlation-Id` header on every request.
- **Exception Handling Middleware**: Catches unhandled exceptions, logs them, returns consistent error response.

## Configuration

Use `appsettings.json` and the Options pattern:
```csharp
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RetrySettings>(builder.Configuration.GetSection("Retry"));
```

## Structured Logging

- Use `ILogger<T>` everywhere.
- Include correlation ID in log scopes.
- Log at appropriate levels: Information for normal flow, Warning for retries, Error for failures.
