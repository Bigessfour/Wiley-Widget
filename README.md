# WileyWidget

![CI](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)

Single-user WPF application scaffold (NET 9) using Syncfusion WPF controls (pinned v30.2.4) with pragmatic tooling.

## Current Status (v0.1.0)
- Core app scaffold stable (build + unit tests green on CI)
- Binary MSBuild logging added (`/bl:msbuild.binlog`) – artifact: `build-diagnostics` (includes `TestResults/msbuild.binlog` & `MSBuildDebug.zip`)
- Coverage threshold: 70% (enforced in CI)
- UI smoke test harness present (optional; not yet comprehensive)
- Syncfusion license loading supports env var, file, or inline (sample left commented)
- Logging: Serilog rolling files + basic enrichers; no structured sink beyond file yet
- Nullable refs intentionally disabled for early simplicity
- Next likely enhancements: richer UI automation, live theme switching via `SfSkinManager`, packaging/signing


## Quick Start
1. Clone: `git clone https://github.com/Bigessfour/Wiley-Widget.git`
2. Add Syncfusion license key: uncomment `SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY")` in `App.xaml.cs` (OR drop a `license.key` beside the exe for local dev). Verify with `pwsh ./scripts/show-syncfusion-license.ps1`.
3. Build & test: `pwsh ./scripts/build.ps1` (set `RUN_UI_TESTS=1` env var to include UI smoke tests)
4. Run: `dotnet run --project WileyWidget/WileyWidget.csproj`
5. Open logs (if needed): `explorer %AppData%\WileyWidget\logs`

Minimal enough that future-you won’t hate past-you.

## Features
- Syncfusion DataGrid + Ribbon (add your license key)
- MVVM (CommunityToolkit.Mvvm)
- NUnit tests + coverage
- CI & Release GitHub workflows
- Central versioning (`Directory.Build.targets`)
- Global exception logging to `%AppData%/WileyWidget/logs`
- Theme persistence (FluentDark / FluentLight)
- User settings stored in `%AppData%/WileyWidget/settings.json`
- About dialog with version info
- Window size/position + state persistence
- External license key loader (license.key beside exe)
 - Status bar: total item count, selected widget name & price preview
 - Theme change logging (recorded via Serilog)

## Syncfusion Setup
Pinned packages (NuGet):
```pwsh
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Licensing --version 30.2.4
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfGrid.WPF --version 30.2.4
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfSkinManager.WPF --version 30.2.4
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Tools.WPF --version 30.2.4
```
License placement (choose one):
1. Environment variable (recommended): set `SYNCFUSION_LICENSE_KEY` (User scope) then restart shell/app
	```pwsh
	[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','<your-key>','User')
	```
2. Provide a `license.key` file beside the executable (auto‑loaded)
3. Hard‑code in `App.xaml.cs` register call (NOT recommended for OSS / commits)

**WARNING:** Never commit `license.key` or a hard‑coded key – both are ignored/avoidance reinforced via `.gitignore`.

## Raw File References (machine-consumable)
| Purpose | Raw URL (replace OWNER/REPO if forked) |
|---------|----------------------------------------|
| Settings Service | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/Services/SettingsService.cs |
| Main Window | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/MainWindow.xaml |
| Build Script | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/scripts/build.ps1 |
| App Entry | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs |
| About Dialog | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/AboutWindow.xaml |
| License Loader Note | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs |

## Raw URLs (Machine Readability)
Direct raw links to key project artifacts for automation / ingestion:

- Project file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/WileyWidget.csproj
- Solution file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget.sln
- CI workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/ci.yml
- Release workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/release.yml
- Settings service: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/Services/SettingsService.cs
- License sample: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/LicenseKey.Private.sample.cs
- Build script: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/scripts/build.ps1

Replace REPO_OWNER/REPO_NAME with the actual GitHub org/repo when published.

## License Key (Inline Option)
If you prefer inline (e.g., private fork) uncomment and set in `App.xaml.cs`:
```csharp
SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY");
```
Official docs: https://help.syncfusion.com/common/essential-studio/licensing/how-to-register-in-an-application

## Build & Run (Direct)
```pwsh
dotnet build WileyWidget.sln
dotnet run --project WileyWidget/WileyWidget.csproj
```

## Preferred One-Step Build Script
```pwsh
pwsh ./scripts/build.ps1                               # restore + build + unit tests + coverage
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1                # include UI smoke tests
TEST_FILTER='Category=UiSmokeTests' pwsh ./scripts/build.ps1 -Config Debug  # ad-hoc filtered run
pwsh ./scripts/build.ps1 -Publish                      # publish single-file (framework-dependent)
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64  # self-contained executable
```
Tip: For always self-contained releases use: `pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64`.

## Versioning
Edit `Directory.Build.targets` (Version / FileVersion) or use release workflow (updates automatically).

## Logging
Structured logging via Serilog writes rolling daily files at:
`%AppData%/WileyWidget/logs/app-YYYYMMDD.log`

Included enrichers: ProcessId, ThreadId, MachineName.

Quick access: `explorer %AppData%\WileyWidget\logs`
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
Theme buttons update the stored theme immediately; applied on next launch (applied via planned `SfSkinManager` integration).

Environment override (tests / portable mode):
```pwsh
$env:WILEYWIDGET_SETTINGS_DIR = "$PWD/.wiley_settings"
```
If set, the service writes `settings.json` under that directory instead of AppData.

Active theme button is visually indicated (✔) and disabled to prevent redundant clicks.

Example `settings.json`:
```json
{
		"Theme": "FluentDark",
	"Window": { "Width": 1280, "Height": 720, "X": 100, "Y": 100, "State": "Normal" },
	"LastWidgetSelected": "WidgetX"
}
```

## About Dialog
Ribbon: Home > Help > About shows version (AssemblyInformationalVersion).

## Release Flow
1. Decide new version (e.g. 0.1.1)
2. Run GitHub Action: Release (provide version)
3. Download artifact from GitHub Releases page
4. Distribute (self-contained zip includes dependencies)

Artifacts follow naming pattern: `WileyWidget-vX.Y.Z-win-x64.zip`

Releases: https://github.com/Bigessfour/Wiley-Widget/releases

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

One-liner (local quick view):
```pwsh
dotnet test --collect:"XPlat Code Coverage" ; reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html ; start CoverageReport/index.html
```

### Coverage Threshold (CI)
CI enforces a minimum line coverage (default 70%). Adjust `COVERAGE_MIN` env var in `.github/workflows/ci.yml` as the test suite grows.

## Next (Optional)
- Integrate `SfSkinManager` for live theme switch (doc-backed pattern)
- UI automation (FlaUI) for DataGrid + Ribbon smoke
	- Basic smoke test already included: launches app, asserts main window title & UI children
- Dynamic DataGrid column generation snippet (future):
	```csharp
	dataGrid.Columns.Clear();
	foreach (var prop in source.First().GetType().GetProperties())
			dataGrid.Columns.Add(new GridTextColumn { MappingName = prop.Name });
	```
- Signing + updater
- Pre-push hook (see scripts/pre-push) to gate pushes

Nullable reference types disabled intentionally for simpler interop & to reduce annotation noise at this early stage (may revisit later).

## UI Tests
Basic FlaUI-based smoke test ensures the WPF app launches, main window title contains "Wiley" and a DataGrid control is present. Category: `UiSmokeTests`.

Run only smoke UI tests:
```pwsh
dotnet test WileyWidget.sln --filter Category=UiSmokeTests
```
Or via build script (set optional filter):
```pwsh
$env:TEST_FILTER='Category=UiSmokeTests'
pwsh ./scripts/build.ps1 -Config Debug
```
Enable unit + UI phases (separate runs) without manual filter:
```pwsh
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1
```
Notes:
- UI smoke tests optional; set RUN_UI_TESTS to include them, or filter manually.
- Script performs pre-test process cleanup (kills lingering WileyWidget/testhost) and retries up to 3 times.
- Automation sets `WILEYWIDGET_AUTOCLOSE_LICENSE=1` to auto-dismiss Syncfusion trial dialog when present.

## Troubleshooting
Diagnostics & common gotchas:
- File locking (MSB3026/MSB3027/MSB3021): build script auto-cleans (kills WileyWidget/testhost/vstest.console); verify no stray processes.
- MSB4166 (child node exited): binary log captured at `TestResults/msbuild.binlog`. Open with MSBuild Structured Log Viewer. Raw debug files (if any) archived under `TestResults/MSBuildDebug` + zip. A marker file keeps the folder non-empty for CI retention.
- Capture fresh logs manually:
	```pwsh
	$env:MSBUILDDEBUGPATH = "$env:TEMP/MSBuildDebug"
	pwsh ./scripts/build.ps1
	# Inspect TestResults/msbuild.binlog or $env:TEMP/MSBuildDebug/ for MSBuild_*.failure.txt
	```
- No UI tests discovered: use `--filter Category=UiSmokeTests` or set `RUN_UI_TESTS=1`.
- Syncfusion license not detected: run `pwsh ./scripts/show-syncfusion-license.ps1 -Watch`; ensure env var or `license.key` file present.
- Syncfusion trial dialog blocks exit: set `WILEYWIDGET_AUTOCLOSE_LICENSE=1` during automation.
- Coverage report missing: confirm `coverage.cobertura.xml` under `TestResults/`; install ReportGenerator for HTML view.
- Coverage threshold fail: adjust `COVERAGE_MIN` env var or pass `-SkipCoverageCheck` for exploratory build.

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

## Version Control Quick Start (Solo Flow)
Daily minimal:
```pwsh
git status
git add .
git commit -m "feat: describe change"
git pull --rebase
git push
```

Feature (risky) change:
```pwsh
git checkout -b feature/thing
# edits
git commit -m "feat: add thing"
git checkout main
git pull --rebase
git merge --ff-only feature/thing
git branch -d feature/thing
git push
```

Undo helpers:
- Discard unstaged file: `git checkout -- path`
- Amend last commit message: `git commit --amend`
- Revert a pushed commit: `git revert <hash>`

Tag release:
```pwsh
git tag -a v0.1.0 -m "v0.1.0"
git push --tags
```

## Extra Git Aliases
Moved to `CONTRIBUTING.md` to keep this lean.
