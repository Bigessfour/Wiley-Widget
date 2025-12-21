# Wiley Widget — Ship-Ready Checklist

**Generated:** 2025-12-20

## Overview

This document is a focused, actionable checklist to take Wiley Widget from final-development to ship-ready. Follow each step, run the commands provided, and mark items complete when verified. The checklist is organized by priority: Build → Tests → DI & Startup → Data → UI → Logs → Performance → Packaging → Docs → Smoke Test.

---

## Quick Summary — Next Focus Area

- Primary focus: Validation & Polish
- Priorities: fix known log warnings (e.g., missing `audit` icon), seed FY2026 data, ensure design-time ViewModel coverage, verify DI/startup order, and produce a clean release build and installer.

---

## 1. Build & Compile

- [x] Clean and build full solution with no errors (warnings acceptable but note them).

Commands:

```powershell
# Clean and build
dotnet clean "WileyWidget.sln"
dotnet build "WileyWidget.sln" --no-restore --configuration Debug --verbosity minimal
```

Result (2025-12-20 13:14 UTC):

- Command run: `dotnet clean` then `dotnet build` on `WileyWidget.sln`.
- Outcome: Build succeeded in 12.0s with 0 errors and 8 warnings.
- Notable warnings:
  - `EncryptedLocalSecretVaultService._disposed` is never assigned (CS0649).
  - Multiple CA2000/CA1307 analyzer warnings in tests (disposable handling / Assert.Contains overload).
  - MSB3277: Conflicts between versions of `Microsoft.CodeAnalysis.CSharp`.

Action items remaining for Phase 1:

- [ ] Address analyzer warnings (optional at shipping but recommended).
- [ ] Investigate MSB3277 binding redirect / package version conflicts if CI requires clean logs.

---

## 2. Unit & Integration Tests

- [ ] Run all unit tests and collect coverage.

Commands:

```powershell
# Run tests and collect coverage (platform may vary)
dotnet test "WileyWidget.sln" --verbosity minimal
```

Expectation:

- All tests pass. Aim for coverage >85% on core assemblies (ViewModels, Services, Data).

---

## 3. DI & Startup Validation

- [ ] Validate DI registrations and startup timeline ordering.

Commands:

```powershell
# Run WinForms app in verification mode (no interactive UI required)
dotnet run --project src/WileyWidget.WinForms -- --verify-startup --verbose > logs/verify-startup-$(Get-Date -Format 'yyyyMMdd-HHmmss').log 2>&1
Get-ChildItem logs/verify-startup-*.log | Sort-Object LastWriteTime | Select-Object -Last 1 | Get-Content -Tail 200
```

Checks:

- ✅ **DI validation COMPLETE (2025-12-20 18:06)**: Fixed missing `ICacheService` registration and enhanced validator to check **ALL 52 services** (was ~48). Results: 6 critical services, 9 repositories, 22 business services, 14 ViewModels, 1 form - all validated successfully in ~132ms.
- ✅ **Startup timeline order FIXED (2025-12-20 18:10)**: Corrected phase 11 (Splash Screen Hide) to execute **AFTER** phase 10 (Data Prefetch) completes. Previously executed too early, violating dependency chain. Now properly waits for ApplicationContext constructor (which runs MainForm Creation + Data Prefetch) before hiding splash.

**DI Changes Made:**

- Added `ICacheService` registration in [DependencyInjection.cs](src/WileyWidget.WinForms/Configuration/DependencyInjection.cs#L75-L76)
- Enhanced [WinFormsDiValidator.cs](src/WileyWidget.WinForms/Services/WinFormsDiValidator.cs) to validate: `ICacheService`, `IStartupTimelineService`, `IDepartmentExpenseService`, `IGrokRecommendationService`, `IDispatcherHelper`, `IPanelNavigationService`, `UIConfiguration`, `IWinFormsDiValidator`, and all 4 missing ViewModels.

**Timeline Fix:**

- Moved "Splash Screen Hide" in [Program.cs](src/WileyWidget.WinForms/Program.cs#L463) to execute AFTER `WileyWidgetApplicationContext` construction completes (which includes Data Prefetch phase).
- **Correct order:** License(1) → Theme(2) → WinForms(3) → DI(4-6) → MainForm(7) → Chrome(8) → Panel Mgmt(9) → Data Prefetch(10) → **Splash Hide(11)** → UI Loop(12)

If dependency violations appear, edit `Program.cs` phases order and re-run verification.

---

## 4. Data & Seeding

- [ ] Seed FY2026 budget data so dashboard is not empty.

Commands (example):

```powershell
# If a seeding script exists
pwsh scripts/seed-fy2025-data.ps1 -Year 2026
# Or run any provided seeder via host args
dotnet run --project src/WileyWidget.WinForms -- --seed-budget FY2026
```

Verification:

- Query the DB to confirm entries:

```powershell
# Example (SQL): adjust as needed
sqlcmd -S ".\\SQLEXPRESS" -Q "SELECT COUNT(*) FROM BudgetEntries WHERE FiscalYear = '2026'"
```

- Dashboard should populate with seeded entries.

---

## 5. UI / Designer Polish

- [ ] Ensure design-time partials exist for ViewModels and provide sample data.
- [ ] Ensure `NullLogger` and no-op commands exist in design-time partials to avoid designer exceptions.
- [ ] Validate `SfSkinManager` theme compliance (NO manual BackColor/ForeColor assignments except semantic status colors).

Commands / Checks:

```powershell
# Validate design-time ViewModels (script)
pwsh scripts/Validate-ViewModels.ps1

# Search for manual color assignments that violate policy
git grep -n "BackColor\s*=\|ForeColor\s*=" -- src | grep -v SfSkinManager || true
```

Fixes:

- For missing design-time data, add `<ViewModel>.DesignTime.cs` with `DesignModeHelper.IsDesignMode` guard, `NullLogger`, and no-op commands.
- For theme violations, remove manual colors and use `SfSkinManager.SetVisualStyle()` or `ThemeColors.ApplyTheme(this)`.

---

## 6. Logs & Runtime Cleanliness

- [ ] Eliminate recurring non-critical warnings (e.g., ThemeIconService unknown icon `audit`).

Fix pattern:

- Add a fallback or mapping in `ThemeIconService` to map `'audit'` → `'accounting'` (or another existing glyph).

Verification:

```powershell
# Run app and tail logs
dotnet run --project src/WileyWidget.WinForms > logs/startup-$(Get-Date -Format 'yyyyMMdd-HHmmss').log 2>&1 &
Get-ChildItem logs/startup-*.log | Sort-Object LastWriteTime | Select-Object -Last 1 | Get-Content -Wait
```

- Confirm the warning `Icon 'audit' unknown` no longer appears.

---

## 7. Performance & Diagnostics

- [ ] Verify startup time and runtime counters.

Commands:

```powershell
# Measure startup time roughly (or use built-in timing)
# Example: verify startup from verification run logs
Get-ChildItem logs/verify-startup-*.log | Sort-Object LastWriteTime | Select-Object -Last 1 | Get-Content | Select-String "Duration" -Context 0,2

# Live counters (requires dotnet tools)
dotnet-counters monitor -p <PID> System.Runtime
```

Expectation:

- Startup ~800–900ms; GC/CPU usage within acceptable bounds for typical runs.

---

## 8. Packaging & Release

- [ ] Produce a self-contained release publish for Windows x64.

Commands:

```powershell
dotnet publish src/WileyWidget.WinForms/WileyWidget.WinForms.csproj -c Release -r win-x64 --self-contained true -o publish/
```

- [ ] Build installer (if repo contains scripts):

```powershell
pwsh scripts/Build-Installer.ps1 -Configuration Release
```

---

## 9. Documentation & Repo Health

- [ ] Sync IDE rule files and verify no diffs.

Commands:

```powershell
pwsh scripts/tools/sync-rules-to-vscode.ps1 -Force
git status --porcelain
```

- [ ] Update `README.md` with release notes, usage and troubleshooting.

---

## 10. Final Smoke Test (manual)

- [ ] Launch the published app on a clean VM/host.
- [ ] Navigate all primary flows:
  - Open Dashboard, Accounts, Budget, Reports, Settings
  - Create/Edit/Delete a Budget entry
  - Export a report
  - Run quick export and verify file output
- [ ] Confirm no unhandled exceptions, no UI freezes, expected telemetry/logging behavior.

---

## Completion Criteria

- All checklist boxes are checked.
- `dotnet build` succeeds with 0 errors.
- `dotnet test` passes.
- DI/Startup verification passes (`--verify-startup`).
- UI Designer previews show representative data and no designer exceptions.
- Logs contain no recurring warnings (e.g., missing icons) and startup metrics meet targets.
- Published `publish/` output runs on target OS.

---

## Notes / Blockers

- Fix `ThemeIconService` mapping for `audit` (log noise).
- Seed FY2026 data to avoid empty dashboard.
- Confirm Syncfusion license key availability for designer and CI runs.

## Estimate

- Time: 2–6 hours depending on fixes required (seeding and icon fixes are quick wins).

---

## Next Steps

- If you want, I can:
  - Run the build and test commands and attach the logs.
  - Apply the `ThemeIconService` quick fix to map `audit` → `accounting`.
  - Run `scripts/Validate-ViewModels.ps1` and fix missing design-time patterns across ViewModels.

---

### Progress Log

- [2025-12-20 13:14 UTC] Phase 1 `Build & Compile` completed: `Build succeeded` (0 errors, 8 warnings). See "Result" details above. Next: Phase 2 `Unit & Integration Tests` (not started).
- [2025-12-20 13:42 UTC] Phase 3 `DI & Startup` updated: Fixed startup timeline ordering by moving `Splash Screen Hide` to after `WileyWidgetApplicationContext` creation so `Data Prefetch` runs first; added `scripts/testing/verify-startup-timeline.ps1`; ran PSScriptAnalyzer (no findings); executed verify-startup and confirmed timeline violation is resolved (no occurrences found in `logs/verify-startup-*.log`).

---

## Testing Documentation References

The following docs under `docs/testing/` contain canonical instructions, scripts, and CI patterns for running tests and UI validation. Consult these when running Phase 1 and CI pipelines.

- `docs/testing/MCP-CICD-INTEGRATION.md` — CI/CD integration patterns, pre-commit hooks, and pipeline examples.
- `docs/testing/MCP-COPILOT-PROMPTS.md` — Copilot prompt examples and EvalCSharp/ValidateFormTheme usage patterns.
- `docs/testing/MCP-IMPLEMENTATION-SUMMARY.md` — MCP server implementation and tool summaries.
- `docs/testing/MCP-INTEGRATION-GUIDE.md` — MCP workflows and recommended CI usage.
- `docs/testing/MCP-QUICK-REFERENCE.md` — Quick commands & power patterns for test runs.

---

## How to build and run tests (Phase 1 — recommended)

Follow these steps (also described in the testing docs above):

1. Restore + build the solution:

```powershell
dotnet restore WileyWidget.sln
dotnet build WileyWidget.sln --no-restore --configuration Debug --verbosity minimal
```

1. Run unit tests for the whole solution:

```powershell
dotnet test WileyWidget.sln --verbosity minimal
```

To run a specific test project (faster during iterative work):

```powershell
dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj --verbosity minimal
```

1. Run E2E UI tests (if environment is configured):

```powershell
# Enable UI tests environment variable and run
Set-Item -Path Env:WILEYWIDGET_UI_TESTS -Value 'true'
dotnet test tests/WileyWidget.WinForms.E2ETests/WileyWidget.WinForms.E2ETests.csproj --filter Category=UI
```

1. Use the MCP server tools for UI/theme validations (recommended in CI):

```powershell
# Build the MCP server
dotnet build tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj
# Example: run batch validations
dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj -- BatchValidateForms --outputFormat=text
```

---

## Known Phase 1 follow-ups (if tests/build report warnings)

- **MSB3277 (Roslyn version conflicts):** If `MSB3277` appears during build, try unifying Roslyn packages in `Directory.Packages.props` (we added `Microsoft.CodeAnalysis.CSharp` / `Microsoft.CodeAnalysis.Common` to 4.11.0). If conflicts persist, add an explicit `PackageReference` with the desired version to the test project(s) or update the transitive dependency causing the older version.

- **Analyzer warnings (CA2000 / CA1307):** Fix by disposing created `IDisposable` objects or adjusting asserts to use the recommended overloads. See the test files referenced in the warnings for quick edits.

---

_End of appended testing references and quick run guide._
