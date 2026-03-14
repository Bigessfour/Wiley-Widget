# Phase 2 Professional Panel Polish Plan

## Objective

Bring remaining panels to the same professional skeleton and runtime behavior now enforced in `ScopedPanelBase`.

## Priority Sequence

1. Runtime blockers first (JARVIS interop stability, Settings usability)
2. Factory enforcement sweep
3. Visual consistency audit

## Factory Migration Sweep (5–10 panels/day)

- `PanelHeader.cs`: replace any direct button instantiation with factory-created controls.
- `NoDataOverlay.cs`: replace any direct button/text input instantiation with factory-created controls.
- `CsvMappingWizardPanel.cs`: validate `SfDataGrid` creation and checklist compliance.
- Remaining panels: replace direct `new TextBox`, `new Button`, and `new Sf*` controls with `SyncfusionControlFactory`.

## Theming and Style Cleanup

- Remove manual `.BackColor`, `.ForeColor`, `.Font`, and ad-hoc `.Padding` overrides unless semantic status color exceptions apply.
- Keep `SfSkinManager` as the single source of truth for runtime theme changes.
- Ensure runtime-added controls receive current theme and `Dock = Fill` where required.

## Global Consistency Actions

- Add/verify a runtime theme applicator path in `ScopedPanelBase` for controls created after initial load.
- Ensure content surfaces are hosted in `ContentHost` and respect 12px interior padding contract.
- Validate panel headers use `PanelHeader` auto-title contract.

## Visual Audit Targets

- AnalyticsHub panel
- Budget panel
- QuickBooks panel
- Payments panel
- RevenueTrends panel

## Layout Token Audit Loop

- Run `python scripts/audit-layout-tokens.py --root . --scope full-ui --fail-on none` before each polish batch.
- Treat results as advisory and prioritize High findings in `QuickBooksPanel.cs`, `SettingsPanel.cs`, and `JARVISChatUserControl.cs` first.
- Optional persisted artifacts: `Reports/layout_tokens_audit.json` and `Reports/layout_tokens_audit.md`.

## Microsoft Design Principles Guardrail

- Keep panel updates aligned with `Effortless`, `Calm`, `Familiar`, and `Complete + Coherent` from the Windows 11 design principles guide.
- Prefer tokenization and existing platform behaviors over ad-hoc per-control styling.
- Ensure hierarchy remains clear: primary content first, transient overlays second, accent usage sparse and purposeful.

## Exit Criteria

- No direct non-factory creation of interactive controls in migrated panels.
- No clipping or raw/un-themed controls in target panels.
- Runtime theme switching updates all target panels without manual color code.
- Build and targeted panel tests pass.
