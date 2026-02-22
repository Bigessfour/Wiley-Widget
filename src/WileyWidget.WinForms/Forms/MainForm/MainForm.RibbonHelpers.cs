using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Services;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// MainForm partial class containing RibbonControlAdv configuration and helper methods.
///
/// SYNCFUSION API REFERENCE: RibbonControlAdv (Syncfusion.Windows.Forms.Tools)
/// Documentation: https://help.syncfusion.com/windowsforms/ribbon/overview
/// Local Samples: C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19\Samples\Ribbon
///
/// RIBBONCONTROLADV KEY PROPERTIES (configured in this file):
/// ================================================================
///
/// CORE PROPERTIES:
/// - Name                              : Control identifier for automation/testing
/// - Dock                              : DockStyleEx.Top (ribbon docking position)
/// - Size, MinimumSize, MaximumSize    : DPI-aware sizing
/// - AutoSize                          : false (manual control)
/// - Visible, Enabled                  : Visibility and interaction state
/// - TabIndex, TabStop                 : Keyboard navigation order
///
/// APPEARANCE PROPERTIES:
/// - RibbonStyle                       : Office2016 (visual style)
/// - LauncherStyle                     : Metro (group launcher icon style)
/// - BorderStyle                       : None (frameless appearance)
/// - ShowCaption                       : true (show title bar)
/// - CaptionHeight                     : DPI-aware height for title bar
/// - RibbonHeaderImage                 : None (no custom header image)
/// - QuickPanelImageLayout             : StretchImage (QAT icon layout)
/// - TouchMode                         : false (optimize for mouse/keyboard)
/// - OfficeColorScheme                 : Derived from theme (Blue/White/Black/DarkGray)
///
/// LAYOUT PROPERTIES:
/// - LayoutMode                        : Normal/Simplified (ribbon layout mode)
/// - EnableSimplifiedLayoutMode        : true (allow simplified mode switching)
/// - ShowRibbonDisplayOptionButton     : true (show layout mode dropdown)
/// - AutoLayoutToolStrip               : true (automatic strip layout)
/// - AllowCollapse                     : true (allow ribbon collapse)
/// - AllowMinimize                     : true (allow ribbon minimize)
/// - IsMinimized                       : false (initial expanded state)
///
/// MENU BUTTON PROPERTIES (FILE MENU):
/// - MenuButtonVisible                 : true (show File button)
/// - MenuButtonEnabled                 : true (enable File button)
/// - MenuButtonText                    : "File"
/// - MenuButtonWidth                   : DPI-aware width (min 56px)
/// - MenuButtonFont                    : Segoe UI 9.75pt
/// - MenuButtonBackColor               : Office blue (#0072C6)
///
/// QUICK ACCESS TOOLBAR PROPERTIES:
/// - QuickPanelVisible                 : true (show QAT)
/// - ShowQuickItemsDropDownButton      : true (show QAT dropdown)
/// - QuickPanelImageLayout             : StretchImage
///
/// SYSTEM TEXT PROPERTIES (LOCALIZATION):
/// - SystemText.QuickAccessDialogDropDownName      : "Start menu"
/// - SystemText.RenameDisplayLabelText             : "&Display Name:"
/// - SystemText.AutoHideRibbon                     : "Auto-hide Ribbon"
/// - SystemText.ShowTabs                           : "Show Tabs"
/// - SystemText.ShowTabsAndCommands                : "Show Tabs and Commands"
/// - SystemText.AddToQuickAccessToolBarText        : "&Add to Quick Access Toolbar"
/// - SystemText.CustomizeQuickAccessToolBarText    : "&Customize Quick Access Toolbar..."
/// - SystemText.MinimizeTheRibbon                  : "&Minimize the Ribbon"
/// - SystemText.RemoveFromQuickAccessToolBarText   : "&Remove from Quick Access Toolbar"
///
/// ACCESSIBILITY PROPERTIES:
/// - AccessibleName                    : "Ribbon_Main"
/// - AccessibleDescription             : "Main application ribbon for navigation and tools"
/// - AccessibleRole                    : MenuBar
///
/// BEHAVIOR PROPERTIES:
/// - HideMenuButton                    : false (show File button)
/// - ShowKeyTips                       : true (show keyboard shortcuts)
///
/// THEME PROPERTIES:
/// - ThemeName                         : Office2019Colorful/Dark/Black/White (SfSkinManager theme)
/// - ThemeStyle                        : Office2019ColorfulTheme (loaded theme assembly)
///
/// OFFICE MENU PROPERTIES:
/// - OfficeMenu                        : Initialized but not populated (legacy)
///
/// HEADER PROPERTIES:
/// - Header.MainItems                  : Collection of ToolStripTabItem (tabs)
/// - Header.AddMainItem(tab)           : Add tab to ribbon
/// - Header.RemoveMainItem(tab)        : Remove tab from ribbon
///
/// BACKSTAGE PROPERTIES:
/// - BackStageView                     : BackStageView instance (File menu backstage)
/// - MenuButtonEnabled                 : true (enable backstage activation)
///
/// KEY EVENTS (available for subscription):
/// ================================================================
/// - RibbonClick                       : Fired when any ribbon item clicked
/// - LayoutModeChanged                 : Fired when layout mode changes (Normal/Simplified)
/// - IsMinimizedChanged                : Fired when ribbon minimize state changes
/// - OfficeMenuShown                   : Fired when Office menu (File) shown
/// - OfficeMenuHidden                  : Fired when Office menu (File) hidden
/// - LauncherClick                     : Fired when group launcher button clicked
///
/// KEY METHODS (available for invocation):
/// ================================================================
/// - PerformLayout()                   : Force layout recalculation
/// - Refresh()                         : Redraw ribbon
/// - BeginInit() / EndInit()           : Suspend/resume initialization
/// - SuspendLayout() / ResumeLayout()  : Suspend/resume layout updates
/// - ShowOfficeMenu()                  : Show File menu backstage
/// - HideOfficeMenu()                  : Hide File menu backstage
/// - Header.AddMainItem(tab)           : Add tab to ribbon
/// - Header.RemoveMainItem(tab)        : Remove tab from ribbon
///
/// IMPLEMENTATION NOTES:
/// ================================================================
/// - InitializeRibbon() in MainForm.Chrome.cs creates the RibbonControlAdv
/// - This file contains configuration helpers and group creation methods
/// - All properties configured in ConfigureRibbonAppearance()
/// - Navigation commands use PanelRegistry for panel discovery
/// - Theme applied via SfSkinManager.SetVisualStyle()
/// - DPI scaling via Syncfusion.Windows.Forms.DpiAware
/// </summary>
public partial class MainForm
{
    private delegate void RibbonCommand();

    /// <summary>
    /// Segoe MDL2 Assets icon glyph mappings for common ribbon buttons.
    /// Reference: https://learn.microsoft.com/windows/apps/design/style/segoe-ui-symbol-font
    /// </summary>
    private static readonly Dictionary<string, string> RibbonIconGlyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        // Administration tab
        ["Account Editor"] = "\uE70F",      // Edit/pen — account editor panel

        // Home — Core Navigation
        ["Enterprise Vital Signs"] = "\uE80F",  // Enterprise vital signs (replaces Dashboard)
        ["JARVIS Chat"] = "\uE720",              // Chat / Message bubble

        // Financials tab
        ["Budget"] = "\uE8F0",                          // Money/Calculator
        ["Budget Management & Analysis"] = "\uE8F0",    // Money/Calculator (full display name)
        ["Municipal Accounts"] = "\uE7EE",              // Contact list / Ledger
        ["Accounts"] = "\uE7EE",                        // Contact list
        ["Rates"] = "\uE8AB",                           // Chart bars
        ["Payments"] = "\uE8C7",                        // Payment / Check register
        ["QuickBooks"] = "\uE8F1",                      // Cloud / Sync

        // Analytics & Reports tab
        ["Analytics Hub"] = "\uEA24",                   // Analytics chart
        ["Revenue Trends"] = "\uE8E5",                  // Line chart trending
        ["Reports"] = "\uE8A1",                         // Document
        ["Department Summary"] = "\uE902",              // Building / Department
        ["War Room"] = "\uE7EF",                        // Strategy / Org chart
        ["Proactive AI Insights"] = "\uE8B9",           // Insights / Lightbulb

        // Utilities tab
        ["Customers"] = "\uE716",                       // Person / Contact
        ["Utility Bills"] = "\uE7BD",                   // Utilities / Invoice
        ["Recommended Monthly Charge"] = "\uE8AB",      // Chart bars (same as Rates)

        // Administration tab
        ["Settings"] = "\uE713",                        // Settings gear
        ["Data Mapper"] = "\uE8B7",                     // Map / Import
        ["Activity Log"] = "\uE81C",                    // Recent history / Clock
        ["Audit Log & Activity"] = "\uE838",            // Clipboard / Audit

        // File Operations
        ["New"] = "\uE8A5",                 // New document
        ["Open"] = "\uE8E5",                // Open folder
        ["Save"] = "\uE74E",                // Save
        ["Export"] = "\uEDE1",              // Export/Share

        // Layout
        ["Save Layout"] = "\uE74E",         // Save
        ["Reset Layout"] = "\uE7A7",        // Refresh/Reset
        ["Lock Panels"] = "\uE72E",         // Lock

        // Actions
        ["Sort Asc"] = "\uE74A",            // Sort ascending
        ["Sort Desc"] = "\uE74B"            // Sort descending
    };

    private static readonly Action<ILogger, string, string, string, Exception?> LogRibbonNav =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            default,
            "[RIBBON_NAV] → {PanelName} (Type={Type}, Dock={Dock})");

    /// <summary>
    /// Loads a colorful icon from Syncfusion's built-in resources or embedded project resources.
    /// Falls back to Segoe MDL2 Assets glyph if resource not found.
    ///
    /// ICON SOURCES (in priority order):
    /// 1. Syncfusion Essential Studio icons: C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19\Icons\
    /// 2. Embedded project resources: WileyWidget.WinForms.Resources.Icons.*
    /// 3. Segoe MDL2 Assets font glyphs (fallback)
    /// </summary>
    /// <param name="iconName">Icon name (e.g., "EnterpriseVitalSigns", "Budget", "Settings")</param>
    /// <param name="size">Icon size in pixels (16, 32, 48, etc.)</param>
    /// <param name="fallbackGlyph">Segoe MDL2 glyph to use if icon not found</param>
    /// <returns>Icon image or null if not found</returns>
    private static Image? LoadRibbonIcon(string iconName, int size, string? fallbackGlyph)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return fallbackGlyph != null ? CreateIconFromSegoeGlyph(fallbackGlyph, size, SystemColors.HotTrack) : null;
        }

        try
        {
            // Try loading from Icons\ folder beside the deployed EXE (deployment-safe — no dev machine path).
            var localIconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
            if (Directory.Exists(localIconsPath))
            {
                // Try exact size match
                var iconPath = Path.Combine(localIconsPath, $"{iconName}_{size}x{size}.png");
                if (File.Exists(iconPath))
                {
                    return Image.FromFile(iconPath);
                }

                // Try common variations
                var variations = new[] { iconName, iconName.Replace(" ", ""), iconName.ToLowerInvariant() };
                foreach (var variant in variations)
                {
                    iconPath = Path.Combine(localIconsPath, $"{variant}_{size}x{size}.png");
                    if (File.Exists(iconPath))
                    {
                        return Image.FromFile(iconPath);
                    }
                }
            }
        }
        catch
        {
            // Continue to next method
        }

        try
        {
            // Try loading from embedded project resources
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = $"WileyWidget.WinForms.Resources.Icons.{iconName}_{size}x{size}.png";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return Image.FromStream(stream);
            }
        }
        catch
        {
            // Continue to fallback
        }

        // Fallback: Use Segoe MDL2 Assets glyph with Office blue color for visual appeal
        if (!string.IsNullOrWhiteSpace(fallbackGlyph))
        {
            return CreateIconFromSegoeGlyph(fallbackGlyph, size, SystemColors.HotTrack);
        }

        return null;
    }

    /// <summary>
    /// Creates a navigation command delegate for a panel from PanelRegistry.
    /// Creates a navigation command using the generic ShowPanel method (no more 30-line if/else monster).
    /// This handles EVERY panel type uniformly, including Dashboard, JARVIS Chat, etc.
    /// </summary>
    private static RibbonCommand CreatePanelNavigationCommand(
        MainForm form,
        PanelRegistry.PanelEntry entry,
        ILogger? logger)
    {
        return () =>
        {
            try
            {
                if (logger != null)
                {
                    LogRibbonNav(logger, entry.DisplayName, entry.PanelType.Name, entry.DefaultDock.ToString(), null);
                }

                form.EnsurePanelNavigatorInitialized();

                // ONE LINE TO RULE THEM ALL
                form.ShowPanel(entry.PanelType, entry.DisplayName, entry.DefaultDock);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_NAV] Failed to navigate to {Panel}", entry.DisplayName);
            }
        };
    }

    /// <summary>
    /// Applies the appropriate RibbonStyle based on the current theme.
    ///
    /// RIBBON STYLES AVAILABLE (Syncfusion.Windows.Forms.RibbonStyle):
    /// - Office2007        : Classic Office 2007 appearance
    /// - Office2010        : Office 2010 appearance (flatter design)
    /// - Office2013        : Office 2013 appearance (flat, minimal)
    /// - Office2016        : Office 2016 appearance (modern, flat) ✓ USED
    /// - TouchStyle        : Touch-optimized large buttons
    /// - Metro             : Modern Metro/Modern UI style
    ///
    /// CURRENT IMPLEMENTATION: Uses theme-aware mapping (Office2007/2010/2013/2016).
    /// Office2019 themes map to Office2016 RibbonStyle per Syncfusion sample conventions.
    ///
    /// API REFERENCE: RibbonControlAdv.RibbonStyle property
    /// </summary>
    /// <param name="ribbon">RibbonControlAdv instance</param>
    /// <param name="themeName">Theme name (e.g., "Office2019Colorful")</param>
    /// <param name="logger">Logger for diagnostics</param>
    private static void ApplyRibbonStyleForTheme(RibbonControlAdv ribbon, string themeName, ILogger? logger)
    {
        if (ribbon == null)
        {
            return;
        }

        try
        {
            var resolvedTheme = themeName ?? string.Empty;
            var style = RibbonStyle.Office2016;

            if (resolvedTheme.Contains("Office2007", StringComparison.OrdinalIgnoreCase))
            {
                style = RibbonStyle.Office2007;
            }
            else if (resolvedTheme.Contains("Office2010", StringComparison.OrdinalIgnoreCase))
            {
                style = RibbonStyle.Office2010;
            }
            else if (resolvedTheme.Contains("Office2013", StringComparison.OrdinalIgnoreCase))
            {
                style = RibbonStyle.Office2013;
            }

            ribbon.RibbonStyle = style;
            logger?.LogDebug("RibbonStyle set to {RibbonStyle} for theme: {Theme}", style, resolvedTheme);
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

    /// <summary>
    /// Configures all RibbonControlAdv appearance and behavior properties per Syncfusion API.
    ///
    /// CONFIGURED PROPERTIES:
    /// - BorderStyle                      : ToolStripBorderStyle.None
    /// - ShowCaption                      : true (false in test mode)
    /// - QuickPanelVisible                : true (QAT visibility)
    /// - ShowQuickItemsDropDownButton     : true (QAT dropdown)
    /// - ShowRibbonDisplayOptionButton    : true (layout mode dropdown)
    /// - AutoLayoutToolStrip              : true (automatic strip layout)
    /// - QuickPanelImageLayout            : StretchImage (QAT icon sizing)
    /// - RibbonHeaderImage                : None (no custom header)
    /// - MenuButtonVisible                : true (File button)
    /// - MenuButtonEnabled                : true (File button interactive)
    /// - MenuButtonWidth                  : 56px minimum (DPI-aware)
    /// - TouchMode                        : false (optimized for mouse/keyboard)
    /// - AllowCollapse                    : true (ribbon can collapse)
    /// - SystemText.*                     : Localized strings for UI elements
    ///
    /// API REFERENCE: Syncfusion.Windows.Forms.Tools.RibbonControlAdv
    /// </summary>
    /// <param name="ribbon">RibbonControlAdv instance to configure</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="isUiTestRuntime">Whether running in UI test mode (disables interactive features)</param>
    private static void ConfigureRibbonAppearance(RibbonControlAdv ribbon, ILogger? logger, bool isUiTestRuntime)
    {
        if (ribbon == null)
        {
            return;
        }

        try
        {
            // === BORDER AND CAPTION ===
            ribbon.BorderStyle = ToolStripBorderStyle.None;
            ribbon.ShowCaption = !isUiTestRuntime;

            // === QUICK ACCESS TOOLBAR ===
            ribbon.QuickPanelVisible = !isUiTestRuntime;
            ribbon.ShowQuickItemsDropDownButton = !isUiTestRuntime;
            ribbon.QuickPanelImageLayout = PictureBoxSizeMode.StretchImage;

            // === LAYOUT AND DISPLAY OPTIONS ===
            ribbon.ShowRibbonDisplayOptionButton = !isUiTestRuntime;
            ribbon.AutoLayoutToolStrip = true;

            // === BEHAVIOR ===
            ribbon.AllowCollapse = true;

            // === HEADER IMAGE ===
            ribbon.RibbonHeaderImage = RibbonHeaderImage.None;

            // === MENU BUTTON (FILE MENU) ===
            ribbon.MenuButtonVisible = !isUiTestRuntime;
            ribbon.MenuButtonEnabled = !isUiTestRuntime;
            ribbon.MenuButtonWidth = Math.Max(56, ribbon.MenuButtonWidth);

            // === TOUCH MODE ===
            ribbon.TouchMode = false;

            // === SYSTEM TEXT (LOCALIZATION) ===
            if (ribbon.SystemText != null)
            {
                // Quick Access Toolbar text
                if (string.IsNullOrWhiteSpace(ribbon.SystemText.QuickAccessDialogDropDownName))
                {
                    ribbon.SystemText.QuickAccessDialogDropDownName = "Start menu";
                }

                // Display options text
                if (string.IsNullOrWhiteSpace(ribbon.SystemText.RenameDisplayLabelText))
                {
                    ribbon.SystemText.RenameDisplayLabelText = "&Display Name:";
                }
            }

            // === INITIALIZE COLLECTIONS (ensure properties are accessed) ===
            _ = ribbon.OfficeMenu;
            _ = ribbon.Header?.MainItems;

            logger?.LogDebug("RibbonControlAdv appearance configured successfully");
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

    /// <summary>
    /// Accesses all key ToolStripTabItem API properties to ensure complete initialization.
    /// Workaround for Syncfusion lazy initialization issues where properties aren't set until accessed.
    ///
    /// TOOLSTRIPTABITEM KEY PROPERTIES (Syncfusion.Windows.Forms.Tools ToolStripTabItem):
    /// - Font               : Tab text font
    /// - Padding            : Interior padding around tab content
    /// - Panel              : RibbonControlAdvPanel container for ribbon groups
    /// - Position           : Tab position (Left/Right/Top/Bottom)
    /// - Selected           : Whether tab is currently selected
    /// - GetPreferredSize() : Calculate minimum required size for tab content
    ///
    /// WHY THIS METHOD EXISTS:
    /// Syncfusion's RibbonControlAdv uses lazy initialization for performance.
    /// Some properties (especially Panel) aren't created until first access.
    /// This method forces initialization by accessing all key properties.
    ///
    /// API REFERENCE: Syncfusion.Windows.Forms.Tools ToolStripTabItem
    /// </summary>
    /// <param name="tabItem">ToolStripTabItem to initialize</param>
    /// <param name="logger">Logger for diagnostics</param>
    private static void CompleteToolStripTabItemAPI(ToolStripTabItem tabItem, ILogger? logger)
    {
        if (tabItem == null)
        {
            return;
        }

        try
        {
            // Access properties to force initialization
            _ = tabItem.Font;
            _ = tabItem.Padding;
            _ = tabItem.Panel;
            _ = tabItem.Position;
            _ = tabItem.Selected;
            _ = tabItem.GetPreferredSize(new Size(200, 100));

            logger?.LogDebug("ToolStripTabItem API initialization completed for tab: {TabText}", tabItem.Text);
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
                var settingsEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Settings");
                if (settingsEntry != null)
                {
                    SafeExecute((RibbonCommand)(() => SafeNavigate(form, "Settings", CreatePanelNavigationCommand(form, settingsEntry, logger), logger)), "BackStage_Settings", logger);
                }
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

            // File backstage tab: move common file operations here (New/Open/Save/Export)
            var fileTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "File",
                Name = "BackStage_File",
                ThemesEnabled = true
            };

            var filePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20)
            };

            var newBudgetButton = new SfButton { Text = "New Budget", Width = 220, Height = 40 };
#pragma warning disable CS0618
            newBudgetButton.Click += (_, _) => SafeExecute((RibbonCommand)form.CreateNewBudget, "BackStage_NewBudget", logger);

            var openBudgetButton = new SfButton { Text = "Open Budget", Width = 220, Height = 40 };
            openBudgetButton.Click += (_, _) => SafeExecute((RibbonCommand)form.OpenBudget, "BackStage_OpenBudget", logger);

            var saveLayoutButton = new SfButton { Text = "Save Layout", Width = 220, Height = 40 };
            saveLayoutButton.Click += (_, _) => SafeExecute((RibbonCommand)form.SaveCurrentLayout, "BackStage_SaveLayout", logger);

            var exportButton = new SfButton { Text = "Export Data", Width = 220, Height = 40 };
            exportButton.Click += (_, _) => SafeExecute((RibbonCommand)form.ExportData, "BackStage_ExportData", logger);
#pragma warning restore CS0618

            // Apply theme to backstage buttons
            try
            {
                var theme = ResolveRibbonThemeName(SfSkinManager.ApplicationVisualTheme, logger);
                SfSkinManager.SetVisualStyle(newBudgetButton, theme);
                SfSkinManager.SetVisualStyle(openBudgetButton, theme);
                SfSkinManager.SetVisualStyle(saveLayoutButton, theme);
                SfSkinManager.SetVisualStyle(exportButton, theme);
            }
            catch { }

            filePanel.Controls.Add(newBudgetButton);
            filePanel.Controls.Add(openBudgetButton);
            filePanel.Controls.Add(saveLayoutButton);
            filePanel.Controls.Add(exportButton);
            fileTab.Controls.Add(filePanel);

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
            exitButton.Click += (_, _) => SafeExecute((RibbonCommand)(() => form.Close()), "BackStage_Exit", logger);

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
            AutoSize = false,
            Height = (int)DpiAware.LogicalToDeviceUnits(110f),
            LauncherStyle = LauncherStyle.Metro,
            ShowLauncher = true,
            ImageScalingSize = new Size(40, 40),
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

    /// <summary>
    /// Creates an icon from a Segoe MDL2 Assets glyph character with Office-style blue gradient.
    /// Used as fallback when Syncfusion colorful icons are not available.
    /// </summary>
    /// <param name="glyph">Unicode glyph (e.g., "\uE8A5" for Dashboard)</param>
    /// <param name="size">Icon size in pixels</param>
    /// <param name="color">Icon color (defaults to Office blue if not provided)</param>
    /// <returns>Bitmap image of the icon</returns>
    private static Bitmap? CreateIconFromSegoeGlyph(string glyph, int size, Color color)
    {
        if (string.IsNullOrWhiteSpace(glyph) || size <= 0)
        {
            return null;
        }

        try
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                using (var font = new Font("Segoe MDL2 Assets", size * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    // Use Office blue color for professional appearance
                    var iconColor = color.IsEmpty || color == SystemColors.ControlText
                        ? Color.FromArgb(255, 0, 120, 215) // Office blue
                        : color;

                    using (var brush = new SolidBrush(iconColor))
                    {
                        var format = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        graphics.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
                    }
                }
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts Keys enumeration to keyboard shortcut display text.
    /// </summary>
    /// <param name="keys">Keyboard shortcut keys</param>
    /// <returns>Human-readable shortcut text (e.g., "Ctrl+Shift+D")</returns>
    private static string GetKeyboardShortcutText(Keys keys)
    {
        if (keys == Keys.None)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        if ((keys & Keys.Control) == Keys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((keys & Keys.Shift) == Keys.Shift)
        {
            parts.Add("Shift");
        }

        if ((keys & Keys.Alt) == Keys.Alt)
        {
            parts.Add("Alt");
        }

        var keyCode = keys & ~Keys.Modifiers;
        if (keyCode != Keys.None)
        {
            parts.Add(keyCode.ToString());
        }

        return string.Join("+", parts);
    }

    // Explicit short ribbon labels for entries whose display name is too long to fit.
    // Key = PanelRegistry DisplayName; Value = the text shown on the ribbon button (use \n for line breaks).
    private static readonly Dictionary<string, string> RibbonShortLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Budget Management & Analysis"] = "Budget\nMgmt",
        ["Municipal Accounts"] = "Municipal\nAccounts",
        ["Recommended Monthly Charge"] = "Recommended\nMonthly\nCharge",
        ["Proactive AI Insights"] = "Proactive\nAI Insights",
        ["Revenue Trends"] = "Revenue\nTrends",
        ["Department Summary"] = "Dept.\nSummary",
        ["Analytics Hub"] = "Analytics\nHub",
        ["Audit Log & Activity"] = "Audit Log",
        ["JARVIS Chat"] = "JARVIS\nChat",
        ["Utility Bills"] = "Utility\nBills",
        ["War Room"] = "War\nRoom",
    };

    private static string WrapRibbonText(string text, int maxCharsPerLine = 10)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Use explicit override if available
        if (RibbonShortLabels.TryGetValue(text, out var shortLabel))
            return shortLabel;

        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= maxCharsPerLine)
            {
                current.Append(' ').Append(word);
            }
            else
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Creates a large ribbon navigation button with icon, tooltip, and keyboard shortcut support.
    /// </summary>
    /// <param name="name">Button name for automation</param>
    /// <param name="text">Button display text</param>
    /// <param name="onClick">Command to execute on click</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="navigationTarget">Navigation target panel name</param>
    /// <param name="iconGlyph">Segoe MDL2 Assets icon glyph (e.g., "\uE8A5")</param>
    /// <param name="tooltip">Tooltip text (keyboard shortcut auto-appended)</param>
    /// <param name="shortcutKeys">Keyboard shortcut keys</param>
    /// <returns>Configured ToolStripButton</returns>
    private static ToolStripButton CreateLargeNavButton(
        string name,
        string text,
        RibbonCommand onClick,
        ILogger? logger,
        string? navigationTarget = null,
        string? iconGlyph = null,
        string? tooltip = null,
        Keys shortcutKeys = Keys.None)
    {
        var displayText = WrapRibbonText(text);
        var isMultiLine = displayText.Contains('\n');

        var button = new ToolStripButton(displayText)
        {
            Name = name,
            AutoSize = false,
            DisplayStyle = string.IsNullOrWhiteSpace(iconGlyph) ? ToolStripItemDisplayStyle.Text : ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Padding = new Padding(6, 4, 6, 4),
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(108f), (int)DpiAware.LogicalToDeviceUnits(96f)),
            TextAlign = ContentAlignment.BottomCenter,
            ImageAlign = ContentAlignment.TopCenter,
            Margin = new Padding(3, 2, 3, 2),
            ImageScaling = ToolStripItemImageScaling.None,
            AutoToolTip = true,
            Overflow = ToolStripItemOverflow.Never,
            Font = new Font("Segoe UI", isMultiLine ? 9F : 10.5F, FontStyle.Bold),
            AccessibleName = text,
            AccessibleRole = AccessibleRole.PushButton
        };

        // Create icon from Syncfusion resources or Segoe MDL2 Assets glyph
        if (!string.IsNullOrWhiteSpace(iconGlyph))
        {
            button.Image = LoadRibbonIcon(text, 40, iconGlyph);
        }

        // Set display style based on whether icon glyph was provided (not actual image load success)
        button.DisplayStyle = string.IsNullOrWhiteSpace(iconGlyph)
            ? ToolStripItemDisplayStyle.Text
            : ToolStripItemDisplayStyle.ImageAndText;

        // Build comprehensive tooltip with keyboard shortcut
        var tooltipText = tooltip ?? text;
        if (shortcutKeys != Keys.None)
        {
            var shortcutText = GetKeyboardShortcutText(shortcutKeys);
            tooltipText = $"{tooltipText} ({shortcutText})";
        }
        button.ToolTipText = tooltipText;
        button.AccessibleDescription = tooltipText;

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

    /// <summary>
    /// Creates a small ribbon button with icon and tooltip support.
    /// </summary>
    private static ToolStripButton CreateSmallNavButton(
        string name,
        string text,
        RibbonCommand onClick,
        ILogger? logger,
        string? iconGlyph = null,
        string? tooltip = null,
        Keys shortcutKeys = Keys.None)
    {
        var button = new ToolStripButton(text)
        {
            Name = name,
            DisplayStyle = string.IsNullOrWhiteSpace(iconGlyph) ? ToolStripItemDisplayStyle.Text : ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(2, 1, 2, 1),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AccessibleName = text,
            AccessibleRole = AccessibleRole.PushButton
        };

        // Create icon from Syncfusion resources or Segoe MDL2 Assets glyph
        if (!string.IsNullOrWhiteSpace(iconGlyph))
        {
            try
            {
                // Try to load colorful Syncfusion icon first, fall back to glyph
                button.Image = LoadRibbonIcon(text, 24, iconGlyph);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to create icon for small button {ButtonName}", name);
            }
        }

        // Build tooltip
        var tooltipText = tooltip ?? text;
        if (shortcutKeys != Keys.None)
        {
            var shortcutText = GetKeyboardShortcutText(shortcutKeys);
            tooltipText = $"{tooltipText} ({shortcutText})";
        }
        button.ToolTipText = tooltipText;
        button.AccessibleDescription = tooltipText;

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
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(100f), (int)DpiAware.LogicalToDeviceUnits(88f)),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Padding = new Padding(3, 1, 3, 1),
            Margin = new Padding(2, 1, 2, 1),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
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
        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");

            // Look up icon glyph for this panel
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");

            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
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
        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");

            // Look up icon glyph for this panel
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");

            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
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

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");

            // Look up icon glyph for this panel
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");

            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
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
        bool isFirst = true;

        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");

            // Look up icon glyph for this panel
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            // Assign keyboard shortcuts for common tools
            var shortcutKeys = panel.DisplayName switch
            {
                "Settings" => Keys.Control | Keys.Alt | Keys.S,
                "QuickBooks" => Keys.Control | Keys.Q,
                _ => Keys.None
            };

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel",
                shortcutKeys: shortcutKeys);

            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
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

#pragma warning disable CS0618
        var saveLayoutBtn = CreateLargeNavButton(
            "Nav_SaveLayout",
            "Save\nLayout",
            () => SafeExecute((RibbonCommand)form.SaveCurrentLayout, "SaveLayout", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Save Layout"),
            tooltip: "Save current panel layout",
            shortcutKeys: Keys.Control | Keys.Shift | Keys.S);

        var resetLayoutBtn = CreateLargeNavButton(
            "Nav_ResetLayout",
            "Reset\nLayout",
            () => SafeExecute((RibbonCommand)form.ResetLayout, "ResetLayout", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Reset Layout"),
            tooltip: "Reset panel layout to default");

        var lockLayoutBtn = CreateLargeNavButton(
            "Nav_LockPanels",
            "Lock\nPanels",
            () => SafeExecute((RibbonCommand)form.TogglePanelLocking, "LockPanels", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Lock Panels"),
            tooltip: "Lock/unlock panel positions");
#pragma warning restore CS0618

        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(resetLayoutBtn);
        strip.Items.Add(new ToolStripSeparator());
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

        bool isFirstGallery = true;
        foreach (var panel in panels)
        {
            var item = CreateGalleryItem(
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName);

            if (!isFirstGallery) strip.Items.Add(new ToolStripSeparator());
            isFirstGallery = false;
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

        var sortAscBtn = CreateSmallNavButton(
            "Grid_SortAsc",
            "Sort Asc",
            () => SafeExecute((RibbonCommand)(() => form.SortActiveGridByFirstSortableColumn(false)), "SortAscending", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Sort Asc"),
            tooltip: "Sort grid ascending");

        var sortDescBtn = CreateSmallNavButton(
            "Grid_SortDesc",
            "Sort Desc",
            () => SafeExecute((RibbonCommand)(() => form.SortActiveGridByFirstSortableColumn(true)), "SortDescending", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Sort Desc"),
            tooltip: "Sort grid descending");

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

        var label = new ToolStripLabel("Global\r\nSearch:") { Name = "ActionGroup_SearchLabel", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
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
                    if (!IsUiTestEnvironment())
                    {
                        form.Refresh();
                    }
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

    // ─────────────────────────────────────────────────────────────────────────
    // Financials tab — group creators
    // ─────────────────────────────────────────────────────────────────────────

    private static ToolStripEx CreatePaymentsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Payments", "PaymentsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Payments", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    private static ToolStripEx CreateIntegrationGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Integration", "IntegrationGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Integration", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            var shortcutKeys = panel.DisplayName switch
            {
                "QuickBooks" => Keys.Control | Keys.Q,
                _ => Keys.None
            };

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel",
                shortcutKeys: shortcutKeys);
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics & Reports tab — group creators
    // ─────────────────────────────────────────────────────────────────────────

    private static ToolStripEx CreateAnalyticsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Analytics", "AnalyticsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Analytics", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    private static ToolStripEx CreateOperationsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Operations", "OperationsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Operations", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilities tab — group creator
    // ─────────────────────────────────────────────────────────────────────────

    private static ToolStripEx CreateUtilitiesGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Utilities", "UtilitiesGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Utilities", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Administration tab — group creators
    // ─────────────────────────────────────────────────────────────────────────

    private static ToolStripEx CreateAdministrationGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Administration", "AdministrationGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "Administration", StringComparison.OrdinalIgnoreCase)
                     && p.ShowInRibbonPanelsMenu)
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);

            var shortcutKeys = panel.DisplayName switch
            {
                "Settings" => Keys.Control | Keys.Alt | Keys.S,
                _ => Keys.None
            };

            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel",
                shortcutKeys: shortcutKeys);
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    private static ToolStripEx CreateAuditLogsGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Audit & Logs", "AuditLogsGroup", theme, logger);

        var panels = PanelRegistry.Panels
            .Where(p => string.Equals(p.DefaultGroup, "AuditLogs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.DisplayName)
            .ToList();

        bool isFirst = true;
        foreach (var panel in panels)
        {
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(panel.DisplayName, @"[^\w]", "");
            RibbonIconGlyphs.TryGetValue(panel.DisplayName, out var iconGlyph);
            var button = CreateLargeNavButton(
                $"Nav_{sanitizedName}",
                panel.DisplayName,
                () => SafeNavigate(form, panel.DisplayName, CreatePanelNavigationCommand(form, panel, logger), logger),
                logger,
                navigationTarget: panel.DisplayName,
                iconGlyph: iconGlyph,
                tooltip: $"Open {panel.DisplayName} panel");
            if (!isFirst) strip.Items.Add(new ToolStripSeparator());
            isFirst = false;
            strip.Items.Add(button);
        }

        return strip;
    }

    private static ToolStripEx CreateFileGroup(MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("File", "FileGroup", theme, logger);

#pragma warning disable CS0618
        var newBudgetBtn = CreateLargeNavButton(
            "File_NewBudget",
            "New\nBudget",
            () => SafeExecute((RibbonCommand)form.CreateNewBudget, "NewBudget", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("New"),
            tooltip: "Create new budget",
            shortcutKeys: Keys.Control | Keys.N);

        var openBudgetBtn = CreateLargeNavButton(
            "File_OpenBudget",
            "Open\nBudget",
            () => SafeExecute((RibbonCommand)form.OpenBudget, "OpenBudget", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Open"),
            tooltip: "Open existing budget",
            shortcutKeys: Keys.Control | Keys.O);

        var saveLayoutBtn = CreateLargeNavButton(
            "File_SaveLayout",
            "Save\nLayout",
            () => SafeExecute((RibbonCommand)form.SaveCurrentLayout, "SaveLayout", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Save Layout"),
            tooltip: "Save current layout",
            shortcutKeys: Keys.Control | Keys.Shift | Keys.S);

        var exportBtn = CreateLargeNavButton(
            "File_ExportData",
            "Export\nData",
            () => SafeExecute((RibbonCommand)form.ExportData, "ExportData", logger),
            logger,
            iconGlyph: RibbonIconGlyphs.GetValueOrDefault("Export"),
            tooltip: "Export data to file",
            shortcutKeys: Keys.Control | Keys.E);
#pragma warning restore CS0618

        strip.Items.Add(newBudgetBtn);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(openBudgetBtn);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(new ToolStripSeparator());
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

            // Avoid direct styling of RibbonPanel internals; RibbonControlAdv theme cascades safely.
            if (tab.Panel is Control panelControl && !IsRibbonPanelControl(panelControl) && !IsBackStageControl(panelControl))
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
        if (root == null || root.IsDisposed || root.Disposing || string.IsNullOrWhiteSpace(themeName))
        {
            return;
        }

        if (IsRibbonPanelControl(root) || IsBackStageControl(root))
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
            logger?.LogDebug("[SAFENAV] Skipping navigation - form is null or disposed");
            return;
        }

        void PerformNavigation()
        {
            try
            {
                if (form.IsDisposed || form.Disposing)
                {
                    return;
                }

                // Ensure form is visible and active
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

                // Ensure PanelNavigator is initialized (required for floating panel navigation)
                form.EnsurePanelNavigatorInitialized();

                if (form._panelNavigator == null)
                {
                    logger?.LogError("[SAFENAV] Navigation blocked - PanelNavigator is null for target '{Target}'", navigationTarget);
                    return;
                }

                logger?.LogDebug("[SAFENAV] Executing navigation action for '{Target}'", navigationTarget);

                // Execute the navigation command (opens floating panel via PanelNavigationService)
                navigateAction();

                logger?.LogInformation("[SAFENAV] Navigation completed for '{Target}'", navigationTarget);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[SAFENAV] Navigation failed for '{Target}'", navigationTarget);
            }
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
            logger?.LogError(ex, "[SAFENAV] Failed to dispatch navigation for '{Target}'", navigationTarget);
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

    private static void InitializeQuickAccessToolbar(RibbonControlAdv ribbon, ILogger? logger, bool isUiTestRuntime, params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null)
        {
            return;
        }

        try
        {
            if (isUiTestRuntime)
            {
                ribbon.QuickPanelVisible = false;
                ribbon.ShowQuickItemsDropDownButton = false;
                return;
            }

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

    /// <summary>
    /// Creates default Quick Access Toolbar buttons with icons and tooltips.
    /// QAT provides one-click access to frequently used commands.
    /// </summary>
    private static ToolStripButton[] CreateDefaultQuickAccessToolbarButtons(MainForm form, string theme, ILogger? logger)
    {
        // Save Layout button
        var saveButton = new ToolStripButton
        {
            Name = "QAT_Save",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE74E", 16, SystemColors.ControlText), // Save icon
            ToolTipText = "Save layout (Ctrl+Shift+S)",
            AutoSize = true,
            Enabled = true
        };
#pragma warning disable CS0618
        saveButton.Click += (_, _) => SafeExecute((RibbonCommand)form.SaveCurrentLayout, "QAT_SaveLayout", logger);
#pragma warning restore CS0618

        // Enterprise Vital Signs button (replaces deprecated Dashboard)
        var dashboardButton = new ToolStripButton
        {
            Name = "QAT_VitalSigns",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE80F", 16, SystemColors.ControlText), // Vital Signs icon
            ToolTipText = "Open Enterprise Vital Signs",
            AutoSize = true,
            Enabled = true
        };
        dashboardButton.Click += (_, _) =>
        {
            var vitalSignsEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Enterprise Vital Signs");
            if (vitalSignsEntry != null)
            {
                SafeNavigate(form, "Enterprise Vital Signs", CreatePanelNavigationCommand(form, vitalSignsEntry, logger), logger);
            }
        };

        // Settings button
        var settingsButton = new ToolStripButton
        {
            Name = "QAT_Settings",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE713", 16, SystemColors.ControlText), // Settings gear
            ToolTipText = "Settings (Ctrl+Alt+S)",
            AutoSize = true,
            Enabled = true
        };
        settingsButton.Click += (_, _) =>
        {
            var settingsEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Settings");
            if (settingsEntry != null)
            {
                SafeNavigate(form, "Settings", CreatePanelNavigationCommand(form, settingsEntry, logger), logger);
            }
        };

        // Budget button
        var budgetButton = new ToolStripButton
        {
            Name = "QAT_Budget",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE8F0", 16, SystemColors.ControlText), // Budget/Money icon
            ToolTipText = "Budget Management",
            AutoSize = true,
            Enabled = true
        };
        budgetButton.Click += (_, _) =>
        {
            var budgetEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Budget Management & Analysis");
            if (budgetEntry != null)
            {
                SafeNavigate(form, "Budget Management & Analysis", CreatePanelNavigationCommand(form, budgetEntry, logger), logger);
            }
        };

        return new[] { saveButton, dashboardButton, budgetButton, settingsButton };
    }

    private static void AttachRibbonLayoutHandlers(MainForm form, RibbonControlAdv ribbon, ToolStripTabItem homeTab, ToolStripTabItem? layoutContextTab, bool isUiTestRuntime, ILogger? logger)
    {
        if (form == null || ribbon == null || isUiTestRuntime)
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

    /// <summary>Helper: Check if control is a ribbon panel control</summary>
    private static bool IsRibbonPanelControl(Control control)
    {
        return control?.GetType().Name.Contains("RibbonPanel") ?? false;
    }

    /// <summary>Helper: Check if control is a backstage control</summary>
    private static bool IsBackStageControl(Control control)
    {
        return control?.GetType().Name.Contains("BackStage") ?? false;
    }

}
