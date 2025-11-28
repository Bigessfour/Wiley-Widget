# Testing & Database Integration Guide

## WinForms UI E2E tests (interactive)

- Status (from a local run): the WinForms UI E2E tests require a real WinForms executable to be present (or an interactive runner). When the executable is missing the tests will fail with FileNotFound or process-launch errors.
- Current defensive behavior: test code has been updated so tests may skip in non-interactive environments and will provide clearer guidance when the executable is missing.

When you need to run these tests locally or in CI, pick one of the following flows:

1) Run against a published WinForms executable
   - Build & publish the WinForms app (recommended):

     dotnet publish WileyWidget.WinForms\WileyWidget.WinForms.csproj --configuration Debug --framework net9.0-windows --output ./publish

   - Point tests at the published exe:

     - E2ETests reads environment variable INTEGRATION_EXE_PATH (preferred) — set it to the absolute path of the published exe (for example: C:\work\Wiley-Widget\WileyWidget.WinForms\publish\WileyWidget.WinForms.exe).
     - Dashboard-style tests look at WILEYWIDGET_EXE — set this to the published exe path.

2) Run interactively on a self-hosted runner (CI)
   - CI jobs that run interactive UI tests must run on a self-hosted Windows runner that can display UI (or an equivalent interactive runner).
   - The test run will skip UI tests unless explicitly opted in. To opt in in CI, set the env var:

     WILEYWIDGET_UI_TESTS=true

   - If you want a single variable to override the executable path in CI, set INTEGRATION_EXE_PATH to the published exe path.

Notes, troubleshooting and facts
- If the test runner cannot find the executable it will instruct you to either build/publish the WinForms app or set the env var INTEGRATION_EXE_PATH/WILEYWIDGET_EXE.
- Launch failures (Win32Exception) can happen on non-interactive runners; those will also be skipped when configured to do so.
- For CI: prefer a dedicated, interactive self-hosted runner for these tests — public shared hosted runners cannot run interactive UI tests safely.

---



This document describes how the test projects are organized and how to run the SQL Server / SQL Express integration tests locally and in CI.

## Test project layout (high level)

- tests/WileyWidget.Data.Tests — Unit tests for data layer (DbContext factory, startup state, mapping sanity). Uses InMemory or provider-specific setup depending on tests.
- WileyWidget.IntegrationTests — Integration tests which are lightweight by default (InMemory) but include SQL Express tests when a SQL connection is available. Tests are tagged with `[Trait("Category", "Database.SqlExpress")]`.
- tests/WileyWidget.Services.Tests — Service-layer unit/integration tests.
- tests/WileyWidget.WinForms.E2ETests — UI E2E tests for WinForms (existing project, uses automation tooling).

## SQL Express integration tests

By default the integration project `WileyWidget.IntegrationTests` uses `appsettings.test.json` which points to `InMemory`. To enable the SQL Server / SQL Express tests you have two options:

1) Provide a `TEST_SQL_CONNECTIONSTRING` environment variable pointing to a reachable SQL Server instance.

   Example (PowerShell):

   ```powershell
   $env:TEST_SQL_CONNECTIONSTRING = 'Server=localhost\SQLEXPRESS;Database=WileyWidgetTestDb;Trusted_Connection=True;TrustServerCertificate=True;'
   dotnet test WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj --filter Category=Database.SqlExpress
   ```

2) Start a SQL Server container (suitable for CI) and point tests at it. Example using SQL Server 2022 container:

   ```powershell
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 --name wiley-sql -d mcr.microsoft.com/mssql/server:2022-latest
   # Then set TEST_SQL_CONNECTIONSTRING to use the container
   $env:TEST_SQL_CONNECTIONSTRING = 'Server=localhost,1433;Database=WileyWidgetTestDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;'
   dotnet test WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj --filter Category=Database.SqlExpress
   ```

Notes:
- The integration test will early-return if the connection string looks like an in-memory sentinel (e.g. "InMemoryDb").
- The test will attempt to run EF migrations (idempotent) against the provided database and perform a small CRUD flow. Ensure a test database name is used, or let the script create a new test DB.

## CI recommendations (GitHub Actions)

- Add a job that starts a SQL Server container job step (as above), publishes a `TEST_SQL_CONNECTIONSTRING` secret or sets it in the job environment, and then runs the filtered database tests:

```yaml
jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Start SQL Server
        run: |
          docker run -d --name wiley-sql -e ACCEPT_EULA=Y -e SA_PASSWORD=$env:SA_PASSWORD -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
        env:
          SA_PASSWORD: ${{ secrets.SQL_SA_PASSWORD }}
      - name: Set connection string
        run: echo "TEST_SQL_CONNECTIONSTRING=Server=localhost,1433;Database=WileyWidgetTestDb;User Id=sa;Password=${{ secrets.SQL_SA_PASSWORD }};TrustServerCertificate=True;" >> $GITHUB_ENV
      - name: Run sql-express integration tests
        run: dotnet test WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj --filter Category=Database.SqlExpress
```

## Running everything locally

- Run all tests (unit + integration): `dotnet test WileyWidget.sln`
- Run only database integration tests (if you have SQL/SQLExpress available):
  - Set `TEST_SQL_CONNECTIONSTRING` as shown above
  - `dotnet test WileyWidget.IntegrationTests\WileyWidget.IntegrationTests.csproj --filter Category=Database.SqlExpress`

## Next steps & suggestions

- Add a dedicated `tests/WileyWidget.Data.Tests` project (done) for unit-testing AppDbContext factory and startup state.
- Add more mapping tests (verify indexes, constraints, check constraints) in `WileyWidget.Data.Tests` using the InMemory or a SQLite provider for deterministic behavior.
- Consider adding a small SQL Express setup step to CI to run the `Database.SqlExpress` tests on PRs.

---
