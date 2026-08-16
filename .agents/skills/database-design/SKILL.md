---
name: database-design
description: |
  Skill for designing and managing the SQL Server database schema using Entity Framework Core.
  Covers the 4-table model (Users, Webhooks, Events, DeliveryAttempts), migrations, seeding,
  relationships, indexes, and JSON column storage for event types.
---

# Database Design Skill

## Schema Overview

The database consists of exactly 4 tables:

```
┌──────────┐     ┌──────────────┐     ┌──────────┐     ┌──────────────────┐
│  Users   │────▶│   Webhooks   │     │  Events  │────▶│ DeliveryAttempts │
└──────────┘     └──────────────┘     └──────────┘     └──────────────────┘
                        │                   │                    │
                        └───────────────────┴────────────────────┘
                         (Webhooks + Events → DeliveryAttempts)
```

## Table: Users

| Column       | Type           | Notes                          |
|-------------|----------------|--------------------------------|
| Id          | Guid (PK)      | Generated on creation          |
| Email       | nvarchar(256)  | Unique, indexed                |
| PasswordHash| nvarchar(max)  | BCrypt or ASP.NET Identity hash|
| ApiKey      | nvarchar(64)   | Unique, indexed, for event API |
| CreatedAt   | datetimeoffset | UTC timestamp                  |

## Table: Webhooks

| Column         | Type           | Notes                                    |
|---------------|----------------|------------------------------------------|
| Id            | Guid (PK)      | Generated on creation                    |
| UserId        | Guid (FK)      | References Users.Id                      |
| Name          | nvarchar(256)  | Display name                             |
| Url           | nvarchar(2048) | Destination URL                          |
| Secret        | nvarchar(128)  | HMAC signing secret                      |
| IsActive      | bit            | Soft enable/disable                      |
| EventTypes    | nvarchar(max)  | JSON array, e.g. ["payment.created"]     |
| CreatedAt     | datetimeoffset | UTC timestamp                            |
| UpdatedAt     | datetimeoffset | UTC timestamp                            |

### Event Types Storage

Event types are stored as a JSON array inside the Webhooks table. This keeps the schema within 4 tables and is acceptable for this MVP scope.

```csharp
// EF Core value converter
builder.Property(w => w.EventTypes)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!
    );
```

## Table: Events

| Column      | Type           | Notes                          |
|------------|----------------|--------------------------------|
| Id         | Guid (PK)      | Idempotency key                |
| EventType  | nvarchar(128)  | e.g. "payment.created"         |
| Payload    | nvarchar(max)  | JSON payload                   |
| CreatedAt  | datetimeoffset | UTC timestamp                  |

### Idempotency

The Event ID serves as the idempotency key. Before processing, check if an event with the same ID already exists. If it does, return the existing event without reprocessing.

## Table: DeliveryAttempts

| Column         | Type           | Notes                              |
|---------------|----------------|------------------------------------|
| Id            | Guid (PK)      | Generated on creation              |
| WebhookId     | Guid (FK)      | References Webhooks.Id             |
| EventId       | Guid (FK)      | References Events.Id               |
| AttemptNumber | int            | 1, 2, 3, ...                      |
| HttpStatusCode| int?           | Response status (null if timeout)  |
| ResponseBody  | nvarchar(max)  | Truncated response body            |
| DurationMs    | long           | Request duration in milliseconds   |
| Success       | bit            | Whether delivery succeeded         |
| IsDeadLettered| bit            | True after max retries exhausted   |
| Timestamp     | datetimeoffset | When this attempt was made         |

## Indexes

- `Users.Email` — Unique index
- `Users.ApiKey` — Unique index
- `Webhooks.UserId` — Foreign key index
- `Events.EventType` — Non-unique index for filtering
- `DeliveryAttempts.WebhookId` — Foreign key index
- `DeliveryAttempts.EventId` — Foreign key index
- `DeliveryAttempts.(EventId, WebhookId)` — Composite index for delivery lookups

## EF Core Conventions

- Use Fluent API configuration in separate `IEntityTypeConfiguration<T>` classes.
- Configure value conversions for JSON columns.
- Use `datetimeoffset` for all timestamps (UTC).
- Configure cascade delete behavior appropriately.
- Use migrations for all schema changes.

## DbContext

```csharp
public class WebhookDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
}
```
