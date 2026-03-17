# Wiley Widget Testing Strategy

## Goal

Prove that Wiley Widget starts, the main shell works, the top-level panels can be shown, and the critical business surfaces expose their primary controls. This strategy favors reliable proof over broad but low-signal coverage.

## Source Of Truth

- Panel inventory comes from `PanelRegistry`.
- The required proof lane lives in `tests/WileyWidget.WinForms.Tests`.
- GitHub Actions must run test projects directly, not `dotnet test WileyWidget.sln`.

## Proof Lanes

### 1. PR Smoke Gate

Use `.github/workflows/build-winforms.yml`.

This gate must prove:

- the solution builds on Windows
- MainForm startup completes without exceptions
- every panel in `PanelRegistry` can be shown through production navigation
- critical panels expose their primary controls
- JARVIS renders through its dedicated FlaUI smoke test

This is the main pass/fail signal for pull requests.

### 2. Coverage Lane

Use `.github/workflows/test-coverage.yml`.

This lane is for business logic and integration coverage, not UI proof. It runs the real WinForms test project directly and excludes the smoke lane and real API tests.

### 3. Exploratory / Local UI Work

Keep the broader FlaUI tests and the separate `tests/WileyWidget.UiTests` project for local investigation and focused debugging. They are useful, but they are not the primary release gate.

## What Counts As Proof

- `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions`
- `PanelRegistryNavigationProofTests.PanelRegistry_AllPanels_CanBeShownThroughProductionNavigation`
- `Category=Smoke` tests in `tests/WileyWidget.WinForms.Tests`
- registry-driven all-panel navigation proof
- hard-fail checks for critical panel controls

## What Does Not Count As Proof

- `dotnet test WileyWidget.sln`
  The solution file does not include the test projects, so this is a false signal.
- stale `paneltest` task references
  The current task points at a missing project and should not be used as evidence.
- soft-skip FlaUI tests that `return` when a control is missing
  Keep them for exploration if useful, but do not rely on them as release proof.
- static reports under `Reports/` or older E2E docs
  They are snapshots, not executable proof.

## Panel Coverage Policy

Panels are split into three groups:

- all registry panels: must be proven by the smoke suite through `MainForm.ShowPanel(...)`
- critical business panels: must also prove primary controls after navigation
- special panels: JARVIS remains in the smoke lane with its dedicated render test

If a new panel is added to `PanelRegistry`, the smoke suite must classify it immediately. That keeps panel growth from silently escaping the proof path.

## Path Forward

1. Treat `build-winforms.yml` as the required PR gate.
2. Treat `test-coverage.yml` as the business-logic confidence lane.
3. Add new panel checks to the registry-driven smoke suite before adding more one-off FlaUI files.
4. Gradually replace soft-skip panel tests with hard-fail smoke or targeted integration tests.
5. Retire or merge `tests/WileyWidget.UiTests` after any unique value there has been ported into `tests/WileyWidget.WinForms.Tests`.
