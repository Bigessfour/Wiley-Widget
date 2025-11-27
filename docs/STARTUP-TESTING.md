# Startup verification, tests and CI hardening (automated changes)

This document describes the changes implemented to strengthen the *startup* and *dashboard* slices, the tests added, and how CI was hardened to enforce E2E readiness.

## High-level changes implemented

1. Program startup license behavior aligned with orchestrator
   - `WileyWidget.WinForms.Program` no longer throws on missing Syncfusion license at startup.
   - A new helper `WileyWidget.WinForms.Services.LicenseHelper` centralizes discovery and registration of Syncfusion license keys.
   - This makes `Program` tolerant (trial mode) while still attempting to register a license when present.

2. Test coverage
   - Added `LicenseHelperTests` in `WileyWidget.Services.UnitTests` to verify license discovery logic.
   - Added `WinFormsDashboardViewModelCtorTests` to assert the WinForms `DashboardViewModel` constructor is safe when services fail (no unobserved exceptions) and that background loads set `ErrorMessage` appropriately.
   - Added a DI circular dependency detection test to `DiValidationServiceTests`.

3. Process-level verify mode & smoke script
   - Program now supports a `--verify-startup` argument. When used, the app starts hosted services (runs startup orchestration) and exits — no UI launched.
   - CI now runs a smoke verification script `scripts/verify-startup.ps1` against the published exe to validate real startup behavior.

4. CI hardening
   - `.github/workflows/build-winforms.yml` now:
     - Runs unit and integration tests with coverage collection (XPlat Code Coverage)
     - Runs Trunk validation via `trunk-io/trunk-action@v1` (CI check + upload)
     - Generates a coverage summary using `reportgenerator` and fails the build when line coverage is below 85%
     - Removes `continue-on-error` from key test steps (tests now fail the build when failing)
     - Adds a `ui-e2e` manual job scaffold for running interactive UI tests (WinAppDriver/FlaUI) on-demand.

5. UI E2E test scaffold
   - A new test project `tests/WileyWidget.WinForms.E2ETests` (FlaUI-based) contains a placeholder test (skipped by default). This provides a template for future automated UI E2E work.

## Files added / modified

- Added: `WileyWidget.WinForms/Services/LicenseHelper.cs`
- Modified: `WileyWidget.WinForms/Program.cs` (no longer throws on missing license; supports `--verify-startup`)
- Added: `WileyWidget.Services.UnitTests/LicenseHelperTests.cs`
- Added: `WileyWidget.Services.UnitTests/WinFormsDashboardViewModelCtorTests.cs`
- Updated: `WileyWidget.Services.UnitTests/DiValidationServiceTests.cs` (circular dependency test)
- Added: `tests/WileyWidget.WinForms.E2ETests/*` (E2E test project + scaffold)
- Added: `scripts/verify-startup.ps1`
- Updated: `.github/workflows/build-winforms.yml` (Trunk, coverage gating, verify-startup smoke test, UI E2E job)

## How to run the new smoke verification locally

1. Build & publish the WinForms app locally:

```powershell
# from repository root
dotnet publish WileyWidget.WinForms\WileyWidget.WinForms.csproj -c Release -o ./publish
```

2. Run the verification script:

```powershell
pwsh ./scripts/verify-startup.ps1 -ExePath ./publish/WileyWidget.WinForms.exe -TimeoutSeconds 20
```

The script will run the exe with `--verify-startup`, wait for it to complete and return a non-zero exit code when verification fails.

## Where to go next (recommended)

- Convert the manual Dashboard UI specs to automated UI tests using the FlaUI/WinAppDriver scaffold in `tests/WileyWidget.WinForms.E2ETests`.
- Decide whether to force license enforcement on production systems (currently behavior is tolerant). If you prefer strict enforcement, Program/Main and orchestrator tests should be updated to match the policy.
- Consider moving `LicenseHelper` to a more central (non-UI) project if it needs to be reused by non-WinForms hosts.

