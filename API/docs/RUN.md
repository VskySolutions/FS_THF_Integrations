# Running EMS Portal

How to build, configure, and run the platform locally, and how to verify it end-to-end.

See also: [README](../README.md) · [DEVELOPMENT](DEVELOPMENT.md) · [SCALAR](SCALAR.md)

---

## 1. Prerequisites

- **.NET 9 SDK** — the repo pins the SDK via `global.json`. Verify: `dotnet --version` → `9.0.x`.
- **SQL Server** — 2019+ or SQL Express (Azure SQL works as a drop-in). The platform creates its schema via EF Core migrations.
- (Optional) **sqlcmd** for the integration-test database.

---

## 2. Build

```bash
git clone https://github.com/VskySolutions/FS_THF_Integrations.git
cd FS_THF_Integrations
dotnet build EmsPortal.sln -c Release
```

---

## 3. Database & configuration

The platform uses one shared SQL Server database. Set the connection string for **both** the API and the Worker (`appsettings.json` → `ConnectionStrings:SqlServer`), or override via environment variables / user-secrets.

```jsonc
"ConnectionStrings": {
  "SqlServer": "Data Source=.\\SQLEXPRESS;Initial Catalog=EMS_Portal;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
}
```

You do **not** create the schema manually:

- The **API applies all EF Core migrations on startup** (`Database.Migrate()`). Hangfire and Serilog create their own tables on first use.
- On first run, a **bootstrap Super Admin** and a default `system` tenant are seeded from the `Bootstrap` section.

```jsonc
"Bootstrap": {
  "Email": "admin@integrationhub.local",
  "Password": "ChangeMe123!",
  "TenantIdentifier": "system",
  "TenantName": "System"
}
```

> **Change the bootstrap password** (and don't commit real secrets) before any shared/deployed environment. Prefer `dotnet user-secrets`, environment variables, or a secrets manager for the connection string, the RSA signing key, and the bootstrap password.

Other notable settings (`EmsPortal.Api`): `Authentication` (JWT/RS256 + token lifetimes), `ApiKeys`, `Hangfire`, `Retry`, `ExternalSystems`, `Serilog`, `ErrorHandling`. See the [README configuration table](../README.md#configuration).

---

## 4. Run the hosts

Start the **API first** (it owns migrations + seeding).

### API

```bash
# Development profile (recommended) — sets ASPNETCORE_ENVIRONMENT=Development, opens Scalar
dotnet run --project src/EmsPortal.Api
# → http://localhost:5032  (Scalar opens at /scalar/v1)
```

To run on a custom URL or force the environment explicitly (e.g. when not using the launch profile):

```bash
# bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/EmsPortal.Api --urls http://localhost:5080
# PowerShell
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project src/EmsPortal.Api --urls http://localhost:5080
```

### Worker (executes jobs)

The API only **enqueues** jobs; the Worker runs them. Start it to actually process imports and recurring schedules.

```bash
dotnet run --project src/EmsPortal.Workers
```

It boots the Hangfire server and the DB-driven scheduler (requires the same `ConnectionStrings:SqlServer`).

### MCP Server (optional)

```bash
dotnet run --project src/EmsPortal.McpServer
```

---

## 5. Environment differences

| Behavior | Development / Staging | Production |
|----------|-----------------------|------------|
| Scalar UI (`/scalar/v1`) + OpenAPI (`/openapi/v1.json`) | Exposed | Hidden (404) |
| Exception detail in 500 responses | On (unless `ErrorHandling:IncludeExceptionDetails=false`) | Off |
| Default log level | Debug | Information |

`ASPNETCORE_ENVIRONMENT` controls this (`Development`, `Staging`, or `Production`).

---

## 6. Verify it's running

```bash
# Health (anonymous)
curl http://localhost:5032/health/ready
# → 200, per-component status (sqlserver, concur, maconomy)

# Login as the bootstrap admin
curl -X POST http://localhost:5032/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@integrationhub.local","password":"ChangeMe123!"}'
# → { "data": { "accessToken": "...", "refreshToken": "..." } }
```

---

## 7. Trigger a flow end-to-end

```bash
TOKEN=...   # accessToken from login

# Enqueue an expense import (returns 202 + jobId)
curl -X POST http://localhost:5032/api/concur/expenses/import \
  -H "Authorization: Bearer $TOKEN"

# Watch the job (the Worker must be running to process it)
curl http://localhost:5032/api/admin/jobs -H "Authorization: Bearer $TOKEN"
```

- **Hangfire dashboard:** `http://localhost:5032/hangfire` (requires Tenant Admin or above).
- To actually reach Concur/Maconomy, store per-tenant credentials via `PUT /api/admin/tenants/{id}/concur-config` (and `…/maconomy-config`) and test them with `…/concur-config/test`.

---

## 8. Run the tests

```bash
dotnet test EmsPortal.sln              # all tests
dotnet test tests/EmsPortal.UnitTests  # unit only (no DB needed)
```

Integration tests boot the real app against a dedicated test database `EMS_Portal_Test` (created by migrations on first run). If your SQL login can't auto-create databases, pre-create it once:

```sql
IF DB_ID('EMS_Portal_Test') IS NULL CREATE DATABASE [EMS_Portal_Test];
```

---

## 9. Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `Cannot open database … login failed` | Connection string wrong, SQL not running, or the login lacks rights. For the test DB, pre-create it (section 8). |
| `/scalar/v1` returns 404 | App is running in **Production**. Use the `http` launch profile or set `ASPNETCORE_ENVIRONMENT=Development`. |
| Login returns 401 for the bootstrap admin | Seeder didn't run (a user already exists) or the `Bootstrap` password differs. Check the `Users` table / config. |
| Triggered import stays `Created` | The **Worker isn't running** — start `EmsPortal.Workers`. |
| Port already in use | Pass a free port with `--urls http://localhost:<port>`. |
| `dotnet --version` is not 9.x | Install the .NET 9 SDK; `global.json` pins it. |
</content>
