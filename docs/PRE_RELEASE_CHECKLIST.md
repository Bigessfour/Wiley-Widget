# Pre-Release Checklist for Wiley Widget (Desktop WinForms)

Before tagging a release (e.g., v0.1.1), verify:

## 1. Code & Build
- [ ] Run `WileyWidget: Build` (or `dotnet build --configuration Release`) – No errors/warnings.
- [ ] Trunk check: `trunk check --ci` – All linters/formatters pass (dotnet-format, StyleCop).
- [ ] Tests: Run `test` task (or `dotnet test`) – All pass (>0.3% coverage baseline; add if needed).
- [ ] Syncfusion Validation: Theme cascade works (SfSkinManager global, no manual colors); run UI tests if available.
- [ ] Self-Contained EXE: Manual build `dotnet publish --self-contained win-x64` – Single EXE runs standalone.

## 2. CI/CD
- [ ] GH Actions: Push to test branch – All workflows green (build-winforms, test-coverage, security-scan, release on tag).
- [ ] No secrets needed (desktop-only; ignore Azure if present).

## 3. Documentation
- [ ] README.md: Up-to-date (build/run, features, QuickBooks desktop setup).
- [ ] CHANGELOG.md: Entry for new version (e.g., "v0.1.1: Docs alignment, EXE packaging").
- [ ] Docs Scrub: No legacy (Azure/cloud/WPF/Blazor refs); active files focused (Syncfusion, QuickBooks, UI standards).
- [ ] CONTRIBUTING.md: Guidelines for PRs/tests.

## 4. Distribution
- [ ] Tag & Push: `git tag vX.Y.Z && git push origin vX.Y.Z` – Triggers release.yml (EXE artifact).
- [ ] Verify Release: GH Releases page has EXE download; test run on clean machine.
- [ ] Signing (Optional): If needed, add code signing cert to publish step.

## 5. Final Validation
- [ ] Local Run: EXE launches, themes apply, QuickBooks syncs (sandbox).
- [ ] No Secrets: Confirm no cloud deps (e.g., no AZURE_* env vars required).
- [ ] Archive: Tag artifacts in Releases; update version in csproj if semantic.

**Sign-off:** All checked? Merge to main, tag, done. For issues: Run diagnostics tasks (e.g., dotnet-trace).
