# Clean Architecture + DDD Template (.NET 10)

A base for new .NET 10 APIs: Clean Architecture layers, tactical DDD, CQRS without MediatR, the Notification
pattern, read/write repositories with a unit of work, Hangfire jobs, JWT authentication with refresh-token rotation
and permission-based authorization, and observability with Serilog + OpenTelemetry (Grafana, Loki, Tempo).

## Layers

Dependencies point inward: `Web.Api → Infrastructure, Jobs → Application → Domain → SharedKernel`.

| Project | Contains |
|---|---|
| **SharedKernel** | Building blocks with no dependencies: `Primitives` (`Entity`, `AggregateRoot`, `ValueObject` — UUID v7 ids), `Notifications` (`Notification`, `Notifiable`), `Results` (`CommandResult`), `Pagination` (`PagedList`), `Events`, `Persistence` (repository markers), `DependencyInjection` (`IInjectScoped` / `IInjectTransient` / `IInjectSingleton`), `Time`. |
| **Domain** | Aggregates with behavior and private setters, value objects (`Email`), error catalogs, domain events, repository interfaces (`I{Aggregates}SearchableRepository` / `I{Aggregates}WritableRepository`), domain services (`UserRegistrationService`). |
| **Application** | One folder per use case (command/query, handler, validator, response). Pipeline decorators: logging → validation → transaction. `IUnitOfWork`, `IJobScheduler`, auth abstractions. No EF Core, Hangfire or Serilog. |
| **Infrastructure** | EF Core + PostgreSQL (snake_case), repository implementations, `UnitOfWork`, migrations, JWT + refresh tokens, permissions, HybridCache, domain-event dispatch. |
| **Jobs** | Hangfire: PostgreSQL storage (schema `hangfire`), server, dashboard with Basic auth, `JobBase` + auto-scheduled recurring jobs, `CommandJob<T>` behind `IJobScheduler`. |
| **Web.Api** | Minimal-API endpoints, Swagger, Serilog, OpenTelemetry, rate limiting, idempotency, security headers, health checks, global exception handling. |

## Conventions

### Notification pattern

Use cases return `CommandResult` / `CommandResult<T>` and never throw for expected failures. Aggregates collect
notifications instead of throwing; handlers check `IsValid` before persisting:

```csharp
var todoItem = TodoItem.Create(userId, command.Description, command.Priority, command.DueDate, command.Labels, now);

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

### Domain events

Aggregates raise events (`Raise(new TodoItemCompletedDomainEvent(Id))`); the `DbContext` collects them on save and
dispatches them **after the commit** to every `IDomainEventHandler<T>` (discovered automatically). A failing handler
is logged without failing the request. Slow side effects are enqueued as background jobs (`IJobScheduler`), which
retry.

### Dependency injection

Handlers, validators, endpoints and event handlers are discovered by convention. Any other class implements
`IInjectScoped`, `IInjectTransient` or `IInjectSingleton` and is registered as itself and its interfaces.

## Getting started

```bash
cp .env.example .env                                 # then set GRAFANA_ADMIN_PASSWORD (never committed)
docker compose up -d postgres observability          # PostgreSQL + Grafana/Loki/Tempo

# Migrations are never applied on startup — apply them yourself:
dotnet ef database update --project src/Infrastructure --startup-project src/Web.Api

dotnet run --project src/Web.Api                     # Swagger at http://localhost:5000/swagger
```

Or run everything in containers with `docker compose up -d` (apply the migrations from the host as above;
PostgreSQL is published on `localhost:5432`). The override enables HTTPS on port 5001, which needs the ASP.NET
development certificate — Visual Studio creates it; from the command line create it once:

```bash
dotnet dev-certs https -ep "$APPDATA/ASP.NET/Https/Web.Api.pfx" -p <password>
dotnet user-secrets --project src/Web.Api set "Kestrel:Certificates:Development:Password" "<password>"
```

The observability container publishes the OTLP ports 4317/4318 on the host (only needed to export from
`dotnet run`); if another stack already uses them, remove those two port mappings — the API container reaches the
collector inside the compose network.

| URL | What |
|---|---|
| `/swagger` | API documentation (Development only) |
| `/hangfire` | Jobs dashboard — Basic auth, credentials in `Jobs:Dashboard` |
| `/health`, `/health/live` | Readiness (checks the database) and liveness |
| http://localhost:3000 | Grafana (logs, traces, metrics) — login with `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` from `.env` |

New migration (generated by you, never at runtime):

```bash
dotnet ef migrations add {Name} --project src/Infrastructure --startup-project src/Web.Api -o Database/Migrations
```

## Observability

- **Logs** — Serilog. Console: readable text with the trace id in Development, JSON elsewhere. When
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set (docker compose sets it), logs are also exported over OTLP to **Loki**.
  Levels are configured in the `Serilog` section of `appsettings*.json`.
- **Traces and metrics** — OpenTelemetry (ASP.NET Core, HttpClient, Npgsql, runtime, one trace per job) exported
  over OTLP to **Tempo** and **Prometheus**.
- **Finding a request** — take the `traceId` from the response and, in Grafana → Explore:
  - Loki: `{service_name="Web.Api"} | trace_id="<traceId>"` — every log of that request;
  - Tempo: search by trace id — the request with its database calls.

  Logs also carry `CorrelationId` (the `Correlation-Id` request header, or the trace id) and `UserId`; logs written
  by a job carry `JobId` and `JobType`.

## Configuration

`appsettings.Development.json` has working local values. In any other environment provide (environment variables
or a secret store; the app refuses to start without them):

- `ConnectionStrings__Database`
- `Jwt__Secret` (32+ characters), `Jwt__Issuer`, `Jwt__Audience`
- `Jobs__Dashboard__Username`, `Jobs__Dashboard__Password` (8+ characters) — or `Jobs__Dashboard__Enabled=false`

`Jobs__Server__Enabled=false` runs the API without executing jobs (they are still enqueued), so API and workers can
scale separately.

## Tests

```bash
dotnet test CleanArchitecture.slnx    # Docker must be running (Testcontainers)
```

- `Domain.UnitTests` — aggregates, value objects, domain services, `CommandResult`, `PagedList`.
- `Application.UnitTests` — handlers (repositories and unit of work substituted), validators, decorators.
- `IntegrationTests` — the real API over HTTP against PostgreSQL: envelope, auth, caching, jobs, unit of work,
  dependency-injection markers.
- `ArchitectureTests` — layer dependencies and conventions.
