# WileyWidget Integration Tests

This project contains integration tests for WileyWidget. It includes:

- Sqlite-based EF Core tests (no Docker required)
- Optional PostgreSQL tests that use DotNet.Testcontainers to spin up ephemeral containers (requires Docker)

Running locally:

- Fast (Sqlite-only):
  dotnet test tests/WileyWidget.Integration.Tests/WileyWidget.Integration.Tests.csproj --filter "Category!=Postgres" -v minimal

- Full (includes Postgres-based tests):
  - Option A (recommended, portable): Use the helper scripts (PowerShell - works on Windows and Linux runners that have PowerShell):
    ./scripts/docker/start-postgres.ps1 -TimeoutSeconds 120
    dotnet test tests/WileyWidget.Integration.Tests/WileyWidget.Integration.Tests.csproj -v minimal
    ./scripts/docker/stop-postgres.ps1

  - Option B (direct docker-compose, cross-platform):
    docker compose --profile testing-postgres up -d db-postgres
    dotnet test tests/WileyWidget.Integration.Tests/WileyWidget.Integration.Tests.csproj -v minimal
    docker compose --profile testing-postgres down -v

Notes:
- Postgres tests detect Docker availability and will skip themselves if Docker is not available (so CI runners without Docker won't fail unexpectedly).
- There are two Postgres test styles in this project:
  - Testcontainers-based tests (start ephemeral containers via DotNet.Testcontainers)
  - Docker Compose based tests (use `db-postgres` service and the `PostgresComposeConnectivityTests` test that depends on the `Postgres Integration` collection)
- Respawn is available in the project and used in Postgres tests to reset database state between assertions. If you want to iterate quickly, prefer the compose flow so you can re-use a single DB instance and speed up test runs.

Security note:
- The Testcontainers-based fixture reads the Postgres password from the `POSTGRES_TEST_PASSWORD` environment variable. If that variable is not set, a cryptographically-secure, randomly generated temporary password (runtime-only) will be used so there are no hard-coded credentials in the repository.
- For CI runs you can (optionally) set a repository secret named `POSTGRES_TEST_PASSWORD` and reference it in your workflow for reproducible runs and easier debugging.

Example (GitHub Actions):
- Set `POSTGRES_TEST_PASSWORD` in repository secrets.
- The integration test workflow already forwards this secret to the job environment when present.
