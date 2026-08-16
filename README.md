# Webhook Management & Delivery Service

A portfolio-quality backend service built with **ASP.NET Core** that enables users to register webhooks, trigger events, and reliably deliver webhook payloads with retry handling, HMAC-SHA256 security, and structured observability.

---

## Features

- **Webhook Management** — Register, update, and manage webhooks subscribed to specific event types
- **Event Processing** — Trigger events via API with in-memory queuing using `Channel<T>`
- **Reliable Delivery** — Background worker delivers webhooks with exponential backoff retry
- **Security** — JWT authentication, API key auth, and HMAC-SHA256 signed webhook payloads
- **Idempotency** — Event ID-based deduplication prevents duplicate processing
- **Observability** — Correlation ID tracing through the entire request-to-delivery pipeline
- **Dead Letter Handling** — Failed deliveries are preserved, not silently discarded

## Tech Stack

| Layer              | Technology                             |
|--------------------|----------------------------------------|
| API                | ASP.NET Core Web API (.NET 8)          |
| Database           | SQL Server + Entity Framework Core     |
| Authentication     | JWT + API Key                          |
| Queue              | System.Threading.Channels              |
| Background Worker  | BackgroundService                      |
| Testing            | xUnit                                  |
| Documentation      | Swagger / OpenAPI                      |
| CI                 | GitHub Actions                         |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or Developer Edition)

## Getting Started

> 🚧 Setup instructions will be added in Phase 1.

```bash
# Clone the repository
git clone https://github.com/your-username/webhook-management-service.git
cd webhook-management-service

# Restore dependencies
dotnet restore

# Apply database migrations
dotnet ef database update --project src/WebhookService.Infrastructure --startup-project src/WebhookService.Api

# Run the application
dotnet run --project src/WebhookService.Api

# Run tests
dotnet test
```

## Project Structure

```
webhook-management-service/
├── src/
│   ├── WebhookService.Api/            # Controllers, middleware, configuration
│   ├── WebhookService.Core/           # Domain models, interfaces, DTOs, services
│   └── WebhookService.Infrastructure/ # EF Core, repositories, external services
├── tests/
│   ├── WebhookService.Tests.Unit/     # Unit tests
│   └── WebhookService.Tests.Integration/ # Integration tests
├── docs/                              # Project documentation
└── README.md
```

## API Overview

| Endpoint                | Method | Auth    | Description              |
|-------------------------|--------|---------|--------------------------|
| `/api/auth/register`    | POST   | None    | Register a new user      |
| `/api/auth/login`       | POST   | None    | Login and receive JWT    |
| `/api/webhooks`         | POST   | JWT     | Create a webhook         |
| `/api/webhooks`         | GET    | JWT     | List your webhooks       |
| `/api/webhooks/{id}`    | GET    | JWT     | Get webhook details      |
| `/api/webhooks/{id}`    | PUT    | JWT     | Update a webhook         |
| `/api/webhooks/{id}`    | DELETE | JWT     | Delete a webhook         |
| `/api/events`           | POST   | API Key | Trigger an event         |
| `/api/deliveries`       | GET    | JWT     | List delivery attempts   |
| `/api/deliveries/{id}`  | GET    | JWT     | Get delivery details     |
| `/api/mock-webhook`     | POST   | None    | Mock webhook receiver    |

> Full API documentation available via Swagger UI at `/swagger` when running locally.

## Architecture

```
Client → REST API → Event Created → Channel<T> Queue → Background Worker → Webhook Delivery
                                                                              ↓
                                                                     Exponential Backoff
                                                                        → Retry
                                                                        → Dead Letter
```

## Documentation

- [Project Specification](docs/specification.md)

## License

This project is created for portfolio/educational purposes.
