# Changelog

All notable changes to Wiley Widget (self-contained WinForms desktop app).

## [v0.1.1] - 2026-02-28

### Documentation Alignment
- **Scrubbed Legacy**: Removed Azure/cloud refs (scripts/README.md, CHANGELOG); deleted 12 outdated docs (Blazor, WPF audits, cloud deploys).
- **Updated Core Docs**: README.md (WinForms structure, no XAML); CHANGELOG (consolidated); QuickBooks checklist (desktop-only).
- **Pre-Release Checklist**: New `docs/PRE_RELEASE_CHECKLIST.md` for build/test/tag validation.
- **Scripts Cleanup**: Focused on local (DB setup, license); no cloud scripts.

### CI/CD
- **release.yml**: Self-contained EXE packaging (win-x64, single-file); triggers on tags.

**Tag:** `v0.1.1` – Docs clean, desktop-ready.

---

## [v0.1.0] - 2026-02-27

### Theme Enforcement & CI Setup
- **Syncfusion Themes**: Patched ThemeService (SfSkinManager global sync); MainForm.Chrome refresh for owned forms/docking.
- **Tests Added**: ThemeServiceTests (ApplyTheme_SetsApplicationVisualTheme); MainFormTests (OnThemeServiceChanged_ReplaysThemeToOwnedForms).
- **CI/CD**: Workflows (build-winforms matrix: themes/docking; test-coverage; security-scan); Trunk.io pre-commit (dotnet-format/StyleCop).
- **Release**: Tagged v0.1.0; self-contained EXE via release.yml (no cloud).
- **Cleanup**: No manual colors; factory for controls; 0.3% coverage baseline.

**Tag:** `v0.1.0` – Stable WinForms baseline.

---

## [0.4.0] - 2025-11-09 (Historical)

### Bootstrapper Refactor
- **Removed**: 11 dead modules/services; TMP support; IUnitOfWork.
- **Added**: Partial App.cs split (DI/Lifecycle/Telemetry); theme race fix.
- **Updated**: Hardcoded modules (Core/QuickBooks); config caching.
- **LOC Reduction**: ~12k lines; tests rebuilt from scratch.

---

## [0.3.0] - 2025-11-08 (Historical)

### NuGet & Packages
- **Added**: AssemblyResolve handler; AI manifest schema.
- **Updated**: Syncfusion to 31.2.5; Serilog/FluentValidation bumps.
- **Fixed**: CS0246 errors; runtime FileNotFoundExceptions.

---

## [0.2.0] - 2025-10-28 (Historical)

### Architecture Cleanup
- **Removed**: 70+ scripts (migration/cloudflared/prod DB); legacy navigation.
- **Updated**: Threading (Dispatcher over Task.Run); navigation via IRegionManager.
- **Impact**: Cleaner MVVM; no dead code.

---

## [0.1.0] - 2025-08-12 (Initial)

- Scaffold: Syncfusion WinForms; MVVM; basic CI; build script.
