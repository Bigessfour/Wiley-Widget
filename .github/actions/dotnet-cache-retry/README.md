dotnet-cache-retry
===================

A small composite GitHub Action used to:

- cache NuGet artifacts and common build outputs (bin/obj) across runs
- optionally run a single command (e.g. `dotnet restore ...`, `dotnet build ...`, `dotnet test ...`) with retry semantics

Usage
-----

Add the action into any job step using `uses: ./.github/actions/dotnet-cache-retry` and provide any of these inputs:

- `cache-paths` (string, optional) — newline-separated paths/globs to cache (default contains `~/.nuget/packages`, `**/bin`, `**/obj`)
- `lockfile-globs` (string, optional) — glob pattern for files used to compute cache key (default `**/packages.lock.json`)
- `key-prefix` (string, optional) — cache key prefix (default `nuget-packages`)
- `attempts` (int, optional) — number of attempts for a provided `run-command` (default 3)
- `run-command` (string, optional) — an optional command the action will run with retries; useful for running `dotnet restore` or `dotnet test` with retry semantics

Examples
--------

Cache only:

```yaml
- name: Cache dotnet artifacts
  uses: ./.github/actions/dotnet-cache-retry
  with:
    lockfile-globs: '**/packages.lock.json'
    key-prefix: my-job-nuget
```

Cache + restore with retries:

```yaml
- name: Restore packages with retry
  uses: ./.github/actions/dotnet-cache-retry
  with:
    run-command: |
      dotnet restore WileyWidget.sln --verbosity minimal --use-lock-file
    attempts: '3'
```

Cache + run tests with retries (Windows-compatible):

```yaml
- name: Run tests with retry
  uses: ./.github/actions/dotnet-cache-retry
  with:
    run-command: |
      dotnet test tests/WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj --configuration Release --filter "Category=Database.SqlExpress"
    attempts: '3'
```

Notes
-----

- This is a lightweight helper intended to reduce duplication across multiple workflows (cache and retry logic live in one place).
- The action is cross-platform: it runs a Bash retry loop on Linux/macOS and a PowerShell retry loop on Windows.
- For multi-step operations (e.g., clean && restore && build) you may pass a semicolon-separated command or a multi-line block scalar.
