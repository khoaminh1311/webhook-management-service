# Webhook Management & Delivery Service — Project Rules

## Technology Stack

- **Language**: C# (.NET 8)
- **Framework**: ASP.NET Core Web API
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Auth**: JWT (management API) + API Key (integration endpoints)
- **Queue**: System.Threading.Channels / Channel<T>
- **Worker**: BackgroundService
- **Tests**: xUnit, ASP.NET Core integration testing
- **Docs**: Swagger / OpenAPI
- **CI**: GitHub Actions

## Prohibited Technologies

Do NOT introduce: Redis, RabbitMQ, Kafka, Docker, Kubernetes, microservices, frontend frameworks — unless the user explicitly requests them.

## Architecture Rules

1. **Async-first**: Use `async/await` throughout. Never block on async code.
2. **Cancellation tokens**: Propagate `CancellationToken` through all async call chains.
3. **Dependency injection**: Use constructor injection. Register services in `Program.cs` or extension methods.
4. **No business logic in controllers**: Controllers delegate to service classes.
5. **Small cohesive classes**: Each class has a single clear responsibility.
6. **Validation**: Use FluentValidation or Data Annotations. Validate at the API boundary.
7. **Error handling**: Use centralized exception handling middleware. Return consistent error responses.
8. **HTTP status codes**: Follow REST conventions (200, 201, 204, 400, 401, 403, 404, 409, 500).

## Naming Conventions

- **Projects**: `WebhookService.Api`, `WebhookService.Core`, `WebhookService.Infrastructure`, `WebhookService.Tests.Unit`, `WebhookService.Tests.Integration`
- **Classes**: PascalCase, descriptive nouns (e.g., `WebhookDeliveryWorker`, `HmacSignatureService`)
- **Methods**: PascalCase, verb-first (e.g., `CreateWebhookAsync`, `CalculateBackoffDelay`)
- **Async methods**: Suffix with `Async`
- **Interfaces**: Prefix with `I` (e.g., `IWebhookService`)
- **Constants**: PascalCase
- **Private fields**: `_camelCase` with underscore prefix

## Database Rules

- Exactly 4 tables: Users, Webhooks, Events, DeliveryAttempts
- Do not add tables to appear more complex
- Event types stored as JSON collection within Webhooks table
- Use EF Core migrations for schema changes

## Testing Rules

- Write meaningful tests that validate real behavior
- Do not create trivial tests solely for coverage numbers
- Unit tests: retry calculation, HMAC, idempotency, business rules
- Integration tests: API endpoints, auth, DB interaction, event flow

## Code Quality

- No giant classes (aim for <200 lines per class)
- No duplicated validation logic
- No swallowed exceptions
- No unnecessary abstractions
- Use structured logging (ILogger<T>)
- Use correlation IDs for traceability

## Development Workflow

- Develop phase-by-phase (Phase 1 through Phase 6)
- Do not skip phases or implement future phases early
- Each phase must be confirmed complete before proceeding
