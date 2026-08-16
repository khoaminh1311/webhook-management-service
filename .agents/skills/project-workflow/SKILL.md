---
name: project-workflow
description: |
  Skill defining the phased development workflow for the Webhook Management Service.
  Each phase must be completed and confirmed before proceeding to the next.
  Covers phase definitions, deliverables, and completion criteria.
---

# Project Workflow Skill

## Development Phases

This project is developed in 6 sequential phases. Each phase must be completed and reviewed before proceeding.

---

### Phase 1 — Project Foundation

**Goal**: Set up the solution structure, project references, EF Core DbContext, and database schema.

**Deliverables**:
- Solution file with all projects (Api, Core, Infrastructure, Tests.Unit, Tests.Integration)
- Domain models for all 4 entities
- EF Core DbContext with entity configurations
- Initial migration
- `Program.cs` with basic service registration
- Swagger/OpenAPI configured
- Health check endpoint

**Completion criteria**:
- Solution builds without errors
- Database migration can be applied
- Swagger UI loads and shows health endpoint

---

### Phase 2 — Authentication and Webhook Management

**Goal**: Implement JWT auth, API key auth, and webhook CRUD endpoints.

**Deliverables**:
- User registration and login endpoints
- JWT token generation and validation
- API key generation and validation
- Webhook CRUD endpoints (Create, Read, Update, Delete)
- Input validation
- Proper authorization (users can only manage their own webhooks)

**Completion criteria**:
- Can register, login, and receive JWT
- Can create/read/update/delete webhooks with JWT auth
- Cannot access other users' webhooks
- Invalid input returns 400 with details

---

### Phase 3 — Event Processing and Background Delivery

**Goal**: Implement event creation, Channel<T> queuing, BackgroundService delivery worker, and mock endpoint.

**Deliverables**:
- Event creation endpoint (API key authenticated)
- Channel<T> producer/consumer setup
- BackgroundService webhook delivery worker
- HttpClient-based webhook delivery
- Mock webhook receiver endpoint
- Basic delivery attempt recording

**Completion criteria**:
- Can trigger an event via API
- Event is queued and picked up by background worker
- Webhook is delivered to registered URL
- Mock endpoint receives and logs the delivery
- Delivery attempt is recorded in database

---

### Phase 4 — Retry, HMAC, Idempotency, and Observability

**Goal**: Add exponential backoff retries, HMAC-SHA256 signing, event idempotency, and correlation ID tracing.

**Deliverables**:
- Exponential backoff retry logic (2s, 4s, 8s, ...)
- Configurable max retry attempts
- Dead letter state after max retries
- HMAC-SHA256 signature generation
- X-Webhook-Timestamp and X-Webhook-Signature headers
- Event ID idempotency check
- Correlation ID middleware
- Correlation ID in all log entries

**Completion criteria**:
- Failed deliveries are retried with exponential backoff
- After max retries, delivery is marked dead-lettered
- Webhook requests include valid HMAC signature
- Duplicate event IDs are rejected/deduplicated
- Correlation ID flows through the entire pipeline

---

### Phase 5 — Testing

**Goal**: Write meaningful unit and integration tests.

**Deliverables**:
- Unit tests for retry calculation
- Unit tests for HMAC signature generation
- Unit tests for idempotency logic
- Unit tests for core business rules
- Integration tests for auth flow
- Integration tests for webhook CRUD
- Integration tests for event triggering
- Integration tests for delivery flow

**Completion criteria**:
- All tests pass
- Tests cover real behavior, not trivial cases
- Tests are well-organized and readable

---

### Phase 6 — Documentation and CI

**Goal**: Complete documentation, finalize README, and set up GitHub Actions CI.

**Deliverables**:
- Complete README with setup instructions
- API documentation (Swagger + manual docs)
- HMAC signing format documentation
- Architecture decision documentation
- GitHub Actions workflow (build, test)
- Final code review and cleanup

**Completion criteria**:
- README enables a new developer to set up and run the project
- CI pipeline builds and runs all tests
- Documentation is accurate and complete

---

## Phase Transition Rules

1. **Do not skip phases.** Each phase builds on the previous one.
2. **Do not implement future phase work early.** Stay focused on current phase deliverables.
3. **Report completion** at the end of each phase with a summary of what was done.
4. **Wait for confirmation** before starting the next phase.
5. **Document deviations** if something must change from the original plan.
