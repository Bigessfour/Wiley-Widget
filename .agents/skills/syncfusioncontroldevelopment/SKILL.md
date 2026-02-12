---
name: syncfusioncontroldevelopment
description: When evaluating Syncfusion WinForms controls, this skill provides a structured checklist, prescribed inspection methods, and actionable fixes to ensure controls follow Syncfusion and project theming rules.
---

# Syncfusion Control Development Skill

Purpose

- Provide a consistent, repository-auditable process for validating Syncfusion WinForms controls in views and forms.
- Enforce theming rules (SfSkinManager usage, `ThemeName` set, no manual BackColor/ForeColor except semantic statuses).
- Offer actionable remediation templates the agent can apply automatically or propose as patches.

Placement & Activation

- Store this file at `.agents/skills/syncfusioncontroldevelopment/SKILL.md` (already present).
- To enable agent skill usage, ensure the following VS Code settings are present (or add them to `.vscode/settings.json`):

```json
{
  "chat.useAgentSkills": true,
  "chat.agentSkillsLocations": [".agents/skills"]
}
```

Prescribed Methods (agent-facing)

- `scan_form_for_syncfusion(form_path: str) -> List[ControlFinding]`
  - Locate Syncfusion control types in the form and partial classes (`SfDataGrid`, `SfTreeGrid`, `SfListView`, `ButtonAdv`, etc.).
  - For each control generate a `ControlFinding` object describing file, control name, line, and list of detected issues.

- `verify_control_compliance(finding: ControlFinding) -> ControlFinding`
  - Check that `ThemeName` is set when required.
  - Verify `SfSkinManager.LoadAssembly(...)` is called at startup or appropriate location.
  - Detect manual `BackColor`/`ForeColor` assignments and mark them as violations (except allowed semantic status colors: `Color.Red/Green/Orange`).
  - Ensure `ThemeName` literals match configured theme names (e.g., `Office2019Colorful`, `HighContrastBlack`).

- `generate_fix_plan(finding: ControlFinding) -> FixPlan`
  - Suggest minimal in-place fixes (e.g., set `control.ThemeName = themeName;`, replace `control.BackColor = ...` with removal or semantic color mapping, wrap dynamic controls with `SfSkinManager.SetVisualStyle(control, themeName)`).

- `apply_fix_patch(fixPlan: FixPlan) -> PatchResult`
  - Produce an `apply_patch`-style diff the agent can commit or present as a PR.

- `report_results(results: List[ControlFinding], outputPath: str)`
  - Emit a JSON + human-readable summary listing all findings, severity, and suggested fixes. Example schema below.

Checklists (what to look for)

- Startup & Theme Assembly
  - `SkinManager.LoadAssembly(themeAssembly)` is invoked before applying themes.
  - `SfSkinManager.ApplicationVisualTheme` or `SfSkinManager.SetVisualStyle(form, themeName)` is used for form-level application.

- Per-control
  - `control.ThemeName` is set for Syncfusion controls that expose it (e.g., `sfDataGrid.ThemeName = themeName`).
  - No manual `BackColor`/`ForeColor` assignments on controls (violation), except semantic status labels.
  - For dynamically-added controls after form load, `SfSkinManager.SetVisualStyle(control, themeName)` is used.

- Non-violation exceptions
  - Semantic status colors (`Color.Red`, `Color.Green`, `Color.Orange`) for status indicators are allowed.

Enforcement Rules & Actions

- Severity: `error` for theme-avoidance violations (manual BackColor on core controls), `warning` for missing `ThemeName`.
- Fix types: `remove_manual_color`, `set_theme_name`, `wrap_with_sfskinmanager`, `load_theme_assembly`.

Example `ControlFinding` (JSON)

```json
{
  "file": "src/WileyWidget.WinForms/Forms/MainForm.cs",
  "line": 123,
  "control": "sfDataGrid1",
  "type": "SfDataGrid",
  "issues": ["BackColor assignment", "Missing ThemeName"]
}
```

Agent Workflow (how to use this skill)

1.  Run `scan_form_for_syncfusion` on target forms or entire `src/` tree.
2.  For each `ControlFinding` run `verify_control_compliance` to classify issues.
3.  Generate a `FixPlan` for high-severity items; apply safe automated fixes where trivial.
4.  Write `report_results` to `Reports/syncfusion-scan-<timestamp>.json` and present a short summary to the user.

Reporting & Output

- Output both JSON (machine-readable) and a short markdown summary listing top 10 violations and suggested patches.
- Include patch diff snippets for each auto-fix.

Implementation notes for the agent

- Prefer minimal, reversible changes; don't change runtime behavior beyond theming.
- When proposing removal of color assignments, prefer commenting the lines and adding a `// REVIEW: removed manual color to comply with SfSkinManager` note.
- Always run `dotnet build` after applying fixes and report any compiler errors back to the user.

References

- Syncfusion WinForms docs: https://help.syncfusion.com/windowsforms/overview

Example: ButtonAdv

- When inspecting a `ButtonAdv`, check for the following groups on the control's class recipe: Constructors, Fields, Properties, Events, Implements. Verify the control is used idiomatically and that theming is applied via `ThemeName` / `SfSkinManager` rather than manual colors.

---

End of skill file.
