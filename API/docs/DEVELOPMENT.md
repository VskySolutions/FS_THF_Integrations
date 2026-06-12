# IntegrationHub — Development Guidelines

This document defines how the IntegrationHub codebase is organized and how to extend it consistently. Read it before adding features. It complements the [README](../README.md).

---

## 1. Architecture rules

The solution is **Clean Architecture**. The single most important rule:

> **Dependencies point inward.** Inner layers never reference outer layers.

```
Domain  ←  Application  ←  Infrastructure  ←  Api / Workers / McpServer
   ▲                                              
 Shared  ── referenced by everything, references nothing else
```

| Layer | May reference | Must NOT reference | Contains |
|-------|---------------|--------------------|----------|
| **Domain** | Shared | Application, Infrastructure, ASP.NET, EF Core | Entities, enums. POCOs only — no attributes, no EF, no framework types. |
| **Shared** | (nothing) | everything | Config option classes, `ApiResponse` envelopes, security constants, connector result types. |
| **Application** | Domain, Shared | Infrastructure, ASP.NET, EF Core | **Interfaces** (`I…Repository`, `I…Service`, `IConnector`, …), MediatR commands/handlers, transformers, validators, flow services. |
| **Infrastructure** | Application, Domain, Shared | ASP.NET MVC (controllers) | EF Core `DbContext` + repositories, connectors, Hangfire jobs, Serilog, security, Data Protection. **Implements** Application interfaces. |
| **Api** | Infrastructure, Application, Domain, Shared | — | Controllers, middleware, auth, OpenAPI, composition root. |
| **Workers / McpServer** | Infrastructure, Application, Domain, Shared | — | Host + composition root. |

**Golden rules**
- Define an **interface in Application**, implement it in **Infrastructure**. Controllers and services depend on the interface, never the concrete class.
- EF Core, `HttpContext`, Hangfire, and Serilog types stay in Infrastructure/Api — never in Domain or Application abstractions.
- The Hangfire **job classes live in Infrastructure** (not Workers) so the API can enqueue them and the Worker can execute them.

---

## 2. Dependency injection

Each layer exposes one registration entry point:

- `IServiceCollection.AddApplication()` — MediatR, flow services, transformers (keyed by system pair), validators.
- `IServiceCollection.AddInfrastructure(IConfiguration)` — options binding, persistence, security, retry, connectors, Data Protection, health checks.

Conventions:
- Register **interface → implementation**: `services.AddScoped<IFooRepository, FooRepository>();`
- **Scoped** for anything touching the `DbContext` (repositories, unit of work, tenant/correlation context, flow services). **Singleton** for stateless helpers (password hasher, rule evaluator, signing key provider, API-key validator). Connectors are **scoped** (they cache a token per request/job).
- Group registrations into private `Add…()` helpers inside `Infrastructure.DependencyInjection` (e.g. `AddPersistence`, `AddSecurity`, `AddRetry`, `AddConnectors`).
- Host-specific overrides (e.g. the API's `HttpContextActorAccessor`) are registered in the host's extension and rely on `TryAdd…` defaults in Infrastructure.

---

## 3. Persistence

- One `DbContext`: `IntegrationHubDbContext`. Entity mappings live in `Persistence/Configurations/*` as `IEntityTypeConfiguration<T>` (applied via `ApplyConfigurationsFromAssembly`). **No data annotations on entities.**
- **Repositories stage changes; they do not save.** Commit via `IUnitOfWork.SaveChangesAsync()`. This lets an action and its audit entry commit in one transaction.
- The `AuditTrail` repository is **append-only** — expose `AddAsync` only; never an update/delete path.
- All public repo methods are `async` and take a `CancellationToken` (default it).
- Enums are persisted **as strings** (`.HasConversion<string>()`).

### Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/IntegrationHub.Infrastructure \
  --startup-project src/IntegrationHub.Api \
  --output-dir Persistence/Migrations
```

The API applies migrations on startup. Never edit an already-applied migration — add a new one.

---

## 4. Multi-tenancy (non-negotiable)

- Tenant-scoped entities (`IntegrationJob`, `IntegrationLog`, `RetryQueue`, `AuditTrail`, `MappingConfiguration`) carry a `TenantId`.
- `IntegrationHubDbContext` applies a **global query filter** keyed to `ITenantContext.TenantId`, and **stamps `TenantId` on insert** in `SaveChanges`. You normally don't write tenant filters by hand.
- The filter is a **no-op when no tenant is resolved** (background/global ops). For cross-tenant admin queries (Super Admin), use `.IgnoreQueryFilters()` with an explicit `TenantId` filter.
- `ITenantContext` is set by `TenantResolutionMiddleware` (API) from the JWT `activeTenantId`, and by the Hangfire tenant filter (Worker) from the job payload — **before** any data access.
- Tenant credentials are resolved per-tenant at runtime via `ITenantApiConfigurationService` and decrypted with `ICredentialEncryptionService` (Data Protection). Connectors never read credentials from `appsettings`.

---

## 5. API conventions (controllers)

- One `[ApiController]` per resource area. Route at the class or action level.
- **Every response goes through `ApiResponseFactory`** (`Success` / `Paginated` / `Error` / `NotFound` / `Unauthorized` / `Forbidden`). Never return raw objects.
- **Validation** is FluentValidation `AbstractValidator<TRequest>` classes (auto-registered from the assembly). Invalid models are turned into a `VALIDATION_FAILED` envelope by the global `ValidationActionFilter` — controllers assume the model is valid.
- **Authorization** via policy attributes: `[Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]` etc. In-code tenant-scope checks (e.g. a Tenant Admin acting on another tenant) return `403` explicitly.
- Read claims via `User.GetUserId()` / `GetActiveTenantId()` / `GetRole()` / `IsSuperAdmin()` (extensions in `Api/Security`).
- **Document** each action: `[Tags("Area")]` at the class, `[ProducesResponseType<T>(status)]` for success and `ApiErrorResponse` for errors. Use stable codes from `ApiErrorCodes`.
- Long-running work returns **`202 Accepted` + a `jobId`** and is enqueued to Hangfire — never executed in the request thread.

---

## 6. Error handling, logging & correlation

- Unhandled exceptions are caught by `ExceptionHandlingMiddleware` → `500` with an `INTERNAL_ERROR` envelope carrying the correlation id. Detail is exposed only when `ErrorHandling:IncludeExceptionDetails` is on (default: Development).
- Every request gets an `X-Correlation-Id` (`CorrelationIdMiddleware`), pushed into Serilog's `LogContext` so all log entries carry it.
- Use the injected `ILogger<T>`; let Serilog enrich (correlation id, service, environment). **Never log secrets** — the Authorization and `X-Api-Key` headers and credential fields are redacted.

Pipeline order (API): `CorrelationId → ExceptionHandling → RequestResponseLogging → Authentication → Authorization → TenantResolution → endpoints`.

---

## 7. Security

- Passwords: PBKDF2-SHA256, ≥100k iterations, per-user salt (`IPasswordHasher`). Never store or log plaintext. `GenerateTemporaryPassword()` returns a 16-char strong password (one of each character class).
- JWTs are RS256, signed and validated with the same key (`ISigningKeyProvider`). Claims: `sub`, `email`, `activeTenantId`, `role`, `tokenVersion`, `tenantAssignments`, and permission claims.
- Increment `tokenVersion` on password change, deactivation, email change, and logout to invalidate outstanding tokens.
- Tenant credentials are encrypted at rest via `ICredentialEncryptionService`; GET responses return masked indicators only.
- **Permission-based RBAC.** Gate endpoints with `[RequirePermission(Permissions.<Area><Action>)]` (keys live in `IntegrationHub.Shared.Security.Permissions`). Seeded system-role permission sets (`ForSuperAdmin`/`ForTenantAdmin`/`ForOperator`) are re-applied on every startup by `BootstrapSeeder`, so adding a key to the catalogue grants it to system roles without a data migration.
- **Super-Admin-only actions** (delete a `Person`, assign/remove a user's tenant role) carry an explicit `User.IsSuperAdmin()` guard in the controller *in addition to* `[RequirePermission]`, so they stay Super-Admin-only even if a custom role is granted the permission. Tenant Admins do **not** hold `persons.delete` or `roles.assign`.
- **Identity (WO-61):** a `User` holds auth/account data only; personal/contact/professional data lives on a linked `Person` master record (with `Address`/`Media`). Creating a user **promotes an existing `Person`** (`personId`) rather than creating identity inline.

---

## 8. Extending the platform

### Add a new external system (connector)

1. Define `I<System>Connector` + its DTOs in `Application/Abstractions/Connectors/<System>`.
2. Implement `<System>Connector` in `Infrastructure/Connectors/<System>`: resolve per-tenant credentials via `ITenantApiConfigurationService`, cache the auth token in-memory, and normalize HTTP failures with `ConnectorError` (transient → `IsRetriable = true`).
3. Register it scoped in `Infrastructure.DependencyInjection.AddConnectors`.

### Add a new integration flow

1. **Transformer** — extend `TransformerBase<TSource, TDest>` (Application/`<Feature>`). Implement `ExtractFields` / `BuildDestination`; fall back to source values when a mapping is absent. Register keyed by `(SourceSystem, DestinationSystem)` via `AddTransformer<…>`.
2. **Validator** — implement `IValidator<TPayload>` (non-short-circuiting: return all violations).
3. **Integration service** — orchestrate `fetch → validate → transform → write`; on transient failure call `IRetryQueueManager.RegisterFailureAsync`, mark the job `Completed`/`Failed`/`PartiallyFailed`, and write `IntegrationLog` + audit.
4. **MediatR command + handler** — `record …Command(Guid JobId) : IRequest<IntegrationFlowResult>`; the handler delegates to the service.
5. **Hangfire job** — add a class in `Infrastructure/Jobs` (`RunForJobAsync` for API-triggered, `RunRecurringAsync` to fan out across tenants). Register it scoped.
6. **Controller endpoint** — create a tenant-stamped `IntegrationJob`, enqueue the job via `IBackgroundJobClient`, return `202 + jobId`.
7. **Schedule** (if recurring) — add a `JobScheduleConfiguration` seed row; `HangfireJobScheduler` registers required jobs and errors on a missing schedule.

### Add an admin/query endpoint

Add a controller action gated by `[RequirePermission(Permissions.<key>)]`, a FluentValidation request DTO, repository query methods (use `IgnoreQueryFilters` + explicit `tenantId` for Super-Admin cross-tenant reads), and `[ProducesResponseType]` annotations. For an action that must be Super-Admin-only regardless of permissions, also guard with `if (!User.IsSuperAdmin()) return 403`.

### Add a permission

Add the key to `IntegrationHub.Shared.Security.Permissions` (`area.action`), include it in `All` and the relevant `For<Role>()` set(s). It is re-seeded onto system roles on the next startup. Mirror the key into the web side (`WEB/src/composables/usePermissions.js`) so the UI can gate controls.

---

## 9. Coding conventions

- C# latest, **nullable reference types enabled**, implicit usings enabled. Treat warnings seriously.
- `async`/`await` end-to-end; accept and propagate `CancellationToken`. Don't block on async (`.Result`/`.Wait()`).
- Prefer `sealed` classes; `internal` for Infrastructure types unless a host needs them.
- One public type per file; file name matches the type.
- Match the surrounding code's style — comment density, XML docs on public abstractions, naming (`I` prefix for interfaces, `…Options` for config, `…Repository`/`…Service` for roles).
- Constants over magic strings (see `Roles`, `AuthorizationPolicies`, `ApiErrorCodes`, `ConfigurationSections`).

---

## 10. Testing

- **Unit tests** (`tests/IntegrationHub.UnitTests`): xUnit + Moq + FluentAssertions. Mock all dependencies; test pure logic (validators, rule evaluator, backoff, hashing), services with mocked repos, and controllers by instantiating them with a mocked `ClaimsPrincipal` (`WithUser(...)`). `InternalsVisibleTo` exposes Infrastructure internals.
- **Integration tests** (`tests/IntegrationHub.IntegrationTests`): `WebApplicationFactory<Program>` against a dedicated SQL test DB; a shared collection fixture boots the app once. Cover endpoint auth/status codes, repository round-trips, and migration idempotency.
- RBAC **policy** enforcement (e.g. `SuperAdminOnly`) is verified by integration tests, not unit tests (policies aren't applied when a controller method is called directly).
- Run `dotnet test IntegrationHub.sln` before opening a PR; keep it green.

---

## 11. Git & workflow

- Branch off `main`; never commit directly to `main` for feature work.
- Conventional, descriptive commit messages; reference the work order (e.g. `WO-21: …`) where applicable.
- Keep the build and tests green in every commit. Run `dotnet build -c Release` and `dotnet test`.
- Don't commit secrets. `.gitignore` excludes `bin/`, `obj/`, `.vs/`; keep real credentials out of `appsettings.json`.

---

## 12. Configuration & secrets

- Bind config to strongly-typed `…Options` in `Shared.Configuration`; reference sections via `ConfigurationSections` constants.
- For local dev use `dotnet user-secrets`; for deployment use environment variables or a secrets manager for the connection string, RSA signing key, and bootstrap password.
- The Data Protection key ring is persisted to SQL so all instances share it — don't let it diverge across environments.
</content>
