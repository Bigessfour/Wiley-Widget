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
