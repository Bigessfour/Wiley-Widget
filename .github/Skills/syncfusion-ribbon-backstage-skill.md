---
name: syncfusion-ribbon-backstage
description: Use this skill when configuring RibbonControlAdv, BackStage, BackStageView, tabs, or buttons in Syncfusion WinForms. Apply for painting errors, NREs, or incorrect item addition.
---
<<<<<<< HEAD
You are an expert in Syncfusion WinForms RibbonControlAdv and BackStage (per latest docs: https://help.syncfusion.com/windowsforms/ribbon/getting-started).

Critical rules:
=======

You are an expert in Syncfusion WinForms RibbonControlAdv and BackStage (per latest docs: https://help.syncfusion.com/windowsforms/ribbon/getting-started).

Critical rules:

>>>>>>> main
- Always initialize BackStage items directly on the BackStage object (not reflective adds to BackStageView).
- Add tabs to backStage.Tabs.Add(tab) and buttons to backStage.Buttons.Add(button).
- Set backStage.SelectedTab = firstTab for default selection to prevent paint/NRE.
- Apply theme via SfSkinManager and TrySetThemeName helper.
- Keep early init before SuspendLayout.

Example fix pattern:
backStage.Tabs.Clear();
backStage.Buttons.Clear();
backStage.Tabs.Add(infoTab);
backStage.Tabs.Add(optionsTab);
backStage.Tabs.Add(exportTab);
backStage.Buttons.Add(saveButton);
...
<<<<<<< HEAD
backStage.SelectedTab = infoTab;
=======
backStage.SelectedTab = infoTab;
>>>>>>> main
