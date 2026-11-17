# Clean Architecture + DDD Template (.NET 10)

A base for new .NET 10 APIs: Clean Architecture layers, tactical DDD, CQRS without MediatR, the Notification
pattern, read/write repositories with a unit of work, a transactional outbox for domain events, automatic auditing,
Hangfire jobs, JWT authentication with refresh-token rotation and permission-based authorization, and observability
with Serilog + OpenTelemetry (Grafana, Loki, Tempo).

## Layers

Dependencies point inward: `Web.Api → Infrastructure, Jobs → Application → Domain → SharedKernel`.

| Project | Contains |
|---|---|
| **SharedKernel** | Building blocks with no dependencies: `Primitives` (`Entity`, `AggregateRoot`, `ValueObject` — UUID v7 ids — and `IAuditable`), `Notifications` (`Notification`, `Notifiable`), `Results` (`CommandResult`), `Pagination` (`PagedList`), `Events`, `Persistence` (repository markers), `DependencyInjection` (`IInjectScoped` / `IInjectTransient` / `IInjectSingleton`), `Time`. |
| **Domain** | Aggregates with behavior and private setters, value objects (`Email`), error catalogs, domain events, repository interfaces (`I{Aggregates}SearchableRepository` / `I{Aggregates}WritableRepository`), domain services (`UserRegistrationService`). |
| **Application** | One folder per use case (command/query, handler, validator, response). Pipeline decorators: logging → validation → transaction. `IUnitOfWork`, `IJobScheduler`, auth abstractions. No EF Core, Hangfire or Serilog. |
| **Infrastructure** | EF Core + PostgreSQL (snake_case), repository implementations, `UnitOfWork`, migrations, transactional outbox, auditing interceptor, JWT + refresh tokens, versioned password hashing, permissions, HybridCache. |
| **Jobs** | Hangfire: PostgreSQL storage (schema `hangfire`), server, dashboard with Basic auth, `JobBase` + auto-scheduled recurring jobs, `CommandJob<T>` behind `IJobScheduler`. |
| **Web.Api** | Minimal-API endpoints, Swagger, Serilog, OpenTelemetry, forwarded headers, rate limiting, idempotency, security headers, health checks, global exception handling. |

## Conventions

### Notification pattern

Use cases return `CommandResult` / `CommandResult<T>` and never throw for expected failures. Aggregates collect
notifications instead of throwing; handlers check `IsValid` before persisting:

```csharp
var todoItem = TodoItem.Create(userId, command.Description, command.Priority, command.DueDate, command.Labels);

if (!todoItem.IsValid)
{
    return CommandResult.Fail<Guid>(todoItem);
}

todoItems.Add(todoItem);
await unitOfWork.SaveChangesAsync(cancellationToken);

return CommandResult.Ok(todoItem.Id);
```

Every response — success, notification, framework error (401/403/404/429) or unhandled exception — has the same
envelope. The notification type decides the status code (`Validation`/`BusinessRule` 400, `Unauthorized` 401,
`Forbidden` 403, `NotFound` 404, `Conflict` 409, `Failure` 500):

```json
{
  "success": false,
  "data": null,
  "notifications": [{ "key": "Description", "message": "'Description' must not be empty.", "type": "Validation" }],
  "traceId": "0af7651916cd43dd8448eb211c80319c"
}
```

`traceId` is the W3C trace id of the request: the `trace_id` of all its logs in Loki and of its trace in Tempo
(see [Observability](#observability)). Saving an aggregate that is not valid throws, as a safety net.

### Repositories and unit of work

No generic repository. Each aggregate has a read side (`ITodoItemsSearchableRepository`: untracked queries) and/or a
write side (`ITodoItemsWritableRepository`: loads for change, adds, removes). Repositories never save —
`IUnitOfWork.SaveChangesAsync` commits everything the use case changed. A command marked `ITransactionalCommand`
runs in one transaction, committed only when it succeeds.

### Domain services

Business rules that no single aggregate can enforce (see `UserRegistrationService`: e-mail uniqueness). They
decide; handlers persist.

### Domain events and the transactional outbox

Aggregates raise events (`Raise(new TodoItemCompletedDomainEvent(Id))`). On save, the `DbContext` writes them to
`outbox_messages` **in the same transaction** as the change: both are committed or neither is, so an event is never
lost and never published for a change that was rolled back. A background processor then delivers them to every
`IDomainEventHandler<T>` (discovered automatically):

- **Polling with `FOR UPDATE SKIP LOCKED`** — each instance claims a different batch; full batches are drained back
  to back, an empty outbox is polled every second (`Outbox:PollingIntervalInMilliseconds`). A partial index on the
  deliverable messages keeps the query cheap however large the table grows.
- **At least once, idempotent per handler** — `outbox_message_consumers` records the handlers that succeeded; a
  retry runs only the ones that failed. A crash in the middle of a batch can still deliver a message twice, so
  handlers must tolerate repeats.
- **Retries and dead letters** — a failing handler is retried with exponential backoff (2 s, 4 s, 8 s…); after
  `Outbox:MaxAttempts` the message becomes a dead letter (`dead_lettered_on_utc`) and the queue keeps moving. Unknown
  types or corrupted content become dead letters at once. To replay one after fixing the cause:

  ```sql
  UPDATE public.outbox_messages
  SET dead_lettered_on_utc = NULL, attempts = 0, next_attempt_on_utc = NULL, error = NULL
  WHERE id = '<message id>';
  ```

- **Same trace as the request** — the message stores the request's `traceparent`; delivery is a span of the same
  trace in Tempo and the handlers' logs share its `trace_id`.
- **Housekeeping and monitoring** — processed messages are deleted after `Outbox:RetentionInDays`; `/health` reports
  the outbox as *Degraded* when there are dead letters or delivery is more than five minutes behind.
- **Handlers must be quick** — the batch keeps its rows locked while they run. Slow work (e-mail, HTTP calls) is
  enqueued as a background job (`IJobScheduler`), which has its own retries.

Events are serialized as JSON and resolved by type name from the Domain assembly: keep them small, with primitive
values (ids, strings). Ordering follows the time they occurred but is not guaranteed across retries.
`Outbox:Enabled=false` stops delivery in that process (events are still written), e.g. to deliver from workers only.

### Auditing

An entity that implements `IAuditable` (`CreatedOnUtc`, `CreatedBy`, `ModifiedOnUtc`, `ModifiedBy`, all get-only)
is stamped on every save by `AuditingInterceptor`: the time and the authenticated user (`null` for anonymous requests
and background work). Creation values can never be overwritten. No configuration per entity is needed — the
properties are mapped for every `IAuditable`. Bulk `ExecuteUpdate`/`ExecuteDelete` bypass it.

```csharp
public sealed class TodoItem : AggregateRoot, IAuditable
{
    public DateTime CreatedOnUtc { get; }
    public Guid? CreatedBy { get; }
    public DateTime? ModifiedOnUtc { get; }
    public Guid? ModifiedBy { get; }
    // ...
}
```

### Dependency injection

Handlers, validators, endpoints and event handlers are discovered by convention. Any other class implements
`IInjectScoped`, `IInjectTransient` or `IInjectSingleton` and is registered as itself and its interfaces.

## Security

- **Refresh tokens** are stored hashed, rotated on every use, and reusing a rotated token revokes the whole session.
  The row is versioned with PostgreSQL's `xmin`: two concurrent refreshes with the same token cannot both succeed
  (the loser gets `409 Users.RefreshTokenConflict`).
- **Passwords** use PBKDF2-HMAC-SHA512 stored as `v1$iterations$salt$hash`. The cost is configurable
  (`PasswordHasher:Iterations`, default 210,000 — OWASP) and changing it does not invalidate anything: a hash with a
  different cost, or in the legacy format, is upgraded on the next successful login.
- **Behind a reverse proxy** the client IP and scheme come from `X-Forwarded-For` / `X-Forwarded-Proto`, but only
  when sent by a trusted proxy (loopback by default). Add your proxy to `ForwardedHeaders:KnownProxies` or
  `ForwardedHeaders:KnownNetworks`; otherwise every client shares the proxy's IP and its rate limit.

## Getting started

```bash
cp .env.example .env                                 # then set GRAFANA_ADMIN_PASSWORD (never committed)
docker compose up -d postgres observability          # PostgreSQL + Grafana/Loki/Tempo

# Migrations are never applied on startup — apply them yourself:
dotnet ef database update --project src/Infrastructure --startup-project src/Web.Api

dotnet run --project src/Web.Api                     # Swagger at http://localhost:5000/swagger
```

Or run everything in containers with `docker compose up -d` (apply the migrations from the host as above;
PostgreSQL is published on `localhost:5432`). The API listens on HTTP only (`http://localhost:5000`), so no
certificate is needed. For HTTPS on port 5001, export the development certificate once, set `DEV_CERT_PASSWORD` in
`.env` and add `docker-compose.https.yml`:

```bash
dotnet dev-certs https -ep ~/.aspnet/https/aspnetapp.pfx -p <password>
dotnet dev-certs https --trust
docker compose -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.https.yml up -d
```

The observability container publishes the OTLP ports 4317/4318 on the host (only needed to export from
`dotnet run`); if another stack already uses them, remove those two port mappings — the API container reaches the
collector inside the compose network.

| URL | What |
|---|---|
| `/swagger` | API documentation (Development only) |
| `/hangfire` | Jobs dashboard — Basic auth, credentials in `Jobs:Dashboard` |
| `/health`, `/health/live` | Readiness (database and outbox) and liveness |
| http://localhost:3000 | Grafana (logs, traces, metrics) — login with `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` from `.env` |

New migration (generated by you, never at runtime):

```bash
dotnet ef migrations add {Name} --project src/Infrastructure --startup-project src/Web.Api -o Database/Migrations
```

## Observability

- **Logs** — Serilog. Console: readable text with the trace id in Development, JSON elsewhere. When
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set (docker compose sets it), logs are also exported over OTLP to **Loki**.
  Levels are configured in the `Serilog` section of `appsettings*.json`. The outbox polling query is left out of
  the SQL log, which would otherwise get a line every second in Development.
- **Traces and metrics** — OpenTelemetry (ASP.NET Core, HttpClient, Npgsql, runtime, one trace per job, outbox
  deliveries) exported over OTLP to **Tempo** and **Prometheus**.
- **Finding a request** — take the `traceId` from the response and, in Grafana → Explore:
  - Loki: `{service_name="Web.Api"} | trace_id="<traceId>"` — every log of that request, including the delivery of
    its domain events;
  - Tempo: search by trace id — the request with its database calls and the `outbox <Event>` spans.

  Logs also carry `CorrelationId` (the `Correlation-Id` request header, or the trace id), `UserId` and `ClientIp`;
  logs written by a job carry `JobId` and `JobType`.

## Configuration

`appsettings.Development.json` has working local values. In any other environment provide (environment variables
or a secret store; the app refuses to start without them):

- `ConnectionStrings__Database`
- `Jwt__Secret` (32+ characters), `Jwt__Issuer`, `Jwt__Audience`
- `Jobs__Dashboard__Username`, `Jobs__Dashboard__Password` (8+ characters) — or `Jobs__Dashboard__Enabled=false`

Optional, with defaults in `appsettings.json`:

| Section | Settings |
|---|---|
| `Outbox` | `Enabled`, `PollingIntervalInMilliseconds` (1000), `BatchSize` (50), `MaxAttempts` (5), `RetentionInDays` (7) |
| `PasswordHasher` | `Iterations` (210000) |
| `ForwardedHeaders` | `KnownProxies`, `KnownNetworks` (e.g. `10.0.0.0/8`), `ForwardLimit` (1) |
| `RateLimiting` | `Global` / `Authentication`: `PermitLimit`, `WindowInSeconds` |

`Jobs__Server__Enabled=false` runs the API without executing jobs (they are still enqueued), so API and workers can
scale separately.

## Tests

```bash
dotnet test CleanArchitecture.slnx    # Docker must be running (Testcontainers)
```

- `Domain.UnitTests` — aggregates, value objects, domain services, `CommandResult`, `PagedList`.
- `Application.UnitTests` — handlers (repositories and unit of work substituted), validators, decorators.
- `IntegrationTests` — the real API over HTTP against PostgreSQL: envelope, auth, caching, jobs, unit of work,
  outbox (delivery, retries, dead letters, concurrent processors, cleanup), auditing, refresh-token concurrency,
  password hashing, dependency-injection markers. The outbox processor is off in tests; they deliver it explicitly.
- `ArchitectureTests` — layer dependencies and conventions.
