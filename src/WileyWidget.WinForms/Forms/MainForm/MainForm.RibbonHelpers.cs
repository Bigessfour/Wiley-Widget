using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Services;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private delegate void RibbonCommand();

    private static RibbonCommand CreatePanelNavigationCommand(MainForm form, PanelRegistry.PanelEntry entry, ILogger? logger)
    {
        // Returns a command that dynamically invokes ShowPanel<T> with the entry's PanelType
        return () =>
        {
            try
            {
                var showPanelMethod = typeof(MainForm)
                    .GetMethod(nameof(MainForm.ShowPanel),
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(string), typeof(DockingStyle) },
                        null);

                if (showPanelMethod != null)
                {
                    var genericMethod = showPanelMethod.MakeGenericMethod(entry.PanelType);
                    genericMethod.Invoke(form, new object[] { entry.DisplayName, entry.DefaultDock });
                }
                else
                {
                    logger?.LogWarning("ShowPanel method not found for {PanelName}", entry.DisplayName);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to navigate to panel {PanelName} from registry", entry.DisplayName);
            }
        };
    }

    private static void ApplyRibbonStyleForTheme(RibbonControlAdv ribbon, string themeName, ILogger? logger)
    {
        if (ribbon == null)
        {
            return;
        }

        try
        {
            ribbon.RibbonStyle = RibbonStyle.Office2016;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set RibbonStyle for theme {Theme}", themeName);
        }
    }

    private static string ResolveRibbonThemeName(string? candidateTheme, ILogger? logger)
    {
        var resolvedTheme = string.IsNullOrWhiteSpace(candidateTheme)
            ? SfSkinManager.ApplicationVisualTheme
            : candidateTheme;

        if (string.IsNullOrWhiteSpace(resolvedTheme))
        {
            resolvedTheme = AppThemeColors.DefaultTheme;
        }

        return AppThemeColors.ValidateTheme(resolvedTheme, logger);
    }

    private static void ConfigureRibbonAppearance(RibbonControlAdv ribbon, ILogger? logger)
    {
        if (ribbon == null)
        {
            return;
        }

        try
        {
            ribbon.BorderStyle = ToolStripBorderStyle.None;
            ribbon.ShowCaption = true;
            ribbon.QuickPanelVisible = true;
            ribbon.ShowQuickItemsDropDownButton = true;
            ribbon.ShowRibbonDisplayOptionButton = true;
            ribbon.AutoLayoutToolStrip = true;
            ribbon.QuickPanelImageLayout = PictureBoxSizeMode.StretchImage;
            ribbon.RibbonHeaderImage = RibbonHeaderImage.None;
            ribbon.MenuButtonVisible = true;
            ribbon.MenuButtonWidth = Math.Max(56, ribbon.MenuButtonWidth);
            ribbon.TouchMode = false;

            if (ribbon.SystemText != null)
            {
                if (string.IsNullOrWhiteSpace(ribbon.SystemText.QuickAccessDialogDropDownName))
                {
                    ribbon.SystemText.QuickAccessDialogDropDownName = "Start menu";
                }

                if (string.IsNullOrWhiteSpace(ribbon.SystemText.RenameDisplayLabelText))
                {
                    ribbon.SystemText.RenameDisplayLabelText = "&Display Name:";
                }
            }

            _ = ribbon.OfficeMenu;
            _ = ribbon.Header?.MainItems;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to configure ribbon appearance");
        }
    }

    private static void AttachRibbonLauncherHandlers(MainForm form, RibbonControlAdv ribbon, ILogger? logger)
    {
        if (form == null || ribbon?.Header == null)
        {
            return;
        }

        try
        {
            foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
            {
                if (tab?.Panel == null)
                {
                    continue;
                }

                foreach (Control control in tab.Panel.Controls)
                {
                    if (control is not ToolStripEx strip)
                    {
                        continue;
                    }

                    strip.LauncherClick -= form.OnRibbonGroupLauncherClick;
                    strip.LauncherClick += form.OnRibbonGroupLauncherClick;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to attach ribbon launcher handlers");
        }
    }

    private void OnRibbonGroupLauncherClick(object? sender, EventArgs e)
    {
        try
        {
            if (sender is ToolStripEx strip)
            {
                _logger?.LogInformation("Ribbon launcher clicked for group {GroupName}", strip.Name);
                ApplyStatus($"{strip.Text} options are not configured yet.");
            }
            else
            {
                _logger?.LogInformation("Ribbon launcher clicked");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Ribbon launcher action failed");
        }
    }

    private static void CompleteToolStripTabItemAPI(ToolStripTabItem tabItem, ILogger? logger)
    {
        if (tabItem == null)
        {
            return;
        }

        try
        {
            _ = tabItem.Font;
            _ = tabItem.Padding;
            _ = tabItem.Panel;
            _ = tabItem.Position;
            _ = tabItem.Selected;
            _ = tabItem.GetPreferredSize(new Size(200, 100));
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "ToolStripTabItem API verification failed");
        }
    }

    private static Syncfusion.Windows.Forms.BackStageView? CreateBackStage(MainForm form, RibbonControlAdv ribbon, ILogger? logger)
    {
        try
        {
            form.components ??= new System.ComponentModel.Container();
            var backStageView = new Syncfusion.Windows.Forms.BackStageView(form.components);
            var backStage = new Syncfusion.Windows.Forms.BackStage
            {
                BackStagePanelWidth = (int)DpiAware.LogicalToDeviceUnits(220f)
            };

            backStageView.BackStage = backStage;
            backStageView.HostForm = form;
            backStageView.HostControl = ribbon;

            var infoTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Info",
                Name = "BackStage_Info",
                ThemesEnabled = true
            };
            infoTab.Controls.Add(new Label
            {
                Text = "Wiley Widget",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            });

            var optionsTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Options",
                Name = "BackStage_Options",
                ThemesEnabled = true
            };

            var settingsButton = new SfButton { Text = "Application Settings", Width = 220, Height = 40 };
            settingsButton.Click += (_, _) =>
            {
                SafeExecute(() => SafeNavigate(form, "Settings", () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right), logger), "BackStage_Settings", logger);
                if (backStageView.BackStage != null)
                {
                    backStageView.BackStage.Visible = false;
                }
            };

            var optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20)
            };
            optionsPanel.Controls.Add(settingsButton);
            optionsTab.Controls.Add(optionsPanel);

            var separator = new Syncfusion.Windows.Forms.BackStageSeparator
            {
                Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom
            };

            var exitButton = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Exit",
                Name = "BackStage_Exit",
                Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom
            };
            exitButton.Click += (_, _) => SafeExecute(() => form.Close(), "BackStage_Exit", logger);

            backStage.Controls.Clear();
            backStage.Controls.Add(infoTab);
            backStage.Controls.Add(optionsTab);
            backStage.Controls.Add(separator);
            backStage.Controls.Add(exitButton);
            backStage.SelectedTab = infoTab;

            return backStageView;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to create BackStage view");
            return null;
        }
    }

    private static ToolStripEx CreateRibbonGroup(string title, string name, string theme, ILogger? logger)
    {
        var strip = new ToolStripEx
        {
            Name = name,
            Text = title,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = true,
            LauncherStyle = LauncherStyle.Metro,
            ShowLauncher = true,
            ImageScalingSize = new Size(32, 32),
            ThemeName = ResolveRibbonThemeName(theme, logger),
            CanOverflow = false,
            Dock = DockStyle.None,
            LayoutStyle = ToolStripLayoutStyle.StackWithOverflow,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(2, 0, 2, 0),
            Office12Mode = false,
            Visible = true,
            Enabled = true,
            Font = new Font("Segoe UI", 9F)
        };

        try
        {
            SfSkinManager.SetVisualStyle(strip, strip.ThemeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set visual style for strip {Name}", name);
        }

        return strip;
    }

    private static ToolStripSeparator CreateRibbonSeparator()
    {
        return new ToolStripSeparator
        {
            AutoSize = false,
            Margin = new Padding(4, 0, 4, 0),
            Size = new Size(2, (int)DpiAware.LogicalToDeviceUnits(82f))
        };
    }

    private static ToolStripButton CreateLargeNavButton(string name, string text, RibbonCommand onClick, ILogger? logger, string? navigationTarget = null)
    {
        var button = new ToolStripButton(text)
        {
            Name = name,
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Padding = new Padding(8, 4, 8, 4),
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(85f), (int)DpiAware.LogicalToDeviceUnits(80f)),
            TextAlign = ContentAlignment.BottomCenter,
            Margin = new Padding(4, 2, 4, 2),
            Font = new Font("Segoe UI", 9F)
        };

        if (!string.IsNullOrWhiteSpace(navigationTarget))
        {
            button.Tag = $"Nav:{navigationTarget}";
        }

        button.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Ribbon button {ButtonName} failed", name);
            }
        };

        return button;
    }

    private static ToolStripButton CreateSmallNavButton(string name, string text, RibbonCommand onClick, ILogger? logger)
    {
        var button = new ToolStripButton(text)
        {
            Name = name,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(2, 1, 2, 1)
        };

        button.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Ribbon small button {ButtonName} failed", name);
            }
        };

        return button;
    }

    private static ToolStripButton CreateGalleryItem(string text, RibbonCommand onClick, ILogger? logger, string? navigationTarget = null)
    {
        var item = new ToolStripButton(text)
        {
            TextImageRelation = TextImageRelation.ImageAboveText,
            AutoSize = false,
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(85f), (int)DpiAware.LogicalToDeviceUnits(75f)),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Padding = new Padding(3, 1, 3, 1),
            Margin = new Padding(2, 1, 2, 1)
        };

        if (!string.IsNullOrWhiteSpace(navigationTarget))
        {
            item.Tag = $"Nav:{navigationTarget}";
        }

        item.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Gallery item {ItemText} failed", text);
            }
        };

        return item;
    }

    private static (ToolStripEx Strip, ToolStripButton DashboardBtn) CreateCoreNavigationGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Core Navigation", "CoreNavigationGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Core Navigation", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        ToolStripButton? firstButton = null;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            strip.Items.Add(button);
            firstButton ??= button;
        }

        return (strip, firstButton ?? CreateLargeNavButton("Nav_Empty", "No Panels", () => { }, logger));
    }

    private static (ToolStripEx Strip, ToolStripButton AccountsBtn) CreateFinancialsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Financials", "FinancialsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Financials", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        ToolStripButton? firstButton = null;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            strip.Items.Add(button);
            firstButton ??= button;
        }

        return (strip, firstButton ?? CreateLargeNavButton("Nav_Empty", "No Panels", () => { }, logger));
    }

    private static ToolStripEx CreateReportingGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Reporting", "ReportingGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Reporting", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            strip.Items.Add(button);
        }

        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton QuickBooksBtn, ToolStripButton SettingsBtn) CreateToolsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Tools", "ToolsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Tools", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        ToolStripButton? firstButton = null;
        ToolStripButton? settingsButton = null;
        ToolStripButton? quickBooksButton = null;

        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            strip.Items.Add(button);
            firstButton ??= button;

            if (panel.DisplayName == "Settings")
            {
                settingsButton = button;
            }

            if (panel.DisplayName == "QuickBooks")
            {
                quickBooksButton = button;
            }
        }

        return (strip, quickBooksButton ?? firstButton ?? CreateLargeNavButton("Nav_Empty", "No Tools", () => { }, logger), settingsButton ?? firstButton ?? CreateLargeNavButton("Nav_Empty", "No Tools", () => { }, logger));
    }

    private static (ToolStripEx Strip, ToolStripButton SaveLayoutBtn, ToolStripButton ResetLayoutBtn, ToolStripButton LockLayoutBtn) CreateLayoutGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Layout", "LayoutGroup", theme, logger);

        var saveLayoutBtn = CreateLargeNavButton("Nav_SaveLayout", "Save\nLayout", () => SafeExecute(form.SaveCurrentLayout, "SaveLayout", logger), logger);
        var resetLayoutBtn = CreateLargeNavButton("Nav_ResetLayout", "Reset\nLayout", () => SafeExecute(form.ResetLayout, "ResetLayout", logger), logger);
        var lockLayoutBtn = CreateLargeNavButton("Nav_LockPanels", "Lock\nPanels", () => SafeExecute(form.TogglePanelLocking, "LockPanels", logger), logger);

        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(resetLayoutBtn);
        strip.Items.Add(lockLayoutBtn);

        return (strip, saveLayoutBtn, resetLayoutBtn, lockLayoutBtn);
    }

    private static ToolStripEx CreateMoreGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Views", "MorePanelsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Views", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        foreach (var panel in panels)
        {
            var item = CreateGalleryItem(
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            strip.Items.Add(item);
        }

        return strip;
    }

    private static ToolStripEx CreateSearchAndGridGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Actions", "ActionGroup", theme, logger);

        var gridStack = new ToolStripPanelItem
        {
            Name = "ActionGroup_GridStack",
            RowCount = 2,
            AutoSize = true,
            Transparent = true
        };

        var sortAscBtn = CreateSmallNavButton("Grid_SortAsc", "Sort Asc", () => SafeExecute(() => form.SortActiveGridByFirstSortableColumn(false), "SortAscending", logger), logger);
        var sortDescBtn = CreateSmallNavButton("Grid_SortDesc", "Sort Desc", () => SafeExecute(() => form.SortActiveGridByFirstSortableColumn(true), "SortDescending", logger), logger);

        gridStack.Items.Add(sortAscBtn);
        gridStack.Items.Add(sortDescBtn);

        var searchBox = new ToolStripTextBox
        {
            Name = "GlobalSearch",
            AccessibleName = "Global Search Box",
            AccessibleRole = AccessibleRole.Text,
            AccessibleDescription = "Enter search query to find panels and content. Press Enter to search across all modules.",
            AutoSize = false,
            Width = 180,
            BorderStyle = BorderStyle.FixedSingle,
            ToolTipText = "Search panels (Enter to search)"
        };

        searchBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (string.IsNullOrWhiteSpace(searchBox.Text))
            {
                return;
            }

            form.GlobalIsBusy = true;
            try
            {
                await form.PerformGlobalSearchAsync(searchBox.Text);
            }
            finally
            {
                form.GlobalIsBusy = false;
            }
        };

        var searchStack = new ToolStripPanelItem
        {
            Name = "ActionGroup_SearchStack",
            RowCount = 2,
            AutoSize = true,
            Transparent = true
        };

        var label = new ToolStripLabel("Global Search:") { Name = "ActionGroup_SearchLabel" };
        searchStack.Items.Add(label);
        searchStack.Items.Add(searchBox);

        var themeCombo = new ToolStripComboBoxEx
        {
            Name = "ThemeCombo",
            Text = "Theme",
            AutoSize = false,
            Width = (int)DpiAware.LogicalToDeviceUnits(150f)
        };

        themeCombo.Items.AddRange(new object[]
        {
            "Office2019Colorful",
            "Office2019Dark",
            "Office2019White",
            "Office2019Black",
            "Office2019DarkGray",
            "Office2016Colorful",
            "Office2016White",
            "Office2016Black",
            "Office2016DarkGray"
        });

        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
        var index = themeCombo.Items.Cast<object>()
            .Select((item, i) => new { item, i })
            .Where(x => string.Equals(x.item?.ToString(), currentTheme, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.i)
            .DefaultIfEmpty(0)
            .First();
        themeCombo.SelectedIndex = index;

        themeCombo.SelectedIndexChanged += (_, _) =>
        {
            var selectedTheme = themeCombo.Text;
            if (string.IsNullOrWhiteSpace(selectedTheme))
            {
                return;
            }

            try
            {
                form._themeService?.ApplyTheme(selectedTheme);
                if (form._themeService == null)
                {
                    AppThemeColors.EnsureThemeAssemblyLoadedForTheme(selectedTheme, logger);
                    SfSkinManager.ApplicationVisualTheme = selectedTheme;
                    SfSkinManager.SetVisualStyle(form, selectedTheme);
                    ApplyThemeRecursively(form, selectedTheme, logger);
                    form.PerformLayout();
                    form.Refresh();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to apply theme {Theme}", selectedTheme);
            }
        };

        strip.Items.Add(searchStack);
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(gridStack);
        strip.Items.Add(themeCombo);

        return strip;
    }

    private static ToolStripEx CreateFileGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("File", "FileGroup", theme, logger);

        var newBudgetBtn = CreateLargeNavButton("File_NewBudget", "New\nBudget", () => SafeExecute(form.CreateNewBudget, "NewBudget", logger), logger);
        var openBudgetBtn = CreateLargeNavButton("File_OpenBudget", "Open\nBudget", () => SafeExecute(form.OpenBudget, "OpenBudget", logger), logger);
        var saveLayoutBtn = CreateLargeNavButton("File_SaveLayout", "Save\nLayout", () => SafeExecute(form.SaveCurrentLayout, "SaveLayout", logger), logger);
        var exportBtn = CreateLargeNavButton("File_ExportData", "Export\nData", () => SafeExecute(form.ExportData, "ExportData", logger), logger);

        strip.Items.Add(newBudgetBtn);
        strip.Items.Add(openBudgetBtn);
        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(exportBtn);

        return strip;
    }

    private static void AddToolStripToTabPanel(ToolStripTabItem tab, ToolStripEx strip, string theme, ILogger? logger)
    {
        if (tab?.Panel == null || strip == null)
        {
            logger?.LogWarning("AddToolStripToTabPanel skipped: tab.Panel or strip is null");
            return;
        }

        try
        {
            var resolvedTheme = ResolveRibbonThemeName(theme, logger);

            // Apply theme directly to the strip (Syncfusion best practice)
            strip.ThemeName = resolvedTheme;
            SfSkinManager.SetVisualStyle(strip, resolvedTheme);

            // Theme the tab panel container
            if (tab.Panel is Control panelControl)
            {
                SfSkinManager.SetVisualStyle(panelControl, resolvedTheme);
            }

            // Ensure items are visible/enabled before adding
            EnsureToolStripItemsVisibleAndEnabled(strip, logger);

            // ONLY use Syncfusion's managed API — no manual Controls.Add!
            // Using tab.Panel.AddToolStrip() ensures proper parent-child hierarchy
            // and prevents event routing issues from control reparenting.
            tab.Panel.AddToolStrip(strip);

            logger?.LogDebug("Successfully added ToolStrip '{StripName}' to tab '{TabText}' using AddToolStrip",
                strip.Name ?? "<unnamed>", tab.Text);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to add ToolStrip '{StripName}' to tab '{TabText}'",
                strip.Name ?? "<unnamed>", tab.Text);
        }
    }

    private static void ApplyThemeRecursively(Control root, string themeName, ILogger? logger)
    {
        if (root == null || string.IsNullOrWhiteSpace(themeName))
        {
            return;
        }

        try
        {
            SfSkinManager.SetVisualStyle(root, themeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to apply theme recursively to {ControlName}", root.Name);
        }

        if (root is ToolStripEx strip)
        {
            strip.ThemeName = themeName;
            EnsureToolStripItemsVisibleAndEnabled(strip, logger);
        }

        foreach (Control child in root.Controls)
        {
            ApplyThemeRecursively(child, themeName, logger);
        }
    }

    private static void EnsureToolStripItemsVisibleAndEnabled(ToolStripEx strip, ILogger? logger)
    {
        try
        {
            strip.Visible = true;
            strip.Enabled = true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to enable strip {StripName}", strip.Name);
        }

        foreach (ToolStripItem item in strip.Items)
        {
            EnsureToolStripItemVisibleAndEnabledRecursive(item, logger);
        }
    }

    private static void EnsureToolStripItemVisibleAndEnabledRecursive(ToolStripItem item, ILogger? logger)
    {
        try
        {
            item.Visible = true;
            item.Enabled = true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set item visibility for {ItemName}", item.Name);
        }

        if (item is ToolStripPanelItem panelItem)
        {
            foreach (ToolStripItem nested in panelItem.Items)
            {
                EnsureToolStripItemVisibleAndEnabledRecursive(nested, logger);
            }
        }

        if (item is ToolStripDropDownItem dropDown)
        {
            foreach (ToolStripItem nested in dropDown.DropDownItems)
            {
                EnsureToolStripItemVisibleAndEnabledRecursive(nested, logger);
            }
        }
    }

    private static void SafeExecute(RibbonCommand action, string operationName, ILogger? logger)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "{OperationName} failed", operationName);
        }
    }

    private static void SafeNavigate(MainForm form, string navigationTarget, RibbonCommand navigateAction, ILogger? logger)
    {
        if (form == null || form.IsDisposed || form.Disposing)
        {
            return;
        }

        var deferredDockingAttempts = 0;

        void PerformNavigation()
        {
            SafeExecute(() =>
            {
                if (form.IsDisposed || form.Disposing)
                {
                    return;
                }

                if (form.WindowState == FormWindowState.Minimized)
                {
                    form.WindowState = FormWindowState.Normal;
                }

                if (!form.Visible)
                {
                    form.Visible = true;
                }

                form.BringToFront();
                form.Activate();

                if (form._dockingManager == null)
                {
                    if (deferredDockingAttempts < 3)
                    {
                        deferredDockingAttempts++;
                        logger?.LogDebug("Ribbon navigation target '{Target}' deferred because docking manager is not ready (attempt {Attempt}/3)",
                            navigationTarget,
                            deferredDockingAttempts);

                        var retryTimer = new System.Windows.Forms.Timer { Interval = 150 };
                        retryTimer.Tick += (_, _) =>
                        {
                            retryTimer.Stop();
                            retryTimer.Dispose();

                            if (!form.IsDisposed && !form.Disposing)
                            {
                                PerformNavigation();
                            }
                        };
                        retryTimer.Start();
                    }
                    else
                    {
                        logger?.LogWarning("Ribbon navigation target '{Target}' could not run because docking manager is still unavailable", navigationTarget);
                    }

                    return;
                }

                form.EnsurePanelNavigatorInitialized();

                // Single navigation execution only.
                // MainForm.ExecuteDockedNavigation already owns retry/recovery behavior.
                navigateAction();

                if (!IsNavigationTargetActive(form, navigationTarget, logger))
                {
                    logger?.LogWarning("Ribbon navigation target '{Target}' was not activated on first attempt - retrying once", navigationTarget);
                    navigateAction();

                    if (!IsNavigationTargetActive(form, navigationTarget, logger))
                    {
                        logger?.LogWarning("Ribbon navigation target '{Target}' remained inactive after retry", navigationTarget);
                        form.PerformLayout();
                        form.Invalidate(true);
                        form.Refresh();
                    }
                }
            }, $"Navigate:{navigationTarget}", logger);
        }

        try
        {
            if (form.InvokeRequired)
            {
                form.BeginInvoke((MethodInvoker)PerformNavigation);
            }
            else
            {
                PerformNavigation();
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to dispatch navigation for '{Target}'", navigationTarget);
            PerformNavigation();
        }
    }

    private static bool IsNavigationTargetActive(MainForm form, string navigationTarget, ILogger? logger)
    {
        try
        {
            var panelNavigator = form.PanelNavigator;
            if (panelNavigator == null)
            {
                return true;
            }

            var activePanelName = panelNavigator.GetActivePanelName();
            if (string.IsNullOrWhiteSpace(activePanelName))
            {
                return false;
            }

            if (string.Equals(activePanelName, navigationTarget, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedActive = activePanelName.Replace(" ", string.Empty, StringComparison.Ordinal);
            var normalizedTarget = navigationTarget.Replace(" ", string.Empty, StringComparison.Ordinal);
            return string.Equals(normalizedActive, normalizedTarget, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed verifying navigation target '{Target}'", navigationTarget);
            return false;
        }
    }

    private static void InitializeQuickAccessToolbar(RibbonControlAdv ribbon, ILogger? logger, params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null)
        {
            return;
        }

        try
        {
            ribbon.QuickPanelVisible = true;
            ribbon.ShowQuickItemsDropDownButton = true;

            foreach (var button in buttons.Where(static b => b != null && b.Enabled))
            {
                try
                {
                    ribbon.Header.AddQuickItem(button);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Failed to add quick access button {ButtonName}", button.Name);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to initialize quick access toolbar");
        }
    }

    private static ToolStripButton[] CreateDefaultQuickAccessToolbarButtons(MainForm form, string theme, ILogger? logger)
    {
        var saveButton = new ToolStripButton
        {
            Name = "QAT_Save",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "Save",
            ToolTipText = "Save layout (Ctrl+Shift+S)",
            Enabled = true
        };
        saveButton.Click += (_, _) => SafeExecute(form.SaveCurrentLayout, "QAT_SaveLayout", logger);

        var undoButton = new ToolStripButton
        {
            Name = "QAT_Undo",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "Undo",
            ToolTipText = "Undo (not available yet)",
            Enabled = false
        };

        var redoButton = new ToolStripButton
        {
            Name = "QAT_Redo",
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "Redo",
            ToolTipText = "Redo (not available yet)",
            Enabled = false
        };

        return new[] { saveButton, undoButton, redoButton };
    }

    private static void AttachRibbonLayoutHandlers(MainForm form, RibbonControlAdv ribbon, ToolStripTabItem homeTab, ToolStripTabItem? layoutContextTab, ILogger? logger)
    {
        if (form == null || ribbon == null)
        {
            return;
        }

        ribbon.SizeChanged += (_, _) =>
        {
            if (form.IsDisposed || form.Disposing)
            {
                return;
            }

            try
            {
                form.BeginInvoke((MethodInvoker)(() =>
                {
                    form.PerformLayout();
                    homeTab.Panel?.PerformLayout();
                    layoutContextTab?.Panel?.PerformLayout();
                    form.Refresh();
                }));
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Ribbon layout refresh failed");
            }
        };
    }

    private static (ToolStripTabItem? Tab, ToolStripTabGroup? Group) TryCreateLayoutContextualTabGroup(MainForm form, RibbonControlAdv ribbon, string theme, ILogger? logger)
    {
        try
        {
            var layoutTab = new ToolStripTabItem { Text = "Layout", Name = "LayoutTab", Visible = false };
            var (layoutStrip, _, _, _) = CreateLayoutGroup(form, theme, logger);
            AddToolStripToTabPanel(layoutTab, layoutStrip, theme, logger);
            ribbon.Header.AddMainItem(layoutTab);
            return (layoutTab, null);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to create layout contextual tab");
            return (null, null);
        }
    }

    private static void ToggleLayoutContextualTab(RibbonControlAdv ribbon, ToolStripTabItem homeTab, ToolStripTabItem? layoutContextTab, ToolStripTabGroup? layoutTabGroup, ILogger? logger)
    {
        if (layoutContextTab == null)
        {
            return;
        }

        var makeVisible = !layoutContextTab.Visible;
        layoutContextTab.Visible = makeVisible;

        try
        {
            ribbon.SelectedTab = makeVisible ? layoutContextTab : homeTab;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to toggle layout contextual tab");
        }
    }
}
