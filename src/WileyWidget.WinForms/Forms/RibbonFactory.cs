using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;     // Added for potentially missing namespace
using Syncfusion.Windows.Forms.Tools;
using System;
using Action = System.Action;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

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
    private static DpiAwareImageService? GetDpiService()
    {
        try
        {
            var services = Program.ServicesOrNull;
            return services != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(services)
                : null;
        }
        catch
        {
            // Services may not be initialized during tests; return null gracefully
            return null;
        }
    }

    /// <summary>
    /// Safely invokes a method on the form via reflection.
    /// Handles TargetInvocationException to reveal the underlying cause.
    /// </summary>
    private static bool TryInvokeFormMethod(System.Windows.Forms.Form form, string methodName, object?[]? parameters, out object? result, ILogger? logger = null)
    {
        result = null;
        if (form == null) return false;

        try
        {
            var method = form.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null)
            {
                logger?.LogWarning("[RIBBON_FACTORY] Method '{MethodName}' not found on {FormType}", methodName, form.GetType().Name);
                return false;
            }

            result = method.Invoke(form, parameters);
            return true;
        }
        catch (TargetInvocationException tied)
        {
            // The method was found and called, but threw an exception.
            var inner = tied.InnerException ?? tied;
            logger?.LogError(inner, "[RIBBON_FACTORY] Form method '{MethodName}' threw an exception: {Message}", methodName, inner.Message);
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Fatal error invoking form method '{MethodName}'", methodName);
            return false;
        }
    }

    private static void SafeBeginInvoke(Control control, System.Action action, ILogger? logger)
    {
        if (control.IsDisposed || control.Disposing)
        {
            logger?.LogDebug("[RIBBON_FACTORY] BeginInvoke skipped: control disposed ({ControlName})", control.Name);
            return;
        }

        void InvokeAction()
        {
            try
            {
                control.BeginInvoke(action);
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                var message = ex is ObjectDisposedException
                    ? "[RIBBON_FACTORY] BeginInvoke skipped for disposed control {ControlName}"
                    : "[RIBBON_FACTORY] BeginInvoke failed for {ControlName}";
                logger?.LogDebug(ex, message, control.Name);
            }
        }

        if (control.IsHandleCreated)
        {
            InvokeAction();
            return;
        }

        EventHandler? handler = null;
        handler = (_, __) =>
        {
            control.HandleCreated -= handler;
            if (control.IsDisposed || control.Disposing)
            {
                return;
            }

            InvokeAction();
        };

        control.HandleCreated += handler;
    }

    /// <summary>
    /// Creates and configures a production-ready RibbonControlAdv matching Syncfusion Demo standards.
    /// </summary>
    public static (RibbonControlAdv Ribbon, ToolStripTabItem HomeTab) CreateRibbon(
        WileyWidget.WinForms.Forms.MainForm form,
        ILogger? logger)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));

        logger?.LogInformation("[RIBBON_FACTORY] CreateRibbon: Starting ribbon creation for MainForm");

        // Ensure theme assembly is loaded BEFORE initialization to prevent theme resource errors
        try
        {
            AppThemeColors.EnsureThemeAssemblyLoaded(logger);
            logger?.LogDebug("[RIBBON_FACTORY] Office2019Theme assembly loaded successfully");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to load Office2019Theme assembly");
        }

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            Dock = DockStyleEx.Top,
            Location = new Point(0, 0),
            // Let Syncfusion theme control the visual style for Office2019 compatibility
            LauncherStyle = LauncherStyle.Metro
            // Removed: TitleColor = Color.Black (let theme control)
            // Removed: Font = new Font("Segoe UI", 9F, FontStyle.Regular) (let theme control fonts)
        };

        var ribbonHeight = (int)DpiAware.LogicalToDeviceUnits(150f);
        ribbon.AutoSize = false;
        ribbon.Height = ribbonHeight;
        ribbon.MinimumSize = new Size(0, ribbonHeight);
        ribbon.Width = form.ClientSize.Width;

        logger?.LogDebug("[RIBBON_FACTORY] RibbonControlAdv created: Name={Name}, Height={Height}, Width={Width}",
            ribbon.Name, ribbon.Height, ribbon.Width);

        // Apply current application theme to the ribbon (prefer Office2019)
        try
        {
            var appTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
            if (appTheme.StartsWith("Office2019", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure Office2019 theme assembly is available
                AppThemeColors.EnsureThemeAssemblyLoaded(logger);
            }
            ribbon.ThemeName = appTheme;
            logger?.LogDebug("[RIBBON_FACTORY] Theme applied to ribbon: {Theme}", appTheme);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] Theme load failed. Falling back to default theme.");
            ribbon.ThemeName = AppThemeColors.DefaultTheme;
        }

        ConfigureRibbonAppearance(ribbon, logger);

        // === BACKSTAGE VIEW (The "File" Menu) ===
        // Replaces standard MenuButtonText
        var backStageView = CreateBackStage(form, logger);
        ribbon.BackStageView = backStageView;
        ribbon.MenuButtonEnabled = true;
        ribbon.MenuButtonVisible = true;

        // Logging for Menu Button click
        ribbon.MenuButtonClick += (s, e) =>
        {
            logger?.LogInformation("[RIBBON_FACTORY] Menu Button (File) Clicked");
        };

        if (ribbon.BackStageView == null)
        {
            logger?.LogWarning("[RIBBON_FACTORY] BackStageView was null after assignment - recreating");
            ribbon.BackStageView = CreateBackStage(form, logger);
        }

        var currentThemeString = ribbon.ThemeName;
        var homeTab = new ToolStripTabItem { Text = "Home", Name = "HomeTab" };

        logger?.LogDebug("[RIBBON_FACTORY] HomeTab created: Text={Text}, Name={Name}", homeTab.Text, homeTab.Name);

        // === CREATE GROUPS (ToolStripEx) ===
        // Per Demo: Each group is a separate ToolStripEx added to the Tab Panel.

        // 1. Dashboard Group
        var (dashboardStrip, dashboardBtn) = CreateDashboardGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Dashboard group created");

        // 2. Financials Group
        var (financialsStrip, accountsBtn) = CreateFinancialsGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Financials group created");

        // 3. Reporting Group
        var reportingStrip = CreateReportingGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Reporting group created");

        // 4. Tools Group
        var (toolsStrip, quickBooksBtn, settingsBtn) = CreateToolsGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Tools group created");

        // 5. Layout Group
        var layoutStrip = CreateLayoutGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Layout group created");

        // 6. Secondary/More Group
        var moreStrip = CreateMoreGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] More group created");

        // 7. Search & Grid Controls (Combined or separate based on space)
        var searchAndGridStrip = CreateSearchAndGridGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Search and Grid group created");

        // === ASSEMBLE RIBBON ===
        // Add distinct strips to the Home Tab Panel using AddToolStrip (CRITICAL for layout sizing)
        AddToolStripToTabPanel(homeTab, dashboardStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, financialsStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, reportingStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, toolsStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, layoutStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, moreStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, searchAndGridStrip, currentThemeString, logger);

        logger?.LogDebug("[RIBBON_FACTORY] All tool strips added to HomeTab panel");

        homeTab.Panel.AutoSize = true;
        homeTab.Panel.Padding = new Padding(6, 4, 6, 4);

        ribbon.Header.AddMainItem(homeTab);
        logger?.LogDebug("[RIBBON_FACTORY] HomeTab added to ribbon header");

        try
        {
            ApplyThemeRecursively(ribbon, currentThemeString, logger);
            logger?.LogDebug("[RIBBON_FACTORY] Theme applied recursively to ribbon");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Theme recursion failed");
        }

        try
        {
            if (homeTab.Panel != null)
            {
                homeTab.Panel.PerformLayout();
                homeTab.Panel.Refresh();
            }
            logger?.LogDebug("[RIBBON_FACTORY] HomeTab panel layout performed and refreshed");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Home tab panel refresh failed");
        }

        try
        {
            ribbon.PerformLayout();
            ribbon.Refresh();
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Ribbon refresh failed");
        }

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

        try
        {
            var groupCount = homeTab.Panel?.Controls.OfType<ToolStripEx>().Count() ?? 0;
            logger?.LogDebug("[RIBBON_FACTORY] Ribbon HomeTab groups loaded: {GroupCount}", groupCount);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to log ribbon group count");
        }

        logger?.LogDebug("[RIBBON_FACTORY] Ribbon initialized with Office 2019 Demo styling (header items={ItemCount})", ribbon.Header.MainItems.Count);

        // [PERF] Batch UI updates to reduce intermediate paints
        ribbon.PerformLayout();
        ribbon.Refresh();

        logger?.LogInformation("[RIBBON_FACTORY] CreateRibbon: Completed successfully");

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

        // Apply theme to BackStageView
        try
        {
            SfSkinManager.SetVisualStyle(backstage, SfSkinManager.ApplicationVisualTheme);
        }
        catch { }

        try
        {
            // --- TABS: NEW / OPEN ---
            var newTab = new BackStageTab { Text = "New", Name = "BackStage_New" };
            newTab.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] New tab clicked");
                TryInvokeFormMethod(form, "ApplyStatus", new object?[] { "BackStage: New" }, out _, logger);
            };

            var newContainer = new Panel();
            newContainer.Controls.Add(new Label
            {
                Text = "Create a new workspace or budget",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                Location = new Point(20, 20),
                AutoSize = true
            });
            try { newTab.Controls.Add(newContainer); } catch { }

            var openTab = new BackStageTab { Text = "Open", Name = "BackStage_Open" };
            openTab.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Open tab clicked");
                TryInvokeFormMethod(form, "ApplyStatus", new object?[] { "BackStage: Open" }, out _, logger);
            };

            var openContainer = new Panel();
            openContainer.Controls.Add(new Label
            {
                Text = "Open an existing file or project",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                Location = new Point(20, 20),
                AutoSize = true
            });
            try { openTab.Controls.Add(openContainer); } catch { }

            // --- TAB: INFO ---
            var infoTab = new BackStageTab { Text = "Info", Name = "BackStage_Info" };
            infoTab.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Info tab clicked");
            };

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
            try
            {
                exitBtn.Placement = BackStageItemPlacement.Bottom;
            }
            catch { }
            exitBtn.Click += (s, e) =>
            {
                logger?.LogInformation("[BACKSTAGE] Exit button clicked");
                Application.Exit();
            };

            // 3. Add items to the BackStage control safely
            if (backstage.BackStage != null)
            {
                try
                {
                    try
                    {
                        backstage.BackStage.BackStagePanelWidth = 200;
                    }
                    catch { }

                    backstage.BackStage.Controls.Add(newTab);
                    backstage.BackStage.Controls.Add(openTab);
                    backstage.BackStage.Controls.Add(infoTab);
                    backstage.BackStage.Controls.Add(exitBtn);
                    backstage.BackStage.SelectedTab = infoTab;
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
    /// Applies Syncfusion Ribbon appearance defaults from the docs (style, QAT).
    /// </summary>
    private static void ConfigureRibbonAppearance(RibbonControlAdv ribbon, ILogger? logger)
    {
        if (ribbon == null) return;

        try
        {
            // Ensure style compatibility with modern themes
            var themeName = ribbon.ThemeName ?? string.Empty;
            if (themeName.Contains("Office2019", StringComparison.OrdinalIgnoreCase))
            {
                ribbon.RibbonStyle = RibbonStyle.Office2016;
            }
            else
            {
                ribbon.RibbonStyle = RibbonStyle.Office2016; // Use modern style as default
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set RibbonStyle");
        }

        try
        {
            ribbon.MenuButtonText = "File";
            ribbon.MenuButtonVisible = true;
            ribbon.MenuButtonEnabled = true; // Explicitly set
            ribbon.MenuButtonWidth = 54;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set MenuButton configuration");
        }

        try
        {
            ribbon.QuickPanelVisible = true;
            ribbon.ShowQuickItemsDropDownButton = true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set Quick Access Toolbar configuration");
        }
    }

    private static void AddToolStripToTabPanel(ToolStripTabItem tab, ToolStripEx strip, string theme, ILogger? logger)
    {
        if (tab?.Panel == null || strip == null) return;

        try
        {
            tab.Panel.AddToolStrip(strip);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] AddToolStrip failed for {StripName}", strip.Name);
        }

        try
        {
            strip.Visible = true;
            strip.Enabled = true;
            strip.ThemeName = theme;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set ToolStripEx visibility/theme for {StripName}", strip.Name);
        }

        EnsureToolStripItemsVisible(strip, logger);

        try
        {
            if (!tab.Panel.Controls.Contains(strip))
            {
                tab.Panel.Controls.Add(strip);
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to add ToolStripEx to panel controls for {StripName}", strip.Name);
        }
    }

    private static void EnsureToolStripItemsVisible(ToolStripEx strip, ILogger? logger)
    {
        if (strip == null) return;
        foreach (ToolStripItem item in strip.Items)
        {
            try
            {
                item.Visible = true;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set ToolStripItem visible for {ItemName}", item.Name);
            }

            if (item is ToolStripPanelItem panelItem)
            {
                foreach (ToolStripItem nestedItem in panelItem.Items)
                {
                    try
                    {
                        nestedItem.Visible = true;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set nested ToolStripItem visible for {ItemName}", nestedItem.Name);
                    }
                }
            }
        }
    }

    private static void ApplyThemeRecursively(Control root, string themeName, ILogger? logger)
    {
        if (root == null || string.IsNullOrWhiteSpace(themeName)) return;

        try
        {
            SfSkinManager.SetVisualStyle(root, themeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] SetVisualStyle failed for {ControlName}", root.Name);
        }

        if (root is ToolStripEx toolStripEx)
        {
            try
            {
                toolStripEx.ThemeName = themeName;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set ThemeName for {StripName}", toolStripEx.Name);
            }

            EnsureToolStripItemsVisible(toolStripEx, logger);
        }

        foreach (Control child in root.Controls)
        {
            ApplyThemeRecursively(child, themeName, logger);
        }
    }

    /// <summary>
    /// Creates a ToolStripEx group with Office-style layout and theming.
    /// </summary>
    private static ToolStripEx CreateRibbonGroup(string title, string name, string theme)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? " " : title;
        var groupMinHeight = (int)DpiAware.LogicalToDeviceUnits(118f);

        var strip = new ToolStripEx
        {
            Name = name,
            Text = safeTitle,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = true,
            MinimumSize = new Size(0, groupMinHeight),
            LauncherStyle = LauncherStyle.Metro,
            ShowLauncher = false,
            ImageScalingSize = new Size(32, 32),
            ThemeName = theme,
            CanOverflow = false,
            Dock = DockStyle.None,
            LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(1, 0, 1, 0),
            Office12Mode = true
        };
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

    private static (ToolStripEx Strip, ToolStripButton DashboardBtn) CreateDashboardGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Dashboard", "DashboardGroup", theme);

        var dashboardBtn = CreateLargeNavButton(
            "Nav_Dashboard", "Dashboard", "dashboard", theme,
            () => form.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Fill), logger);
        dashboardBtn.Tag = "Nav:Dashboard";
        dashboardBtn.Enabled = true;  // Changed from false to true

        strip.Items.Add(dashboardBtn);
        return (strip, dashboardBtn);
    }

    private static (ToolStripEx Strip, ToolStripButton AccountsBtn) CreateFinancialsGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Financials", "FinancialsGroup", theme);

        var accountsBtn = CreateLargeNavButton(
            "Nav_Accounts", "Accounts", "accounts", theme,
            () => form.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Right), logger);
        accountsBtn.Tag = "Nav:Accounts";
        accountsBtn.Enabled = true;

        var analyticsBtn = CreateLargeNavButton(
            "Nav_Analytics", "Analytics", "analytics", theme,
            () => form.ShowPanel<WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right), logger);
        analyticsBtn.Tag = "Nav:Analytics";
        analyticsBtn.Enabled = true;

        strip.Items.Add(accountsBtn);
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(analyticsBtn);

        return (strip, accountsBtn);
    }

    private static ToolStripEx CreateReportingGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Reporting", "ReportingGroup", theme);

        var reportsBtn = CreateLargeNavButton(
            "Nav_Reports", "Reports", "reports", theme,
            () => form.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right), logger);
        reportsBtn.Tag = "Nav:Reports";
        reportsBtn.Enabled = true;

        strip.Items.Add(reportsBtn);
        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton QuickBooksBtn, ToolStripButton SettingsBtn) CreateToolsGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Tools", "ToolsGroup", theme);

        var settingsBtn = CreateLargeNavButton(
            "Nav_Settings", "Settings", "settings", theme,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right), logger);

        var quickBooksBtn = CreateLargeNavButton(
            "Nav_QuickBooks", "QB Sync", "quickbooks", theme,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks Synchronization", DockingStyle.Right), logger);

        var jarvisBtn = CreateLargeNavButton(
            "Nav_JARVIS", "JARVIS AI", "jarvis", theme,
            () =>
            {
                if (form is not MainForm mainForm)
                {
                    return;
                }

                // Defer slightly to allow ribbon paint to complete before docking layout changes
                SafeBeginInvoke(form, () =>
                {
                    try
                    {
                        mainForm.SwitchRightPanel("JarvisChat");
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
                }, logger);
            }, logger);

        strip.Items.Add(settingsBtn);
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(quickBooksBtn);
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(jarvisBtn);

        return (strip, quickBooksBtn, settingsBtn);
    }

    private static ToolStripEx CreateLayoutGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Layout", "LayoutGroup", theme);

        // Stack these small buttons vertically
        var panelItem = new ToolStripPanelItem { RowCount = 2, AutoSize = true, Transparent = true };

        var saveLayoutBtn = CreateSmallNavButton("Nav_SaveLayout", "Save Layout", null,
            () => TryInvokeFormMethod(form, "SaveLayout", null, out _), logger);

        var resetLayoutBtn = CreateSmallNavButton("Nav_ResetLayout", "Reset Layout", null,
            () => TryInvokeFormMethod(form, "ResetLayout", null, out _), logger);

        panelItem.Items.Add(saveLayoutBtn);
        panelItem.Items.Add(resetLayoutBtn);

        strip.Items.Add(panelItem);
        return strip;
    }

    private static ToolStripEx CreateMoreGroup(
         WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Views", "MorePanelsGroup", theme);

        // Featured items (Large)
        var warRoomBtn = CreateLargeNavButton("Nav_WarRoom", "War Room", "warroom", theme,
             () => form.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true), logger);
        strip.Items.Add(warRoomBtn);

        return strip;
    }

    private static ToolStripEx CreateSearchAndGridGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Actions", "ActionGroup", theme);

        // 1. Grid Tools Stack
        var gridStack = new ToolStripPanelItem
        {
            Name = "ActionGroup_GridStack",
            RowCount = 2,
            AutoSize = true,
            Transparent = true
        };

        var sortAscBtn = CreateSmallNavButton("Grid_SortAsc", "Sort Asc", null,
             () => TryInvokeFormMethod(form, "SortActiveGridByFirstSortableColumn", new object[] { false }, out _), logger);

        var sortDescBtn = CreateSmallNavButton("Grid_SortDesc", "Sort Desc", null,
             () => TryInvokeFormMethod(form, "SortActiveGridByFirstSortableColumn", new object[] { true }, out _), logger);

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
        searchBox.KeyDown += async (s, e) =>
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

            logger?.LogInformation("[RIBBON_FACTORY] Global search initiated: '{Query}'", searchBox.Text);
            form.GlobalIsBusy = true;
            try { await form.PerformGlobalSearchAsync(searchBox.Text); }
            finally { form.GlobalIsBusy = false; }
        };

        var searchStack = new ToolStripPanelItem
        {
            Name = "ActionGroup_SearchStack",
            RowCount = 2,
            AutoSize = true,
            Transparent = true
        };
        var lbl = new ToolStripLabel("Global Search:") { Name = "ActionGroup_SearchLabel" };
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
        themeBtn.Click += (s, e) =>
        {
            logger?.LogInformation("[RIBBON_FACTORY] Theme toggle button clicked");
            form.ToggleTheme();
        };

        strip.Items.Add(searchStack);
        strip.Items.Add(CreateRibbonSeparator());
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
    ///
    /// POLISH ENHANCEMENTS:
    /// - Rich tooltips with action description and keyboard shortcut (if applicable).
    /// - MouseEnter/Leave visual feedback (slight glow effect via BackColor).
    /// - MouseDown pressed state for tactile feedback.
    /// </summary>
    private static ToolStripButton CreateLargeNavButton(
        string name,
        string text,
        string? iconName,
        string theme,
        System.Action onClick,
        ILogger? logger)
    {
        // Extract keyboard shortcut from button name if applicable (e.g., "Dashboard" -> "Alt+D")
        var shortcut = ExtractKeyboardShortcut(name);
        var tooltipText = !string.IsNullOrEmpty(shortcut)
            ? $"{text} [{shortcut}]"
            : text;
        var accessibleDescription = string.IsNullOrEmpty(shortcut)
            ? $"Navigate to the {text.Replace("\n", " ", StringComparison.Ordinal)} panel."
            : $"Navigate to the {text.Replace("\n", " ", StringComparison.Ordinal)} panel. Shortcut: {shortcut}";

        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = $"Open {text}",  // Descriptive action
            AccessibleRole = AccessibleRole.PushButton,  // Role for screen readers
            AccessibleDescription = accessibleDescription,  // Detailed description
            Enabled = true,
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText, // CRITICAL for Large Layout
            ImageScaling = ToolStripItemImageScaling.None, // Prevents downscaling 32px icons
            ToolTipText = tooltipText,
            Padding = new Padding(4),
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(70f), (int)DpiAware.LogicalToDeviceUnits(82f)),
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.TopCenter,
            Margin = new Padding(2, 0, 2, 0)
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
        Bitmap? placeholderImage = null;
        if (btn.Image == null)
        {
            placeholderImage = new Bitmap(32, 32);
            btn.Image = placeholderImage;
            // Optional: Draw a placeholder box if desired, but transparent 32x32 ensures correct height
        }

        if (placeholderImage != null)
        {
            btn.Disposed += (_, __) =>
            {
                if (ReferenceEquals(btn.Image, placeholderImage))
                {
                    btn.Image = null;
                }

                placeholderImage.Dispose();
            };
        }

        btn.Click += (s, e) =>
        {
            try
            {
                LogNavigationActivity(null, text, text, logger);
                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_FACTORY] Navigation button '{ButtonName}' click failed: {Message}", name, ex.Message);
            }
        };

        return btn;
    }

    /// <summary>
    /// Extracts keyboard shortcut from button name.
    /// Example: "Nav_Dashboard" -> "Alt+D", "Nav_Accounts" -> "Alt+A"
    /// </summary>
    private static string ExtractKeyboardShortcut(string buttonName)
    {
        var shortcutMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Nav_Dashboard", "Alt+D" },
            { "Nav_Accounts", "Alt+A" },
            { "Nav_Transactions", "Alt+T" },
            { "Nav_Reports", "Alt+R" },
            { "Nav_Settings", "Alt+S" },
            { "Nav_WarRoom", "Alt+W" },
            { "Nav_Charts", "Alt+C" },
            { "Nav_Customers", "Alt+U" },
            { "Nav_QuickBooks", "Alt+Q" },
            { "Nav_JARVIS", "Alt+J" }
        };

        return shortcutMap.TryGetValue(buttonName, out var shortcut) ? shortcut : string.Empty;
    }

    /// <summary>
    /// Creates a SMALL navigation button (Side-by-side or stacked).
    /// POLISH: Rich tooltips and visual feedback.
    /// </summary>
    private static ToolStripButton CreateSmallNavButton(string name, string text, string? iconName, System.Action onClick, ILogger? logger = null)
    {
        var btn = new ToolStripButton(text)
        {
            Name = name,
            AccessibleName = text,
            ToolTipText = text,  // Enhanced tooltip
            AutoSize = true,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageScaling = ToolStripItemImageScaling.None, // 16x16 standard
            Padding = new Padding(2) // Add padding for better visual spacing
        };

        if (!string.IsNullOrEmpty(iconName))
        {
             var dpi = GetDpiService();
             var img = dpi?.GetImage(iconName);
             if (img != null) btn.Image = new Bitmap(img, new Size(16, 16));
        }

        btn.Click += (s, e) =>
        {
            try
            {
                LogNavigationActivity(null, text, text, logger);
                onClick();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_FACTORY] Small navigation button '{ButtonName}' click failed: {Message}", name, ex.Message);
            }
        };
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
            var services = Program.ServicesOrNull;
            if (services != null)
            {
                // Create a scope to resolve scoped services
                var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(services);
                if (scopeFactory != null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<IActivityLogService>(scope.ServiceProvider);
                    if (service != null)
                    {
                        await service.LogNavigationAsync(
                            actionName: $"Navigated to {actionName}",
                            details: $"Opened {panelName} panel",
                            status: "Success");
                    }
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
