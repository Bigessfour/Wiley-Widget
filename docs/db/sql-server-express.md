# SQL Server Express — Local development and CI

This repository is configured to use Microsoft SQL Server Express as the default relational database for local development and production-lite deployments. There are no Azure/Cosmos DB dependencies or sinks in the codebase.

## Key points

- Default provider: Microsoft SQL Server (EF Core / `UseSqlServer`).
- Local fallback: In-memory provider is used for certain tests or degraded mode.
- Preferred server name for local dev: `.\\SQLEXPRESS` (SQL Server Express instance).
- Connection string key used by the library: `DefaultConnection`.

## Recommended connection string (Development)

```
Server=.\\SQLEXPRESS;Database=WileyWidgetDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30;
```

Use `Integrated Security / Trusted_Connection=True` for local dev to avoid storing credentials.

## Running EF Core migrations and applying the DB

1. Ensure you have SQL Server Express installed locally (or use Docker/CI SQL Server image).
2. From the solution root run migrations commands against the `WileyWidget.Data` project:

```pwsh
# restore & build first if needed
dotnet restore
dotnet build
# add migrations (if creating new migration) -- only needed when changing model
dotnet ef migrations add <Name> --project src/WileyWidget.Data --startup-project src/WileyWidget.Services
# apply migrations
dotnet ef database update --project src/WileyWidget.Data --startup-project src/WileyWidget.Services
```

> NOTE: Some projects may rely on `appsettings.*.json` or environment variables for `DefaultConnection` — AppDbContextFactory falls back to `.\\SQLEXPRESS` if missing.

## CI / Containerized testing

If running integration tests in CI or on non-Windows machines, use a SQL Server container instead of SQL Server Express. Example GitHub Actions service (Linux-compatible):

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      SA_PASSWORD: "YourStrong!Passw0rd"
      ACCEPT_EULA: "Y"
    ports:
      - 1433:1433
    options: >-
      --health-cmd " /opt/mssql-tools/bin/sqlcmd -S localhost -U SA -P YourStrong!Passw0rd -Q \"SELECT 1\" "
      --health-interval 10s
      --health-timeout 5s
      --health-retries 10
```

## Tests

- Unit tests and most integration tests use the in-memory provider for speed and isolation.
- Integration tests can be configured to use an actual SQL Server instance by overriding `DefaultConnection` in `appsettings.test.json` or via environment variables.

## Migration Troubleshooting

- If EF Core complains about mismatches, try `dotnet ef migrations remove` and re-add a clean migration after validating the model.
- Always back up your production database before applying migrations.

---

If you want, I can also add a `docker-compose` snippet for a SQL Server container to make local development reproducible across platforms.