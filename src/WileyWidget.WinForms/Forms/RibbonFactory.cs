using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;     // Added for potentially missing namespace
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring MainForm Ribbon as central navigation hub.
/// REFACTORED: Polished per Syncfusion "Office 2019" demo standards.
/// - Uses distinct ToolStripEx for each group (proper separation).
/// - Uses Metro Launcher and Office2019-compatible styling for modern look.
/// - Standardizes on 32px icons for large buttons using ImageAboveText relation.
/// </summary>
public static class RibbonFactory
{
    private static DpiAwareImageService? GetDpiService() =>
        Program.Services != null
            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services)
            : null;

    /// <summary>
    /// Safely invokes a method on the form via reflection.
    /// </summary>
    private static bool TryInvokeFormMethod(System.Windows.Forms.Form form, string methodName, object?[]? parameters, out object? result)
    {
        result = null;
        try
        {
            var method = form.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null)
                return false;

            result = method.Invoke(form, parameters);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[RIBBON_FACTORY] Form method '{MethodName}' invocation failed", methodName);
            return false;
        }
    }

    /// <summary>
    /// Creates and configures a production-ready RibbonControlAdv matching Syncfusion Demo standards.
    /// </summary>
    public static (RibbonControlAdv Ribbon, ToolStripTabItem HomeTab) CreateRibbon(
        WileyWidget.WinForms.Forms.MainForm form,
        ILogger? logger)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));

        // Ensure theme assembly is loaded BEFORE initialization to prevent Enum.Parse crashes
        try
        {
            // Best practice: skin manager loads theme assembly
            SfSkinManager.LoadAssembly(typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly);
        }
        catch { /* Ignore if already loaded or not available */ }

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            Dock = (DockStyleEx)DockStyle.Top,
            Location = new Point(0, 0),
            // Let Syncfusion theme control the visual style for Office2019 compatibility
            LauncherStyle = LauncherStyle.Metro,
            MenuButtonText = "File",
            MenuButtonWidth = 54,
            MenuButtonVisible = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TitleColor = Color.Black
        };

        // Force height to prevent auto-size collapse in some legacy styles
        ribbon.Height = 150;

        // Apply current application theme to the ribbon (prefer Office2019)
        try
        {
            var appTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            if (appTheme.StartsWith("Office2019", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure Office2019 theme assembly is available
                SfSkinManager.LoadAssembly(typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly);
            }
            ribbon.ThemeName = appTheme;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] Theme load failed. Falling back to default theme.");
            ribbon.ThemeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        }

        // === BACKSTAGE VIEW (The "File" Menu) ===
        // Replaces standard MenuButtonText
        ribbon.BackStageView = CreateBackStage(form, logger);
        ribbon.MenuButtonEnabled = true;



        var currentThemeString = ribbon.ThemeName;
        var homeTab = new ToolStripTabItem { Text = "Home", Name = "HomeTab" };

        // === CREATE GROUPS (ToolStripEx) ===
        // Per Demo: Each group is a separate ToolStripEx added to the Tab Panel.

        // 1. Dashboard Group
        var (dashboardStrip, dashboardBtn) = CreateDashboardGroup(form, currentThemeString, logger);

        // 2. Financials Group
        var (financialsStrip, accountsBtn, budgetsBtn) = CreateFinancialsGroup(form, currentThemeString, logger);

        // 3. Reporting Group
        var reportingStrip = CreateReportingGroup(form, currentThemeString, logger);

        // 4. Tools Group
        var (toolsStrip, quickBooksBtn, settingsBtn) = CreateToolsGroup(form, currentThemeString, logger);

        // 5. Layout Group
        var layoutStrip = CreateLayoutGroup(form, currentThemeString, logger);

        // 6. Secondary/More Group
        var moreStrip = CreateMoreGroup(form, currentThemeString, logger);

        // 7. Search & Grid Controls (Combined or separate based on space)
        var searchAndGridStrip = CreateSearchAndGridGroup(form, currentThemeString, logger);

        // === ASSEMBLE RIBBON ===
        // Add distinct strips to the Home Tab Panel using AddToolStrip (CRITICAL for layout sizing)
        homeTab.Panel.AddToolStrip(dashboardStrip);
        homeTab.Panel.AddToolStrip(financialsStrip);
        homeTab.Panel.AddToolStrip(reportingStrip);
        homeTab.Panel.AddToolStrip(toolsStrip);
        homeTab.Panel.AddToolStrip(layoutStrip);
        homeTab.Panel.AddToolStrip(moreStrip);
        homeTab.Panel.AddToolStrip(searchAndGridStrip);

        ribbon.Header.AddMainItem(homeTab);

        // === RIBBON HEADER VALIDATION (Prevent Paint Crashes) ===
        // CRITICAL: Syncfusion DockHost.GetPaintInfo() crashes with ArgumentOutOfRangeException
        // if ribbon header has zero items when paint fires. Guard by ensuring at least one item.
        if (ribbon.Header.MainItems.Count == 0)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Ribbon header had 0 items - adding fallback Home tab to prevent paint crash");
            var fallbackTab = new ToolStripTabItem { Text = "Home", Name = "FallbackHomeTab" };
            ribbon.Header.AddMainItem(fallbackTab);
        }

        // ===== QUICK ACCESS TOOLBAR (QAT) =====
        try
        {
            InitializeQuickAccessToolbar(ribbon, logger, dashboardBtn, accountsBtn, settingsBtn);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] QAT initialization failed");
        }

        logger?.LogDebug("[RIBBON_FACTORY] Ribbon initialized with Office 2019 Demo styling (header items={ItemCount})", ribbon.Header.MainItems.Count);

        return (ribbon, homeTab);
    }

    /// <summary>
    /// Creates the BackStage view (File Menu) with standard application commands.
    /// Defensive: some Syncfusion versions initialize BackStage lazily. This method avoids throwing if BackStage or its child collections are null.
    /// </summary>
    private static Syncfusion.Windows.Forms.BackStageView CreateBackStage(WileyWidget.WinForms.Forms.MainForm form, ILogger? logger)
    {
        // 1. Create the control (Holds the UI)
        var backstage = new Syncfusion.Windows.Forms.BackStageView(new System.ComponentModel.Container());

        try
        {
            // --- TAB: INFO ---
            var infoTab = new BackStageTab { Text = "Info", Name = "BackStage_Info" };

            // Add info content container
            var infoContainer = new Panel();
            infoContainer.Controls.Add(new Label
            {
                Text = "Wiley Widget System Info",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            });
            infoContainer.Controls.Add(new Label
            {
                Text = $"Version: {Assembly.GetExecutingAssembly().GetName().Version}\nEnvironment: Production\nLicense: Valid",
                Font = new Font("Segoe UI", 10F),
                Location = new Point(25, 60),
                AutoSize = true
            });

            // Prefer adding to tab if possible, otherwise add to BackStage directly
            try
            {
                infoTab.Controls.Add(infoContainer);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "[RIBBON_FACTORY] Could not add content to BackStageTab.Controls - falling back to BackStage.Controls");
            }

            // --- BUTTON: EXIT ---
            var exitBtn = new BackStageButton { Text = "Exit", Name = "BackStage_Exit" };
            exitBtn.Click += (s, e) => Application.Exit();

            // 3. Add items to the BackStage control safely
            if (backstage.BackStage != null)
            {
                try
                {
                    backstage.BackStage.Controls.Add(infoTab);
                    backstage.BackStage.Controls.Add(exitBtn);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to add items to BackStage.BackStage.Controls - skipping BackStage content");
                    return backstage;
                }
            }
            else
            {
                // BackStage property not initialized for this Syncfusion version - skip adding tabs safely.
                logger?.LogDebug("[RIBBON_FACTORY] BackStageView.BackStage is null - skipping tab/button addition (BackStage will be empty)");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] CreateBackStage encountered an error - returning minimal BackStageView");
            return new Syncfusion.Windows.Forms.BackStageView(new System.ComponentModel.Container());
        }

        return backstage;
    }

    /// <summary>
    private static ToolStripEx CreateRibbonGroup(string title, string name)
    {
        var strip = new ToolStripEx
        {
            Name = name,
            Text = title,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = true,
            LauncherStyle = LauncherStyle.Metro,
            ShowLauncher = false, // Set true if you have a dialog launcher event
            ImageScalingSize = new Size(32, 32) // Standardize on 32px for Large buttons
        };
        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton DashboardBtn) CreateDashboardGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Dashboard", "DashboardGroup");

        var dashboardBtn = CreateLargeNavButton(
            "Nav_Dashboard", "Dashboard", "dashboard", theme,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Fill), logger);

        strip.Items.Add(dashboardBtn);
        return (strip, dashboardBtn);
    }

    private static (ToolStripEx Strip, ToolStripButton AccountsBtn, ToolStripButton BudgetsBtn) CreateFinancialsGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Financials", "FinancialsGroup");

        var accountsBtn = CreateLargeNavButton(
            "Nav_Accounts", "Accounts", "accounts", theme,
            () => form.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Right), logger);

        var budgetsBtn = CreateLargeNavButton(
            "Nav_Budgets", "Budgets", "budgets", theme,
            () => form.ShowPanel<BudgetPanel>("Municipal Budgets", DockingStyle.Right), logger);

        var analyticsBtn = CreateLargeNavButton(
            "Nav_Analytics", "Analytics", "analytics", theme,
            () => form.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right), logger);

        strip.Items.Add(accountsBtn);
        strip.Items.Add(budgetsBtn);
        strip.Items.Add(analyticsBtn);

        return (strip, accountsBtn, budgetsBtn);
    }

    private static ToolStripEx CreateReportingGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Reporting", "ReportingGroup");

        var reportsBtn = CreateLargeNavButton(
            "Nav_Reports", "Reports", "reports", theme,
            () => form.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right), logger);

        strip.Items.Add(reportsBtn);
        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton QuickBooksBtn, ToolStripButton SettingsBtn) CreateToolsGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Tools", "ToolsGroup");

        var settingsBtn = CreateLargeNavButton(
            "Nav_Settings", "Settings", "settings", theme,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right), logger);

        var quickBooksBtn = CreateLargeNavButton(
            "Nav_QuickBooks", "QB Sync", "quickbooks", theme,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks Synchronization", DockingStyle.Right), logger);

        var jarvisBtn = CreateLargeNavButton(
            "Nav_JARVIS", "JARVIS AI", "jarvis", theme,
            () => {
                if (form is MainForm mainForm)
                {
                    // Defer slightly to allow ribbon paint to complete before docking layout changes
                    form.BeginInvoke(new System.Action(() =>
                    {
                        try
                        {
                            mainForm.SwitchRightPanel(RightDockPanelFactory.RightPanelMode.JarvisChat);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Failed to switch to JARVIS Chat panel");
                            System.Windows.Forms.MessageBox.Show(
                                $"Failed to open JARVIS Chat: {ex.Message}",
                                "Panel Error",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Warning);
                        }
                    }));
                }
            }, logger);

        strip.Items.Add(settingsBtn);
        strip.Items.Add(quickBooksBtn);
        strip.Items.Add(jarvisBtn);

        return (strip, quickBooksBtn, settingsBtn);
    }

    private static ToolStripEx CreateLayoutGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Layout", "LayoutGroup");

        // Stack these small buttons vertically
        var panelItem = new ToolStripPanelItem { RowCount = 2, AutoSize = true, Transparent = true };

        var saveLayoutBtn = CreateSmallNavButton("Nav_SaveLayout", "Save Layout", null,
            () => TryInvokeFormMethod(form, "SaveLayout", null, out _));

        var resetLayoutBtn = CreateSmallNavButton("Nav_ResetLayout", "Reset Layout", null,
            () => TryInvokeFormMethod(form, "ResetLayout", null, out _));

        panelItem.Items.Add(saveLayoutBtn);
        panelItem.Items.Add(resetLayoutBtn);

        strip.Items.Add(panelItem);
        return strip;
    }

    private static ToolStripEx CreateMoreGroup(
         WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Views", "MorePanelsGroup");

        // Featured items (Large)
        var warRoomBtn = CreateLargeNavButton("Nav_WarRoom", "War Room", "warroom", theme,
             () => form.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true), logger);

        // Secondary items (Vertical Stack)
        var stack = new ToolStripPanelItem { RowCount = 3, AutoSize = true, Transparent = true };

        stack.Items.Add(CreateSmallNavButton("Nav_Charts", "Charts", "charts",
            () => form.ShowPanel<BudgetAnalyticsPanel>("Financial Charts", DockingStyle.Right, allowFloating: true)));

        stack.Items.Add(CreateSmallNavButton("Nav_Customers", "Customers", "customers",
            () => form.ShowPanel<CustomersPanel>("Customer Management", DockingStyle.Right, allowFloating: true)));

        stack.Items.Add(CreateSmallNavButton("Nav_AuditLog", "Audit Log", "audit",
            () => form.ShowPanel<AuditLogPanel>("Audit Log", DockingStyle.Bottom, allowFloating: true)));

        stack.Items.Add(CreateSmallNavButton("Nav_Utilites", "Utility Bills", "utilities",
            () => form.ShowPanel<UtilityBillPanel>("Utility Bills", DockingStyle.Right, allowFloating: true)));

        stack.Items.Add(CreateSmallNavButton("Nav_Import", "CSV Import", "import",
            () => form.ShowPanel<CsvMappingWizardPanel>("Data Mapper", DockingStyle.Right, allowFloating: true)));

        strip.Items.Add(warRoomBtn);
        strip.Items.Add(stack);
        return strip;
    }

    private static ToolStripEx CreateSearchAndGridGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Actions", "ActionGroup");

        // 1. Grid Tools Stack
        var gridStack = new ToolStripPanelItem { RowCount = 2, AutoSize = true, Transparent = true };

        var sortAscBtn = CreateSmallNavButton("Grid_SortAsc", "Sort Asc", null,
             () => TryInvokeFormMethod(form, "SortActiveGridByFirstSortableColumn", new object[] { false }, out _));

        var sortDescBtn = CreateSmallNavButton("Grid_SortDesc", "Sort Desc", null,
             () => TryInvokeFormMethod(form, "SortActiveGridByFirstSortableColumn", new object[] { true }, out _));

        gridStack.Items.Add(sortAscBtn);
        gridStack.Items.Add(sortDescBtn);

        // 2. Search Box (Large control)
        var searchBox = new ToolStripTextBox
        {
            Name = "GlobalSearch",
            AccessibleName = "Global Search Box",
            AccessibleRole = AccessibleRole.Text,  // Text input role
            AccessibleDescription = "Enter search query to find panels and content. Press Enter to search across all modules.",
            AutoSize = false,
            Width = 180,
            BorderStyle = BorderStyle.FixedSingle,
            ToolTipText = "Search panels (Enter to search)"
        };
        searchBox.KeyDown += async (s, e) => {
             if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(searchBox.Text)) {
                 form.GlobalIsBusy = true;
                 try { await form.PerformGlobalSearchAsync(searchBox.Text); }
                 finally { form.GlobalIsBusy = false; }
                 e.Handled = true;
             }
        };

        var searchStack = new ToolStripPanelItem { RowCount = 2, AutoSize = true, Transparent = true };
        var lbl = new ToolStripLabel("Global Search:");
        searchStack.Items.Add(lbl);
        searchStack.Items.Add(searchBox);

        // 3. Theme Toggle (Large)
        var themeBtn = new ToolStripButton
        {
            Text = "Toggle Theme",
            TextImageRelation = TextImageRelation.ImageAboveText,
            ImageScaling = ToolStripItemImageScaling.None
        };
        themeBtn.Name = "ThemeToggle";
        // Reuse an icon if available, or just text
        themeBtn.Click += (s, e) => form.ToggleTheme();

        strip.Items.Add(searchStack);
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(gridStack);
        strip.Items.Add(themeBtn);

        return strip;
    }

    // === BUTTON FACTORIES ===

    /// <summary>
    /// Creates a LARGE navigation button per Syncfusion Demo guidelines.
    /// uses TextImageRelation.ImageAboveText to force the large layout.
    /// Includes full accessibility support (AccessibleName, AccessibleRole, AccessibleDescription).
    /// DPI-Aware: Uses GetScaledImage() to select optimal pre-scaled icon variant for 125%/150%/200% displays,
    /// preventing blurring of 32px icons that would occur from upscaling 16px sources.
    /// </summary>
    private static ToolStripButton CreateLargeNavButton(string name, string text, string? iconName, string theme, System.Action onClick, ILogger? logger)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = $"Open {text}",  // Descriptive action
            AccessibleRole = AccessibleRole.PushButton,  // Role for screen readers
            AccessibleDescription = $"Navigate to the {text.Replace("\n", " ", StringComparison.Ordinal)} panel",  // Detailed description
            Enabled = true,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText, // CRITICAL for Large Layout
            ImageScaling = ToolStripItemImageScaling.None, // Prevents downscaling 32px icons
            ToolTipText = text
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var dpi = GetDpiService();
                var img = dpi?.GetScaledImage(iconName, new Size(32, 32));
                if (img != null)
                {
                    btn.Image = img;
                }
            }
            catch (Exception ex) { logger?.LogWarning(ex, "Failed to load DPI-scaled icon {Icon}", iconName); }
        }

        // Fix: Ensure Image is not null to prevent layout collapse
        if (btn.Image == null)
        {
            btn.Image = new Bitmap(32, 32);
            // Optional: Draw a placeholder box if desired, but transparent 32x32 ensures correct height
        }

        btn.Click += (s, e) =>
        {
            LogNavigationActivity(null, text, text, logger);
            onClick();
        };

        return btn;
    }

    /// <summary>
    /// Creates a SMALL navigation button (Side-by-side or stacked).
    /// </summary>
    private static ToolStripButton CreateSmallNavButton(string name, string text, string? iconName, System.Action onClick)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = text,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageScaling = ToolStripItemImageScaling.None // 16x16 standard
        };

        if (!string.IsNullOrEmpty(iconName))
        {
             var dpi = GetDpiService();
             var img = dpi?.GetImage(iconName);
             if (img != null) btn.Image = new Bitmap(img, new Size(16, 16));
        }

        btn.Click += (s, e) => onClick();
        return btn;
    }

    private static void LogNavigationActivity(WileyWidget.WinForms.Forms.MainForm? form, string actionName, string panelName, ILogger? logger)
    {
        _ = LogNavigationActivityAsync(form, actionName, panelName, logger);
    }

    private static async Task LogNavigationActivityAsync(WileyWidget.WinForms.Forms.MainForm? form, string actionName, string panelName, ILogger? logger)
    {
        try
        {
            Serilog.Log.Information("[NAV] {Action} -> {Panel}", actionName, panelName);
            if (Program.Services != null)
            {
                var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<IActivityLogService>(Program.Services);
                if (service != null)
                {
                    await service.LogNavigationAsync(
                        actionName: $"Navigated to {actionName}",
                        details: $"Opened {panelName} panel",
                        status: "Success");
                }
            }
        }
        catch { /* Fire and forget */ }
    }

    private static void InitializeQuickAccessToolbar(
        RibbonControlAdv ribbon,
        ILogger? logger,
        params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null) return;
        foreach (var button in buttons.Where(b => b != null))
        {
            ribbon.Header.AddQuickItem(new QuickButtonReflectable(button));
        }
    }
}
