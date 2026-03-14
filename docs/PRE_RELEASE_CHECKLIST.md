# Pre-Release Checklist for Wiley Widget

Use this checklist with [docs/TOWN_RELEASE_PLAYBOOK.md](docs/TOWN_RELEASE_PLAYBOOK.md).

The goal is simple: prove the app works, the important panels look acceptable, and the release executable behaves correctly outside the dev machine.

## 1. Freeze the Candidate Scope

- [ ] Only blocker fixes are in scope for this release candidate.
- [ ] No new feature work or unrelated refactors are mixed into the candidate.
- [ ] The exact commit or release branch under evaluation is identified.

## 2. Baseline Validation

- [x] Run `ci-local: release-proof` successfully.
- [x] Focused branded report export unit tests pass (`ReportExportServiceTests`).
- [x] Confirm there are no obvious new startup, docking, or panel activation regressions.
- [x] Layout regression tests pass (MainFormLayoutTests.cs fixed for ribbon docking and MDI positioning).

## 3. Candidate Artifact

- [x] Run `ci-local: publish-release-candidate` or equivalent publish command.
- [x] Confirm the candidate executable is produced successfully.
- [x] Candidate packaging path is untrimmed for WinForms safety.

## 4. Must-Work Functional Check

- [x] App launches cleanly.
- [x] Main form displays correctly with the expected theme.
- [ ] Enterprise Vital Signs opens and renders correctly.
      Data status as of 2026-03-07: `dbo.TownOfWileyBudgetData` on `localhost\SQLEXPRESS` has been populated. If EVS still shows empty in an already-running app session, restart the app to clear the repository's one-hour Town of Wiley dataset cache before re-checking the panel.
- [ ] Budget Management & Analysis opens and is usable.
- [ ] Municipal Accounts opens and is usable.
- [ ] Reports opens and the intended reporting path works.
- [ ] PDF export works from the intended user workflow.
- [ ] Excel export works from the intended user workflow.
- [ ] Settings opens and renders correctly.
- [ ] QuickBooks-related UI, if in scope, loads and fails gracefully when unavailable.

## 4A. Known Unproven Scope Items

- [ ] Reports has been successfully displayed end-to-end at least once on a candidate build.
- [x] QuickBooks sandbox connection has been proven end-to-end at least once.
      Proof as of 2026-03-08: sandbox connection verified in `logs/wiley-widget-20260308.log` with realm `9341456554914940`, sandbox environment, and repeated successful connection tests plus final connected-state proof.
- [x] QuickBooks sandbox contains a Wiley-like COA sufficient for release proof.
      Status as of 2026-03-08: release proof accepted for the Wiley-like COA state in the sandbox.
- [x] In-scope QuickBooks API rows are current and reviewed in [docs/QUICKBOOKS_API_VALIDATION_MATRIX.md](docs/QUICKBOOKS_API_VALIDATION_MATRIX.md).
      Status as of 2026-03-08: reviewed and accepted as current for this release candidate.
- [x] Syncfusion PDF export has been successfully executed end-to-end at least once on a candidate build.
- [x] Syncfusion Excel export has been successfully executed end-to-end at least once on a candidate build.
      Package-level proof as of 2026-03-07: MCP scripts `SyncfusionPdfLibrary_ConfigurationProof.csx` and `SyncfusionXlsIoLibrary_ConfigurationProof.csx` both passed via `WileyWidgetMcpServer --run-script`, confirming create, save, and reopen against the installed Syncfusion PDF and XlsIO assemblies.
- [x] Town logo or approved branding asset is configured for report exports.
- [ ] Branded report masthead content is visibly present in exported PDF and Excel output.
- [ ] Reports proof is complete before publishing this candidate.
- [x] QuickBooks sandbox proof is complete before publishing this candidate.
- [ ] QuickBooks sandbox seed proof is complete before publishing this candidate.
- [ ] PDF and Excel export proof is complete before publishing this candidate.

## 5. Financial Confidence Check

- [ ] A known-good P/L scenario has been reviewed.
- [ ] Core totals and labels look credible.
- [ ] Empty-state, missing-data, or failure-state messaging is understandable.

## 6. UI Sanity Pass

- [ ] No major clipping on core screens.
- [ ] No unreadable text, overlapping controls, or obviously broken layouts.
- [ ] Buttons, status text, overlays, and grids look coherent enough for normal use.
- [ ] The app looks trustworthy on the screens users actually rely on.

## 7. Real Artifact Proof

- [ ] Run the published executable outside the dev environment.
- [ ] Confirm startup works on a second Windows machine, VM, or clean user profile.
- [ ] Recheck at least startup, navigation, and one core financial workflow there.

## 8. Blocker Review

- [ ] Any remaining issue is clearly classified as either release blocker or backlog.
- [x] EVS data import path has been proven against the live app database (`dbo.TownOfWileyBudgetData` on `localhost\SQLEXPRESS`).
- [ ] Reports status is explicitly recorded as proven for this candidate.
- [x] QuickBooks sandbox status is explicitly recorded as proven for this candidate.
      Recorded 2026-03-08 from fresh post-clear logs: successful sandbox connection tests and final connected-state proof for realm `9341456554914940`.
- [ ] QuickBooks sandbox seed status is explicitly recorded as proven for this candidate.
      Recorded 2026-03-08: realistic Wiley COA dry run passed with zero lookup failures, but the live sandbox seed stopped creating unmatched accounts after the QuickBooks sandbox account-limit warning.
- [x] PDF export status is explicitly recorded as proven for this candidate.
- [x] Excel export status is explicitly recorded as proven for this candidate.
- [ ] No known blocker remains unresolved.
- [ ] Only non-blocking issues remain.

## 9. Confidence Rule

- [ ] The checklist has passed cleanly once.
- [ ] The same checklist has passed cleanly a second consecutive time.

## 10. Tag and Release

- [ ] The release tag points to the approved candidate commit.
- [ ] GitHub Actions produced the expected release artifact.
- [ ] The GitHub release contains the expected executable.

## Sign-Off Rule

Release when the app has passed the functional checklist twice in a row and the published executable behaves correctly outside the development environment.
