# Wiley Widget Pre-Release Checklist

This checklist is for release sign-off, not for optimistic progress reporting. A release candidate is ready only when the items below are true in the current repository state.

Use this together with `docs/V1_0_RELEASE_SCOPE.md` and `docs/V1_0_BLOCKER_MATRIX.md` so the team is validating a frozen release slice instead of an open-ended product wish list.

## 1. Scope Is Frozen

- [x] The release slice is explicitly defined.
- [ ] Out-of-scope panels and workflows are deferred, hidden, or documented as known limitations.
- [ ] No unrelated cleanup or opportunistic refactors are riding with release fixes.

Current evidence as of 2026-03-18:

- `docs/V1_0_RELEASE_SCOPE.md` defines the current proposed v1.0 slice and stop-ship conditions.
- `docs/V1_0_BLOCKER_MATRIX.md` converts that slice into explicit blockers and proof lanes.

## 2. Build Health Is Green

- [x] `build` completes without unresolved errors.
- [ ] If analyzers are part of the release lane, analyzer output is reviewed and clean enough for sign-off.
- [ ] A release publish build succeeds and the artifact can be launched on a clean Windows machine.

Current evidence as of 2026-03-19:

- `dotnet build WileyWidget.sln -m:2` completed successfully.
- `build: fast` completed successfully again on 2026-03-19 after the JARVIS automation visibility fix.
- Analyzer/sign-off cleanliness is not yet complete: the current build still reports `CA2024` in `src/WileyWidget.Services/QuickBooksDesktopImportService.cs`.
- Release publish and clean-machine launch validation remain open.

## 3. Proof Is Meaningful

- [ ] Every release-blocking fix has proof that the intended behavior works.
- [ ] Every high-risk fix also has proof that previously working behavior still works.
- [ ] Shared method changes are covered by regression proof for their existing callers.
- [x] Shell changes are covered by startup, navigation, theme, docking, or layout proof as appropriate.
- [x] No filtered zero-test run is being counted as evidence.
- [x] No stale report, old screenshot, or historical `.trx` is being treated as current proof.

Current evidence as of 2026-03-19:

- `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions` passed 1/1.
- `PanelRegistryNavigationProofTests` passed 32/32.
- `JarvisChatFlaUiTests.JarvisChat_RendersNativeAssistView_WhenTabSelected` passed 1/1 after restoring the automation status marker for `WILEYWIDGET_UI_AUTOMATION_JARVIS`.
- Focused QuickBooks Desktop import proof passed 6/6 across `QuickBooksDesktopIifParserTests` and `QuickBooksDesktopImportServiceTests`.
- `MainFormTests` still fails 2 tests and skips 2 tests, so shared-method and high-risk shell regression proof is still incomplete for sign-off.

## 4. In-Scope Workflow Certification

- [x] Main startup completes without exceptions.
- [ ] Production navigation can open the in-scope panels.
- [ ] Critical business workflows for this release are exercised and verified.
- [x] Optional integrations included in the release have focused verification.
- [ ] In-scope panels are marked as Certified, Known Limitation, or Deferred using `Done_Checklist.md`.

Current evidence as of 2026-03-19:

- Startup proof is green: `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions` passed 1/1.
- Focused panel-registry proof is green: `PanelRegistryNavigationProofTests` passed 32/32, but a fresh explicit production-navigation rerun is still needed before checking the navigation item off strictly.
- Focused QuickBooks integration proof is green: `QuickBooksDesktopIifParserTests` and `QuickBooksDesktopImportServiceTests` passed 6/6.
- `Done_Checklist.md` exists as the certification rubric, but the in-scope panel state tracking is not yet recorded there as Certified, Known Limitation, or Deferred.

## 5. Docs Match Reality

- [x] `README.md` describes the current product and release posture.
- [x] `QUICK_START.md` only lists commands and scripts that exist now.
- [x] `CONTRIBUTING.md` reflects the stabilization workflow.
- [x] Internal workflow docs under `.vscode/` and `.github/` do not point at missing files.
- [x] Release notes or changelog entries describe what actually shipped.

Current evidence as of 2026-03-18:

- Verified referenced workflow docs exist under `.vscode/` and `.github/`.
- Verified `README.md`, `QUICK_START.md`, and `CONTRIBUTING.md` reflect the current stabilization workflow.
- `CHANGELOG.md` contains shipped documentation and release entries for the current desktop/WinForms posture.

## 6. Packaging And Distribution

- [ ] Packaging instructions are current.
- [ ] Signing requirements are understood for the release artifact.
- [ ] The output artifact, version tag, and distribution notes are consistent.

Current gap as of 2026-03-18:

- No fresh release publish artifact or clean-machine launch proof was recorded in this pass.

## 7. Stop-Ship Check

- [ ] No unresolved P0 or P1 defects remain for the release slice.
- [ ] No unexplained startup, layout, or panel-regression instability remains in the release lane.
- [ ] No contributor is relying on a known-false validation path for sign-off.

Current gap as of 2026-03-19:

- The dedicated JARVIS FlaUI blocker now has current passing proof, but the focused `MainFormTests` lane still has 2 failures: `LayoutPersistence_SavesOpenDocumentIdentity_WithoutPersistingNativeMdiGeometry` and `OnFirstChanceException_IgnoresThemeExceptions_AndLogsOthers`.
- A fresh explicit production-navigation proof run is still needed if panel-construction proof is treated as insufficient for sign-off.
- Some previously defined VS Code proof tasks required repair because PowerShell was mis-parsing filter and logger arguments; do not treat older failed task runs as release evidence.

## Release Sign-Off Rule

Do not ship because the list of open items feels smaller. Ship when the current release slice is frozen, proven, documented truthfully, and repeatably validated.
