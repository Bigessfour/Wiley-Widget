---
name: syncfusion-theming
description: Use for any theme, color, or visual style changes in the Wiley-Widget WinForms app.
---
Strict rules (project-approved workflow):
- SfSkinManager is the ONLY theme authority.
- Apply once at form level: SfSkinManager.SetVisualStyle(form, "Office2019Colorful") or current DefaultTheme.
- Cascade handles all children — NO manual BackColor/ForeColor on controls.
- For Ribbon/BackStage: Set ThemeName property defensively.
- Default: Office2019Colorful (hard-coded Phase 1).
- Reference: https://help.syncfusion.com/windowsforms/themes/getting-started