# WileyWidget

![CI](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)

Single-user WPF application scaffold (NET 9) using Syncfusion WPF controls (v30.2.x) with pragmatic tooling.

## Features
- Syncfusion DataGrid + Ribbon (add your license key)
- MVVM (CommunityToolkit.Mvvm)
- NUnit tests + coverage
- CI & Release GitHub workflows
- Central versioning (`Directory.Build.targets`)
- Global exception logging to `%AppData%/WileyWidget/logs`
- Theme persistence (Fluent Dark / Light)
- User settings stored in `%AppData%/WileyWidget/settings.json`
- About dialog with version info
- Window size/position + state persistence
- External license key loader (license.key beside exe)
 - Status bar (item count + selected widget & price)
 - Theme change logging (recorded via Serilog)

## PowerShell Development Environment

This project includes a comprehensive PowerShell 7.5.2 compatible development environment with convenient aliases and functions.

### Quick Start

1. **Load the development profile**:
   ```powershell
   # From project root
   . .\WileyWidget.Profile.ps1

   # Or from scripts directory
   . .\scripts\Load-WileyWidgetProfile.ps1
   ```

2. **Available commands**:
   ```powershell
   ww-build     # Build the project
   ww-test      # Run tests
   ww-run       # Start the application
   ww-info      # Show project information
   ww-license   # Check Syncfusion license
   ww-edit-env  # Edit environment configuration
   ww-reset     # Reset development environment
   ```

### Environment Configuration

The project uses a `.env` file for environment variables:

```env
# Wiley Widget Environment Configuration
WILEY_WIDGET_ROOT=C:\Path\To\Project
WILEY_WIDGET_CONFIG=Release
MSBUILDDEBUGPATH=C:\Temp\MSBuildDebug
RUN_UI_TESTS=0
SYNCFUSION_LICENSE_KEY=your_license_key_here
```

### Profile Features

- **Approved Verbs**: All functions use Microsoft-approved PowerShell verbs
- **CamelCase**: Consistent camelCase naming convention
- **Error Handling**: Comprehensive error handling and verbose output
- **Environment Management**: Automatic loading of `.env` configuration
- **Build Integration**: Seamless integration with existing build scripts
- **Help System**: Full PowerShell help documentation for all functions

### Advanced Usage

```powershell
# Build with specific configuration
Invoke-WileyWidgetBuild -Configuration Debug -Clean

# Run tests with coverage
Invoke-WileyWidgetTest -IncludeUITests -Coverage

# Start application in background
Start-WileyWidgetApplication -Configuration Release

# Edit environment configuration
Edit-WileyWidgetEnvironment
```

## GitHub MCP Server Integration

WileyWidget includes GitHub Model Context Protocol (MCP) server integration for enhanced development workflows.

### Quick Setup

1. **Load the development profile**:
   ```powershell
   . .\WileyWidget.Profile.ps1
   ```

2. **Test MCP connection**:
   ```powershell
   .\scripts\Test-GitHub-MCP.ps1
   ```

3. **Migrate to remote server** (optional):
   ```powershell
   .\scripts\Migrate-MCP-To-Remote.ps1 -ServerUrl "https://your-mcp-server.com/github"
   ```

### Server Configuration Types

#### Local Server (Default)
- Runs via npx in VS Code
- No external dependencies
- Automatic startup with VS Code

#### Remote Server Options
- **HTTP/HTTPS**: Production deployments
- **WebSocket**: Real-time collaboration
- **Load Balanced**: High availability setups
- **Docker**: Containerized deployments

### Available Tools
- Repository management
- Issue tracking
- Pull request management
- File operations
- Git operations
- CI/CD pipeline management

### Configuration Files
- `scripts/GitHub-MCP-Setup.md` - Complete setup documentation
- `scripts/Remote-MCP-Config-Template.env` - Remote server template
- `scripts/Migrate-MCP-To-Remote.ps1` - Migration script
- `.vscode/settings.json` - VS Code MCP configuration

### Environment Variables
```powershell
# Required
GITHUB_PERSONAL_ACCESS_TOKEN=your_token_here

# Optional (for remote servers)
MCP_SERVER_URL=https://your-server.com/github
MCP_API_KEY=your_api_key
GITHUB_API_URL=https://api.github.com
```

See `scripts/GitHub-MCP-Setup.md` for detailed configuration options.

## Raw File References (machine-consumable)
| Purpose | Raw URL (replace OWNER/REPO if forked) |
|---------|----------------------------------------|
| Settings Service | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/Services/SettingsService.cs |
| Main Window | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/MainWindow.xaml |
| Build Script | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/scripts/build.ps1 |
| App Entry | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs |
| About Dialog | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/AboutWindow.xaml |
| License Loader Note | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs |

## License Key
Add to `App.xaml.cs` (uncomment line):
```csharp
SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY");
```
Reference: Syncfusion licensing docs.

## Build & Run (Direct)
```pwsh
dotnet build WileyWidget.sln
dotnet run --project WileyWidget/WileyWidget.csproj
```

## Preferred One-Step Build Script
```pwsh
pwsh ./scripts/build.ps1            # restore + build + test + coverage
pwsh ./scripts/build.ps1 -Publish   # also publish single-file output to ./publish
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64  # self-contained executable
```

## Versioning
Edit `Directory.Build.targets` (Version / FileVersion) or use release workflow (updates automatically).

## Logging
Structured logging via Serilog writes rolling daily files at:
`%AppData%/WileyWidget/logs/app-YYYYMMDD.log`

Included enrichers: ProcessId, ThreadId, MachineName.

Sample entry:
`2025-01-01T12:34:56.7890123Z [ERR] (pid:1234 tid:5) Unhandled exception (Dispatcher)`

Retention: last 7 daily log files. Minimum level: Debug (Microsoft overridden to Warning).

Startup, theme changes, license load, and unhandled exceptions are recorded.

## Commenting Standards
We prioritize clear, lightweight documentation:
- File Header (optional for tiny POCOs) kept minimal – class XML summary suffices.
- Public classes, methods, and properties: XML doc comments (///) summarizing intent.
- Private helpers: brief inline // comment only when intent isn't obvious from name.
- Regions avoided; prefer small, cohesive methods.
- No redundant comments (e.g., // sets X) – focus on rationale, edge cases, side-effects.
- When behavior might surprise (fallbacks, error swallowing), call it out explicitly.

Example pattern:
```csharp
/// <summary>Loads persisted user settings or creates defaults on first run.</summary>
public void Load()
{
	// Corruption handling: rename bad file and recreate defaults.
}
```

## Settings & Theme Persistence
User settings JSON auto-created at `%AppData%/WileyWidget/settings.json`.
Theme buttons update the stored theme immediately; applied on next launch.

## About Dialog
Ribbon: Home > Help > About shows version (AssemblyInformationalVersion).

## Release Flow
1. Decide new version (e.g. 0.1.1)
2. Run GitHub Action: Release (provide version)
3. Download zipped artifact from GitHub Release
4. Distribute

## Project Structure
```
WileyWidget/            # App
WileyWidget.Tests/      # Unit tests
WileyWidget.UiTests/    # Placeholder UI harness
scripts/                # build.ps1
.github/workflows/      # ci.yml, release.yml
CHANGELOG.md / RELEASE_NOTES.md
```

## Tests
```pwsh
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"
```
Coverage report HTML produced in CI (artifact). Locally you can install ReportGenerator:
```pwsh
dotnet tool update --global dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

### Coverage Threshold (CI)
CI enforces a minimum line coverage (default 60%). Adjust `COVERAGE_MIN` env var in `.github/workflows/ci.yml` as the test suite grows.

## Next (Optional)
- Theme integration (SkinManager)
- UI automation (FlaUI/WinAppDriver)
- Signing + updater

Nullable reference types disabled per guidelines.

## Contributing & Workflow (Single-Dev Friendly)
Even as a solo developer, a light process keeps history clean and releases reproducible.

Branching (Simple)
- main: always buildable; reflects latest completed work.
- feature/short-description: optional for riskier changes; squash merge or fast-forward.

Commit Messages
- Imperative present tense: Add window state persistence
- Group logically (avoid giant mixed commits). Small cohesive commits aid bisecting.

Release Tags
1. Run tests locally
2. Update version via Release workflow (or adjust `Directory.Build.targets` manually for pre-release experiments)
3. Verify artifact zip on the GitHub Release
4. Tag follows semantic versioning (e.g., v0.1.1)

Hotfix Flow
1. branch: hotfix/issue
2. fix + test
3. bump patch version via Release workflow
4. merge/tag

Code Style & Comments
- Enforced informally via `.editorconfig` (spaces, 4 indent, trim trailing whitespace)
- XML docs for public surface, rationale comments for non-obvious private logic
- No redundant narrations (avoid // increment i)

Checklist Before Push
- Build: success
- Tests: all green
- README: updated if feature/user-facing change
- No secrets (ensure `license.key` not committed)
- Logs, publish artifacts, coverage directories excluded

Future (Optional Enhancements)
- Add pre-push git hook to run build+tests
- Add code coverage threshold gate in CI
- Introduce analyzer set (.editorconfig rules) when complexity grows
