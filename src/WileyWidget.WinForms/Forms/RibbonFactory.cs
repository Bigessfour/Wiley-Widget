using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring MainForm Ribbon as central navigation hub.
/// REFACTORED: Home tab is now the primary navigation interface with large 48px icon buttons,
/// organized in 4 logical groups (Dashboard, Financials, Reporting, Tools) plus Quick Access Toolbar.
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
    /// Creates and configures a production-ready RibbonControlAdv with Home tab as central navigation hub.
    /// </summary>
    /// <remarks>
    /// <para><strong>Navigation Hub:</strong> Home tab contains 4 main groups with large icons (48px) and text labels.</para>
    /// <para><strong>Primary 6 Panels (Home Tab Groups):</strong>
    /// <list type="bullet">
    /// <item><description>Dashboard Group: Dashboard (big blue button)</description></item>
    /// <item><description>Financials Group: Municipal Accounts, Budgets, Budget Analytics</description></item>
    /// <item><description>Reporting Group: Reports</description></item>
    /// <item><description>Tools Group: Settings, QuickBooks Sync</description></item>
    /// </list>
    /// </para>
    /// <para><strong>Secondary Panels (Ribbon menu/buttons):</strong> Charts, Customers, Analytics, Audit Log, Insights, War Room, JARVIS Chat, Utility Bills, CSV Import</para>
    /// <para><strong>Quick Access Toolbar:</strong> Dashboard, Accounts, Settings (pinned frequent actions above ribbon tabs)</para>
    /// <para><strong>Search:</strong> Global search box in Control group, Ctrl+F to focus</para>
    /// <para><strong>Theming:</strong> SfSkinManager theme cascade, Office2019 theme</para>
    /// <para><strong>Architecture:</strong> Uses ToolStripPanelItem for group organization, large icon buttons via CreateLargeNavButton helper</para>
    /// </remarks>
    /// <param name="form">MainForm instance for navigation and event handler wiring</param>
    /// <param name="logger">Optional logger for diagnostics and error tracking</param>
    /// <returns>Tuple containing fully configured RibbonControlAdv and Home tab reference</returns>
    /// <exception cref="ArgumentNullException">Thrown when form parameter is null</exception>
    public static (RibbonControlAdv Ribbon, ToolStripTabItem HomeTab) CreateRibbon(
        WileyWidget.WinForms.Forms.MainForm form,
        ILogger? logger)
    {
        if (form == null)
        {
            throw new ArgumentNullException(nameof(form));
        }

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            AccessibleName = "Main Navigation Ribbon",
            AccessibleDescription = "Primary navigation hub with Dashboard, Financials, Reporting, and Tools",
            Dock = (DockStyleEx)DockStyle.Top,
            MinimumSize = new System.Drawing.Size(800, 140),
            MenuButtonText = "File",
            MenuButtonWidth = 54,
            MenuButtonVisible = false,
            LauncherStyle = Syncfusion.Windows.Forms.Tools.LauncherStyle.Office2007
        };

        ribbon.SuspendLayout();

        var currentThemeString = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        var homeTab = new ToolStripTabItem { Text = "Home", AccessibleName = "Home Tab" };
        var homeToolStrip = new ToolStripEx { Name = "HomeToolStrip", AccessibleName = "Home ToolStrip" };

        // Create ribbon groups
        var (dashboardGroup, dashboardBtn) = CreateDashboardGroup(form, currentThemeString, logger);
        var (financialsGroup, accountsBtn, settingsBtn) = CreateFinancialsAndToolsGroups(form, currentThemeString, logger);
        var reportingGroup = CreateReportingGroup(form, currentThemeString, logger);
        var (toolsGroup, quickBooksBtn) = CreateToolsGroup(form, currentThemeString, logger);
        var layoutGroup = CreateLayoutGroup(form, currentThemeString, logger);
        var moreGroup = CreateMoreGroup(form, currentThemeString, logger);
        var searchThemeGroup = CreateSearchThemeGroup(form, currentThemeString, logger);
        var gridPanel = CreateGridToolsPanel(form, logger);

        // Create dropdown button for secondary panels
        var chartsBtn = CreateNavButton("Nav_Charts", "Charts", "charts", currentThemeString,
            () => form.ShowPanel<BudgetAnalyticsPanel>("Financial Charts", DockingStyle.Right, allowFloating: true),
            logger);

        var customersBtn = CreateNavButton("Nav_Customers", "Customers", "customers", currentThemeString,
            () => form.ShowPanel<CustomersPanel>("Customer Management", DockingStyle.Right, allowFloating: true),
            logger);

        var auditLogBtn = CreateNavButton("Nav_AuditLog", "Audit Log", "audit", currentThemeString,
            () => form.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true),
            logger);

        var insightsBtn = CreateNavButton("Nav_ProactiveInsights", "Insights", "insights", currentThemeString,
            () => form.ShowPanel<ProactiveInsightsPanel>("Proactive AI Insights", DockingStyle.Right, allowFloating: true),
            logger);

        var warRoomBtn = CreateNavButton("Nav_WarRoom", "War Room", "warroom", currentThemeString,
            () => form.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true),
            logger);

        var jarvischatBtn = new ToolStripButton
        {
            Name = "Nav_JARVISChat",
            AccessibleName = "JARVIS Chat",
            AccessibleDescription = "Open premium Grok-powered AI assistant",
            Text = "JARVIS Chat",
            AutoSize = true,
            ToolTipText = "Open JARVIS - Premium AI Assistant",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        jarvischatBtn.Click += (s, e) =>
        {
            try
            {
                // Switch to JARVIS Chat tab in right dock panel
                if (form is MainForm mainForm)
                {
                    logger?.LogInformation("[RIBBON_JARVIS] Switching to JARVIS Chat tab");
                    mainForm.SwitchRightPanel(RightDockPanelFactory.RightPanelMode.JarvisChat);
                    logger?.LogInformation("[RIBBON_JARVIS] JARVIS Chat tab activated");
                }
                else
                {
                    logger?.LogWarning("[RIBBON_JARVIS] Parent form is not MainForm - cannot switch to JARVIS Chat");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_JARVIS] Failed to switch to JARVIS Chat tab");
                MessageBox.Show($"Failed to open JARVIS Chat: {ex.Message}", "JARVIS Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var utilityBillsBtn = CreateNavButton("Nav_UtilityBills", "Utility Bills", "utilities", currentThemeString,
            () => form.ShowPanel<UtilityBillPanel>("Utility Bills", DockingStyle.Right, allowFloating: true),
            logger);

        var csvImportBtn = CreateNavButton("Nav_CsvImport", "CSV Import", "import", currentThemeString,
            () => form.ShowPanel<CsvMappingWizardPanel>("CSV Data Mapper", DockingStyle.Right, allowFloating: true),
            logger);

        moreGroup.Items.AddRange(new ToolStripItem[]
        {
            chartsBtn,
            customersBtn,
            new ToolStripSeparator(),
            auditLogBtn,
            insightsBtn,
            warRoomBtn,
            new ToolStripSeparator(),
            jarvischatBtn,
            new ToolStripSeparator(),
            utilityBillsBtn,
            csvImportBtn
        });

        // ===== ASSEMBLE HOME TAB =====
        homeToolStrip.Items.AddRange(new ToolStripItem[]
        {
            dashboardGroup,
            new ToolStripSeparator(),
            financialsGroup,
            new ToolStripSeparator(),
            reportingGroup,
            new ToolStripSeparator(),
            toolsGroup,
            new ToolStripSeparator(),
            layoutGroup,
            new ToolStripSeparator(),
            moreGroup,
            new ToolStripSeparator(),
            searchThemeGroup,
            new ToolStripSeparator(),
            gridPanel
        });

        homeTab.Panel.AddToolStrip(homeToolStrip);
        ribbon.Header.AddMainItem(homeTab);

        // ===== QUICK ACCESS TOOLBAR (QAT) =====
        // Pinned frequent actions above ribbon tabs
        try
        {
            InitializeQuickAccessToolbar(ribbon, logger, dashboardBtn, accountsBtn, settingsBtn);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] QAT initialization failed, continuing without QAT");
        }

        ribbon.ResumeLayout(performLayout: true);

        logger?.LogDebug("[RIBBON_FACTORY] Ribbon initialized as central navigation hub with Home tab (Dashboard, Financials, Reporting, Tools groups), QAT, and search box");

        return (ribbon, homeTab);
    }

    /// <summary>
    /// Creates a LARGE navigation button (48px+ icons) with text below for primary Home tab navigation.
    /// </summary>
    /// <remarks>
    /// <para>Large buttons provide high-visibility, touchscreen-friendly navigation for primary panels.</para>
    /// <para>Used for Dashboard, Accounts, Budgets, Analytics, Reports, Settings, QuickBooks.</para>
    /// </remarks>
    private static ToolStripButton CreateLargeNavButton(string name, string text, string? iconName, string theme, System.Action onClick, ILogger? logger)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = text.Replace("\n", " ", StringComparison.Ordinal),
            AccessibleDescription = $"Navigate to {text.Replace(Convert.ToChar(10), ' ')}",
            Enabled = true,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"Open {text.Replace(Convert.ToChar(10), ' ')}",
            ImageScaling = ToolStripItemImageScaling.None,
            Image = null
        };

        // Set large icon via DpiAwareImageService
        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var dpi = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services)
                    : null;
                // Request 48px icon for large buttons
                btn.Image = dpi?.GetImage(iconName);
                if (btn.Image != null)
                {
                    // Scale to 48x48 if needed
                    var scaledImage = new System.Drawing.Bitmap(btn.Image, new System.Drawing.Size(48, 48));
                    btn.Image = scaledImage;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to get large icon for '{IconName}'", iconName);
            }
        }

        // Wire click handler with logging and activity tracking
        btn.Click += (s, e) =>
        {
            try
            {
                Serilog.Log.Information("[RIBBON_NAV] Large button '{ButtonName}' clicked", name);
                logger?.LogInformation("[RIBBON_NAV] Navigating via {ButtonName}", name);

                // Log activity with access to form context through click handler closure
                var cleanText = text.Replace("\n", " ", StringComparison.Ordinal);
                LogNavigationActivity(form: null, actionName: cleanText, panelName: cleanText, logger);

                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_NAV] Button '{ButtonName}' click failed", name);
                MessageBox.Show($"Failed to navigate: {ex.Message}", "Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        return btn;
    }

    /// <summary>
    /// Creates a standard navigation button for secondary/advanced panels.
    /// </summary>
    /// <remarks>
    /// <para>Standard size buttons for secondary features (Charts, Customers, Audit, etc.).</para>
    /// </remarks>
    private static ToolStripButton CreateNavButton(string name, string text, string? iconName, string theme, System.Action onClick, ILogger? logger)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = name.Replace("Nav_", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal),
            AccessibleDescription = $"Navigate to {text} panel",
            Enabled = true,
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"Open {text}",
            ImageScaling = ToolStripItemImageScaling.None
        };

        // Set icon via DpiAwareImageService
        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var dpi = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services)
                    : null;
                btn.Image = dpi?.GetImage(iconName);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to get icon for '{IconName}'", iconName);
            }
        }

        // Wire click handler with logging and activity tracking
        btn.Click += (s, e) =>
        {
            try
            {
                Serilog.Log.Information("[RIBBON_NAV] Button '{ButtonName}' clicked", name);
                logger?.LogInformation("[RIBBON_NAV] Navigating via {ButtonName}", name);

                // Log activity
                LogNavigationActivity(form: null, actionName: text, panelName: text, logger);

                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_NAV] Button '{ButtonName}' click failed", name);
                MessageBox.Show($"Failed to navigate to {text}: {ex.Message}", "Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        return btn;
    }

    /// <summary>
    /// Creates a grid operation button for data manipulation.
    /// Supports both sync and async operations.
    /// </summary>
    private static ToolStripButton CreateGridButton(string name, string text, Func<Task> onClickAsync, ILogger? logger)
    {
        var cleanName = name.Replace("Grid_", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);
        var btn = new ToolStripButton
        {
            Name = name,
            Text = text,
            AccessibleName = cleanName,
            AccessibleDescription = $"Grid operation: {cleanName}",
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ToolTipText = $"{cleanName} active grid"
        };

        btn.Click += async (s, e) =>
        {
            try
            {
                await onClickAsync();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_GRID] Grid button '{ButtonName}' click failed", name);
                MessageBox.Show($"Grid operation '{cleanName}' failed: {ex.Message}", "Grid Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        return btn;
    }

    /// <summary>
    /// Logs navigation activities to the activity service for audit trail and activity dashboard.
    /// Integrates with IActivityLogService if available via DI.
    /// </summary>
    private static void LogNavigationActivity(WileyWidget.WinForms.Forms.MainForm? form, string actionName, string panelName, ILogger? logger)
    {
        // Fire-and-forget pattern: start async task without awaiting
        _ = LogNavigationActivityAsync(form, actionName, panelName, logger);
    }

    /// <summary>
    /// Internal async implementation for logging navigation activities.
    /// </summary>
    private static async Task LogNavigationActivityAsync(WileyWidget.WinForms.Forms.MainForm? form, string actionName, string panelName, ILogger? logger)
    {
        try
        {
            // Log via Serilog (always available)
            Serilog.Log.Information("[ACTIVITY_LOG] Navigation: {Action} -> {Panel}", actionName, panelName);
            logger?.LogInformation("[ACTIVITY_LOG] Navigation: {Action} -> {Panel}", actionName, panelName);

            // Try to log via activity service if available
            if (Program.Services != null)
            {
                try
                {
                    var activityService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<IActivityLogService>(Program.Services);

                    if (activityService != null)
                    {
                        await activityService.LogNavigationAsync(
                            actionName: $"Navigated to {actionName}",
                            details: $"Opened {panelName} panel",
                            status: "Success");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[ACTIVITY_LOG] Failed to log via IActivityLogService");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[ACTIVITY_LOG] Failed to log navigation activity");
        }
    }


    /// <summary>
    /// Initializes the Quick Access Toolbar (QAT) with frequently used buttons.
    /// </summary>
    /// <remarks>
    /// <para>QAT provides one-click access to key navigation buttons above ribbon tabs.</para>
    /// <para>Buttons are added as QuickButtonReflectable references, inheriting click handlers.</para>
    /// <para>Per Syncfusion API: QAT items inherit theme from ribbon automatically.</para>
    /// </remarks>
    private static void InitializeQuickAccessToolbar(
        RibbonControlAdv ribbon,
        ILogger? logger,
        params ToolStripButton[] buttons)
    {
        if (ribbon?.Header == null)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize QAT: Ribbon or Header is null");
            return;
        }

        if (buttons == null || buttons.Length == 0)
        {
            logger?.LogWarning("[RIBBON_FACTORY] Cannot initialize QAT: No buttons provided");
            return;
        }

        try
        {
            var addedCount = 0;
            foreach (var button in buttons.Where(b => b != null))
            {
                ribbon.Header.AddQuickItem(new QuickButtonReflectable(button));
                addedCount++;
            }
            logger?.LogDebug("[RIBBON_FACTORY] QAT initialized with {Count} pinned actions", addedCount);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to initialize QAT, continuing without QAT");
        }
    }

    /// <summary>
    /// Sets ThemeName property on target object (defensive theme application).
    /// </summary>
    private static void TrySetThemeName(object target, string themeName, ILogger? logger)
    {
        try
        {
            var prop = target.GetType().GetProperty("ThemeName", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.CanWrite != true) return;
            prop.SetValue(target, themeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set ThemeName on {Type}", target.GetType().FullName);
        }
    }

    /// <summary>
    /// Creates the Dashboard group with the main navigation button.
    /// </summary>
    private static (ToolStripPanelItem Group, ToolStripButton Button) CreateDashboardGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "DashboardGroup",
            Text = "Dashboard",
            AccessibleName = "Dashboard Navigation",
            AccessibleDescription = "Primary dashboard and overview",
            RowCount = 1
        };

        var dashboardBtn = CreateLargeNavButton(
            "Nav_Dashboard",
            "Dashboard",
            "dashboard",
            themeName,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Fill),
            logger);

        group.Items.Add(dashboardBtn);
        TrySetThemeName(group, themeName, logger);

        return (group, dashboardBtn);
    }

    /// <summary>
    /// Creates the Financials and Tools groups with account/budget/settings buttons.
    /// </summary>
    private static (ToolStripPanelItem FinancialsGroup, ToolStripButton AccountsButton, ToolStripButton SettingsButton) CreateFinancialsAndToolsGroups(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var financialsGroup = new ToolStripPanelItem
        {
            Name = "FinancialsGroup",
            Text = "Financials",
            AccessibleName = "Financial Data Management",
            AccessibleDescription = "Accounts, budgets, and financial analytics",
            RowCount = 2
        };

        var accountsBtn = CreateLargeNavButton(
            "Nav_Accounts",
            "Accounts",
            "accounts",
            themeName,
            () => form.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Right),
            logger);

        var budgetsBtn = CreateLargeNavButton(
            "Nav_Budgets",
            "Budgets",
            "budgets",
            themeName,
            () => form.ShowPanel<BudgetPanel>("Municipal Budgets", DockingStyle.Right),
            logger);

        var analyticsBtn = CreateLargeNavButton(
            "Nav_Analytics",
            "Analytics",
            "analytics",
            themeName,
            () => form.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right),
            logger);

        financialsGroup.Items.AddRange(new ToolStripItem[] { accountsBtn, budgetsBtn, analyticsBtn });
        TrySetThemeName(financialsGroup, themeName, logger);

        // Create a dummy settings button (real one created separately in Tools group)
        var settingsBtn = CreateLargeNavButton(
            "Nav_Settings",
            "Settings",
            "settings",
            themeName,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right),
            logger);

        return (financialsGroup, accountsBtn, settingsBtn);
    }

    /// <summary>
    /// Creates the Reporting group with Reports button.
    /// </summary>
    private static ToolStripPanelItem CreateReportingGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "ReportingGroup",
            Text = "Reporting",
            AccessibleName = "Reports and Analysis",
            AccessibleDescription = "Financial and operational reports",
            RowCount = 1
        };

        var reportsBtn = CreateLargeNavButton(
            "Nav_Reports",
            "Reports",
            "reports",
            themeName,
            () => form.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right),
            logger);

        group.Items.Add(reportsBtn);
        TrySetThemeName(group, themeName, logger);

        return group;
    }

    /// <summary>
    /// Creates the Tools group with Settings and QuickBooks Sync buttons.
    /// </summary>
    private static (ToolStripPanelItem Group, ToolStripButton QuickBooksButton) CreateToolsGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "ToolsGroup",
            Text = "Tools",
            AccessibleName = "System Tools",
            AccessibleDescription = "Settings, integrations, and utilities",
            RowCount = 1
        };

        var settingsBtn = CreateLargeNavButton(
            "Nav_Settings",
            "Settings",
            "settings",
            themeName,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right),
            logger);

        var quickBooksBtn = CreateLargeNavButton(
            "Nav_QuickBooks",
            "QB Sync",
            "quickbooks",
            themeName,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks Synchronization", DockingStyle.Right),
            logger);

        var jarvisBtn = CreateLargeNavButton(
            "Nav_JARVIS",
            "JARVIS AI",
            "jarvis",
            themeName,
            () =>
            {
                try
                {
                    // Switch to JARVIS Chat tab in right dock panel
                    if (form is MainForm mainForm)
                    {
                        logger?.LogInformation("[RIBBON_TOOLS] Switching to JARVIS Chat tab from Tools group");
                        mainForm.SwitchRightPanel(RightDockPanelFactory.RightPanelMode.JarvisChat);
                    }
                    else
                    {
                        logger?.LogWarning("[RIBBON_TOOLS] Parent form is not MainForm - cannot switch to JARVIS Chat");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[RIBBON_TOOLS] Failed to switch to JARVIS Chat tab");
                    MessageBox.Show($"Failed to open JARVIS: {ex.Message}", "JARVIS Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            },
            logger);

        group.Items.AddRange(new ToolStripItem[] { settingsBtn, quickBooksBtn, jarvisBtn });
        TrySetThemeName(group, themeName, logger);

        return (group, quickBooksBtn);
    }

    /// <summary>
    /// Creates the Layout group with Save and Reset buttons for docking layout management.
    /// </summary>
    private static ToolStripPanelItem CreateLayoutGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "LayoutGroup",
            Text = "Layout",
            AccessibleName = "Layout Management",
            AccessibleDescription = "Save and reset panel layouts",
            RowCount = 1
        };

        var saveLayoutBtn = new ToolStripButton
        {
            Name = "Nav_SaveLayout",
            Text = "Save Layout",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = true,
            ToolTipText = "Save current panel layout"
        };
        saveLayoutBtn.Click += (s, e) =>
        {
            try
            {
                // SaveLayout will be implemented in MainForm
                if (form is { } mainForm && mainForm.InvokeRequired == false)
                {
                    var method = form.GetType().GetMethod("SaveLayout", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(form, null);
                        MessageBox.Show("Layout saved successfully.", "Layout Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        logger?.LogInformation("[RIBBON_LAYOUT] Panel layout saved by user");
                    }
                    else
                    {
                        MessageBox.Show("Layout save functionality not available.", "Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_LAYOUT] Failed to save layout");
                MessageBox.Show($"Failed to save layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var resetLayoutBtn = new ToolStripButton
        {
            Name = "Nav_ResetLayout",
            Text = "Reset Layout",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = true,
            ToolTipText = "Reset panels to default layout"
        };
        resetLayoutBtn.Click += (s, e) =>
        {
            try
            {
                // Confirm with user before resetting
                var result = MessageBox.Show(
                    "Reset all panels to default layout? This cannot be undone.",
                    "Confirm Reset",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.OK)
                {
                    // ResetLayout will be implemented in MainForm
                    var method = form.GetType().GetMethod("ResetLayout", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(form, null);
                        MessageBox.Show("Layout reset to defaults.", "Layout Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        logger?.LogInformation("[RIBBON_LAYOUT] Panel layout reset by user");
                    }
                    else
                    {
                        MessageBox.Show("Layout reset functionality not available.", "Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_LAYOUT] Failed to reset layout");
                MessageBox.Show($"Failed to reset layout: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        group.Items.AddRange(new ToolStripItem[] { saveLayoutBtn, resetLayoutBtn });
        TrySetThemeName(group, themeName, logger);

        return group;
    }

    /// <summary>
    /// Creates the More group for secondary panels as a dropdown menu.
    /// </summary>
    private static ToolStripPanelItem CreateMoreGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "MorePanelsGroup",
            Text = "More",
            AccessibleName = "More Panels",
            AccessibleDescription = "Additional panels and advanced features",
            RowCount = 1
        };

        TrySetThemeName(group, themeName, logger);

        return group;
    }

    /// <summary>
    /// Creates the Search and Theme control group.
    /// </summary>
    private static ToolStripPanelItem CreateSearchThemeGroup(
        WileyWidget.WinForms.Forms.MainForm form,
        string themeName,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "SearchThemeGroup",
            Text = "Control",
            AccessibleName = "Search and Theme Control",
            AccessibleDescription = "Global search box and theme toggle",
            RowCount = 1
        };

        var searchLabel = new ToolStripLabel
        {
            Text = "Search:",
            Name = "GlobalSearch_Label",
            AccessibleName = "Search Label"
        };

        var searchBox = new ToolStripTextBox
        {
            Name = "GlobalSearch",
            AccessibleName = "Global Search",
            AccessibleDescription = "Search across all panels and data (press Enter to search)",
            AutoSize = false,
            Width = 220,
            ToolTipText = "Search panels and data (press Enter to execute, Ctrl+F to focus)"
        };
        searchBox.KeyDown += async (s, e) =>
        {
            if (s is not ToolStripTextBox box) return;
            try
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(box.Text))
                {
                    logger?.LogInformation("[RIBBON_SEARCH] Global search triggered: {SearchText}", box.Text);
                    form.GlobalIsBusy = true;
                    try
                    {
                        await form.PerformGlobalSearchAsync(box.Text);
                    }
                    finally
                    {
                        form.GlobalIsBusy = false;
                    }
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                form.GlobalIsBusy = false;
                logger?.LogError(ex, "[RIBBON_SEARCH] Search box KeyDown handler failed");
                MessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var themeToggleBtn = new ToolStripButton
        {
            Name = "ThemeToggle",
            AccessibleName = "Theme Toggle",
            AccessibleDescription = "Switch between light and dark themes",
            AutoSize = true,
            Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light" : "🌙 Dark",
            ToolTipText = "Toggle between light and dark themes (Ctrl+Shift+T)",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        themeToggleBtn.Click += (s, e) => form.ToggleTheme();

        group.Items.AddRange(new ToolStripItem[]
        {
            searchLabel,
            searchBox,
            new ToolStripSeparator(),
            themeToggleBtn
        });

        TrySetThemeName(group, themeName, logger);

        return group;
    }

    /// <summary>
    /// Creates the Grid Tools panel for data grid operations.
    /// </summary>
    private static ToolStripPanelItem CreateGridToolsPanel(
        WileyWidget.WinForms.Forms.MainForm form,
        ILogger? logger)
    {
        var group = new ToolStripPanelItem
        {
            Name = "GridToolsPanel",
            Text = "Grid",
            AccessibleName = "Grid Tools Panel",
            AccessibleDescription = "Data grid manipulation and export tools",
            RowCount = 1
        };

        var gridSortAscBtn = CreateGridButton("Grid_SortAsc", "Sort ▲",
            async () =>
            {
                try { form.GetType().GetMethod("SortActiveGridByFirstSortableColumn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(form, new object[] { false }); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid sort ascending not available"); }
                await Task.CompletedTask;
            },
            logger);

        var gridSortDescBtn = CreateGridButton("Grid_SortDesc", "Sort ▼",
            async () =>
            {
                try { form.GetType().GetMethod("SortActiveGridByFirstSortableColumn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(form, new object[] { true }); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid sort descending not available"); }
                await Task.CompletedTask;
            },
            logger);

        var gridClearBtn = CreateGridButton("Grid_ClearFilter", "Clear",
            async () =>
            {
                try { form.GetType().GetMethod("ClearActiveGridFilter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(form, null); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid clear filter not available"); }
                await Task.CompletedTask;
            },
            logger);

        var gridExportBtn = CreateGridButton("Grid_ExportExcel", "Export",
            async () =>
            {
                try { await (Task)(form.GetType().GetMethod("ExportActiveGridToExcel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(form, null) ?? Task.CompletedTask); }
                catch (Exception ex) { Serilog.Log.Debug(ex, "Grid export not available"); }
            },
            logger);

        group.Items.AddRange(new ToolStripItem[]
        {
            gridSortAscBtn,
            gridSortDescBtn,
            gridClearBtn,
            gridExportBtn
        });

        return group;
    }
}

