# Syncfusion Theming Compliance CI

This repository enforces strict Syncfusion theming rules for all WinForms code:

- No manual `BackColor`, `ForeColor`, or `Color.FromArgb` assignments (except for semantic status colors: `Color.Red`, `Color.Green`, `Color.Orange`).
- All theming must flow from `SfSkinManager` and the global theme cascade.
- Violations will block PRs and must be fixed before merge.

## How it works

- The workflow `.github/workflows/syncfusion-theming.yml` runs on every PR and push to `main`.
- It executes `scripts/check-syncfusion-theming.py` to scan all `src/WileyWidget.WinForms/**/*.cs` files for forbidden color assignments.
- If any violations are found, the workflow fails and reports the file/line in the PR.

## Fixing violations

- Remove all manual color assignments except for allowed semantic status colors.
- Use only theme-driven colors via `SfSkinManager` and `ThemeColors.ApplyTheme`.
- See `.vscode/copilot-instructions.md` for full rules and migration patterns.

## Example error

```
::error file=src/WileyWidget.WinForms/Controls/SomePanel.cs::Syncfusion theming violation(s):
  L42: this.BackColor = Color.FromArgb(30, 30, 30);
  L57: label1.ForeColor = Color.FromArgb(255, 100, 100);
::error ::1 file(s) have forbidden manual color assignments.
```

## Reference

- [Syncfusion WinForms Theming Guide](https://help.syncfusion.com/windowsforms/skins/getting-started)
- `.vscode/copilot-instructions.md` (project rules)
