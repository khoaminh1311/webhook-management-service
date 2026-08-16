# Webhook Management & Delivery Service — Project Specification

## Overview

A portfolio-quality backend service that allows users to register webhooks, trigger events, and reliably deliver webhook payloads to registered endpoints with retry handling, HMAC security, and observability.

**Purpose**: Demonstrate backend engineering skills suitable for a Backend Developer Internship.

---

## Technology Stack

| Component          | Technology                                |
|-------------------|-------------------------------------------|
| Language           | C# (.NET 8)                              |
| Web Framework      | ASP.NET Core Web API                     |
| ORM                | Entity Framework Core                    |
| Database           | SQL Server                               |
| Authentication     | JWT + API Key                            |
| In-Process Queue   | System.Threading.Channels / Channel<T>   |
| Background Worker  | BackgroundService                        |
| Unit Testing       | xUnit                                    |
| Integration Testing| ASP.NET Core TestServer                  |
| API Documentation  | Swagger / OpenAPI                        |
| CI/CD              | GitHub Actions                           |

---

## Architecture

```
Client (REST)
    │
    ▼
┌─────────────────────────┐
│     ASP.NET Core API    │
│  ┌───────────────────┐  │
│  │  Auth Middleware   │  │
│  │  (JWT / API Key)  │  │
│  └───────────────────┘  │
│  ┌───────────────────┐  │
│  │   Controllers     │  │
│  └───────┬───────────┘  │
│          │              │
│  ┌───────▼───────────┐  │
│  │   Services        │  │
│  └───────┬───────────┘  │
│          │              │
│  ┌───────▼───────────┐  │
│  │  Channel<T> Queue │  │
│  └───────┬───────────┘  │
│          │              │
│  ┌───────▼───────────┐  │
│  │ BackgroundService  │  │
│  │ (Delivery Worker)  │  │
│  └───────┬───────────┘  │
└──────────┼──────────────┘
           │
           ▼
   External Endpoint
   (or Mock Endpoint)
```

---

## Database Schema

Four tables:

### 1. Users
- Id (PK, Guid)
- Email (unique)
- PasswordHash
- ApiKey (unique)
- CreatedAt

### 2. Webhooks
- Id (PK, Guid)
- UserId (FK → Users)
- Name
- Url
- Secret
- IsActive
- EventTypes (JSON array)
- CreatedAt
- UpdatedAt

### 3. Events
- Id (PK, Guid — also serves as idempotency key)
- EventType
- Payload (JSON)
- CreatedAt

### 4. DeliveryAttempts
- Id (PK, Guid)
- WebhookId (FK → Webhooks)
- EventId (FK → Events)
- AttemptNumber
- HttpStatusCode
- ResponseBody
- DurationMs
- Success
- IsDeadLettered
- Timestamp

---

## API Endpoints

### Authentication
| Method | Endpoint              | Auth     | Description           |
|--------|-----------------------|----------|-----------------------|
| POST   | /api/auth/register    | None     | Register new user     |
| POST   | /api/auth/login       | None     | Login, receive JWT    |

### Webhook Management
| Method | Endpoint              | Auth     | Description           |
|--------|-----------------------|----------|-----------------------|
| POST   | /api/webhooks         | JWT      | Create webhook        |
| GET    | /api/webhooks         | JWT      | List user's webhooks  |
| GET    | /api/webhooks/{id}    | JWT      | Get webhook details   |
| PUT    | /api/webhooks/{id}    | JWT      | Update webhook        |
| DELETE | /api/webhooks/{id}    | JWT      | Delete webhook        |

### Events
| Method | Endpoint              | Auth     | Description           |
|--------|-----------------------|----------|-----------------------|
| POST   | /api/events           | API Key  | Trigger an event      |

### Deliveries
| Method | Endpoint              | Auth     | Description           |
|--------|-----------------------|----------|-----------------------|
| GET    | /api/deliveries       | JWT      | List delivery attempts|
| GET    | /api/deliveries/{id}  | JWT      | Get delivery details  |

### Mock Endpoint
| Method | Endpoint              | Auth     | Description           |
|--------|-----------------------|----------|-----------------------|
| POST   | /api/mock-webhook     | None     | Receive test webhooks |

---

## Authentication Model

### JWT (Management API)
- Users register with email/password.
- Login returns a JWT token.
- Token used as Bearer auth for webhook management and delivery viewing.

### API Key (Event Triggering)
- Each user has a unique API key generated at registration.
- API key used via header (e.g., `X-Api-Key`) to trigger events.
- Simpler auth model for integration/automation scenarios.

---

## Webhook Security (HMAC-SHA256)

Each webhook delivery includes:
- `X-Webhook-Timestamp`: Unix timestamp of when the signature was generated.
- `X-Webhook-Signature`: HMAC-SHA256 signature.

### Signing Format

```
signing_payload = "{timestamp}.{json_payload}"
signature = HMAC-SHA256(webhook_secret, signing_payload)
header_value = "sha256={hex_encoded_signature}"
```

### Verification (recipient side)
1. Extract timestamp from `X-Webhook-Timestamp`.
2. Reconstruct signing payload: `"{timestamp}.{body}"`.
3. Compute HMAC-SHA256 using the shared secret.
4. Compare computed signature with `X-Webhook-Signature`.
5. Optionally reject if timestamp is too old (replay protection).

---

## Retry Behavior

- Strategy: Exponential backoff.
- Base delay: 2 seconds.
- Formula: `delay = baseDelay * 2^(attemptNumber - 1)`
  - Attempt 1: 2s
  - Attempt 2: 4s
  - Attempt 3: 8s
- Max attempts: Configurable (default: 5).
- After final failure: Mark as dead-lettered.
- All attempts are persisted in DeliveryAttempts table.

---

## Idempotency

- Each event has a unique ID (Guid).
- Before processing a new event, check if an event with the same ID already exists.
- If it exists, return the existing event (no reprocessing).
- No separate idempotency framework needed.

---

## Observability

- Structured logging via `ILogger<T>`.
- Correlation ID generated per request via middleware.
- Correlation ID propagated through: API → Event creation → Channel → Worker → Delivery.
- No OpenTelemetry for this MVP.

---

## Development Phases

| Phase | Name                                          | Status      |
|-------|-----------------------------------------------|-------------|
| 0     | Bootstrap (skills, rules, docs)               | ✅ Complete |
| 1     | Project Foundation                            | ⬜ Pending  |
| 2     | Authentication and Webhook Management         | ⬜ Pending  |
| 3     | Event Processing and Background Delivery      | ⬜ Pending  |
| 4     | Retry, HMAC, Idempotency, and Observability   | ⬜ Pending  |
| 5     | Testing                                       | ⬜ Pending  |
| 6     | Documentation and CI                          | ⬜ Pending  |
