# Development Handbook

Follow this checklist to set up and work on Wiley Widget locally.

## Environment Setup

1. Install the .NET 9 SDK (see `global.json` for the pinned version).
2. Install PowerShell 7.5+, Docker Desktop (optional but used for MCP tooling), and Node.js 18+ if you need MCP integrations.
3. Run the helper script:
   ```pwsh
   python ./scripts/dev-start.py
   ```
   The script provisions required tools and validates trunk integration.
4. Configure app secrets by setting environment variables or authoring `config/development/appsettings.json` overrides (never commit secrets).

Detailed database and infrastructure notes live in [Database Setup](../reference/database-setup.md) and [Dependency Risk Assessment](../reference/DEPENDENCY_RISK_ASSESSMENT.md).

## Daily Workflow

| Step                 | Command                                                               | Notes                                       |
| -------------------- | --------------------------------------------------------------------- | ------------------------------------------- |
| 1. Clean environment | `pwsh ./scripts/maintenance/cleanup-dotnet-processes.ps1`             | Clears stale `dotnet` hosts                 |
| 2. Restore packages  | `dotnet restore WileyWidget.sln`                                      | Uses central package management             |
| 3. Build fast        | `pwsh ./scripts/build/fast-build.ps1 -Configuration Debug`            | Wraps `dotnet build` with solution defaults |
| 4. Run tests         | `dotnet test WileyWidget.sln --no-build`                              | See [Testing Strategy](Testing.md)          |
| 5. Trunk checks      | `trunk check --ci`                                                    | Mirrors the CI pipeline                     |
| 6. Run local app     | `dotnet run --project src/WileyWidget.WinUI/WileyWidget.WinUI.csproj` | Launches the WinUI shell                    |

## Useful References

- [Build Optimization Guide](../reference/build-optimization-guide.md)
- [Dotnet Process Management](../reference/dotnet-process-management.md)
- [Nullability Migration Plan](../reference/NULLABILITY_MIGRATION.md)
- [Theme Configuration](../reference/THEME_CONFIGURATION.md)
