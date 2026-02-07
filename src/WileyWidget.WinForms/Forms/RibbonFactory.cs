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
using WileyWidget.WinForms.Controls.Panels;
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
        catch (InvalidOperationException)
        {
            // Services may not be initialized during tests; return null gracefully
            return null;
        }
        catch (NullReferenceException)
        {
            // Service provider not available; return null gracefully
            return null;
        }
    }

    /// <summary>
    /// Safely executes an action with exception handling and logging.
    /// Replaces reflection-based TryInvokeFormMethod for compile-time safety.
    /// </summary>
    private static void SafeExecute(System.Action action, string operationName, ILogger? logger)
    {
        try
        {
            action();
        }
        catch (NullReferenceException nrEx)
        {
            logger?.LogError(nrEx, "[RIBBON_FACTORY] {OperationName} failed - null reference: {Message}", operationName, nrEx.Message);
        }
        catch (ObjectDisposedException odEx)
        {
            logger?.LogError(odEx, "[RIBBON_FACTORY] {OperationName} failed - object disposed: {Message}", operationName, odEx.Message);
        }
        catch (InvalidOperationException ioEx)
        {
            logger?.LogError(ioEx, "[RIBBON_FACTORY] {OperationName} failed - invalid operation: {Message}", operationName, ioEx.Message);
        }
        catch (ArgumentException argEx)
        {
            logger?.LogError(argEx, "[RIBBON_FACTORY] {OperationName} failed - invalid argument: {Message}", operationName, argEx.Message);
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
            // let Syncfusion theme control the visual style for Office2019 compatibility
            LauncherStyle = LauncherStyle.Metro,
            RibbonStyle = RibbonStyle.Office2016,
            // [FIX] Force full-width ribbon and stable height
            ShowRibbonDisplayOptionButton = true,  // Adds the display options button for customization (e.g., minimize ribbon)
            EnableSimplifiedLayoutMode = true,     // Enables toggle between normal and simplified (compact) layout for modern feel
            LayoutMode = RibbonLayoutMode.Normal,  // Start in normal mode; user can toggle via minimize button
            AutoSize = false,
            Size = new Size(form.ClientSize.Width, (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(180f)),
            MinimumSize = new Size(0, (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f)),
            MenuButtonVisible = true,
            MenuButtonText = "File"
        };

        // [FIX] Perform initialization within BeginInit/EndInit for proper internal Syncfusion state
        ribbon.BeginInit();

        // [PERF] Suspend layout during construction to prevent intermediate paint artifacts
        ribbon.SuspendLayout();

        logger?.LogDebug("[RIBBON_FACTORY] RibbonControlAdv created: Name={Name}, Dock={Dock}, RibbonStyle={Style}",
            ribbon.Name, ribbon.Dock, ribbon.RibbonStyle);

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
            // [FIX] Explicitly call SetVisualStyle to ensure all child internal components (like Header) are themed
            SfSkinManager.SetVisualStyle(ribbon, appTheme);
            ApplyRibbonStyleForTheme(ribbon, appTheme, logger);
            logger?.LogDebug("[RIBBON_FACTORY] Theme applied to ribbon: {Theme}", appTheme);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] Theme load failed. Falling back to default theme.");
            ribbon.ThemeName = AppThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(ribbon, AppThemeColors.DefaultTheme);
            ApplyRibbonStyleForTheme(ribbon, ribbon.ThemeName, logger);
        }

        // === ENABLE BACKSTAGE VIEW (File Menu) ===
        // WORKAROUND for NRE: Use a compatible theme like Office2016Colorful if Office2019 causes issues
        // Test and adjust based on your Syncfusion version (v.32.1.19)
        try
        {
            ribbon.MenuButtonEnabled = true;
            ribbon.BackStageView = null;  // Will be properly initialized by Syncfusion
            logger?.LogDebug("[RIBBON_FACTORY] BackStage menu enabled with theme: {Theme}", ribbon.ThemeName);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[RIBBON_FACTORY] BackStage initialization failed, disabling: {Message}", ex.Message);
            ribbon.MenuButtonEnabled = false;
            ribbon.BackStageView = null;
        }

        ConfigureRibbonAppearance(ribbon, logger);

        var currentThemeString = ribbon.ThemeName;
        var homeTab = new ToolStripTabItem { Text = "Home", Name = "HomeTab" };

        // [OFFICE2019] Enable auto-layout for proper ribbon group sizing
        ribbon.AutoLayoutToolStrip = true;

        logger?.LogDebug("[RIBBON_FACTORY] HomeTab created: Text={Text}, Name={Name}", homeTab.Text, homeTab.Name);

        // === CREATE GROUPS (ToolStripEx) ===
        // Per Demo: Each group is a separate ToolStripEx added to the Tab Panel.

        // 1. Core Navigation Group (Dashboard)
        var (dashboardStrip, dashboardBtn) = CreateCoreNavigationGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Core Navigation group created");

        // 2. Financials Group (Accounts, Budget, Budget Overview)
        var (financialsStrip, accountsBtn) = CreateFinancialsGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Financials group created");

        // 3. Reporting Group (Analytics, Reports)
        var reportingStrip = CreateReportingGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Reporting group created");

        // 4. Tools Group (Settings, QuickBooks, JARVIS Chat)
        var (toolsStrip, quickBooksBtn, settingsBtn) = CreateToolsGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Tools group created");

        // 5. Layout Group (Save Layout, Reset Layout, Lock Panels)
        var layoutStrip = CreateLayoutGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Layout group created");

        // 6. Secondary/More Group
        var moreStrip = CreateMoreGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] More group created");

        // 7. Search & Grid Controls (Combined or separate based on space)
        var searchAndGridStrip = CreateSearchAndGridGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] Search and Grid group created");

        // 0. File Group (New, Open, Save, Export) - RESTORED from disabled BackStage menu
        var fileStrip = CreateFileGroup(form, currentThemeString, logger);
        logger?.LogDebug("[RIBBON_FACTORY] File group created");

        // === ASSEMBLE RIBBON ===
        // [VISIBILITY] Add homeTab to ribbon header BEFORE adding toolstrips
        // This ensures ToolStripTabItem.Panel is fully initialized and attached to the ribbon
        // so AddToolStrip calls can correctly propagate layout information.
        ribbon.Header.AddMainItem(homeTab);
        logger?.LogDebug("[RIBBON_FACTORY] HomeTab added to ribbon header (MainItems count={ItemCount})", ribbon.Header.MainItems.Count);

        // Add distinct strips to the Home Tab Panel using AddToolStrip (CRITICAL for layout sizing)
        // [PRIORITY] Add File group first to maintain visibility of essential commands from former BackStage menu
        AddToolStripToTabPanel(homeTab, fileStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, dashboardStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, financialsStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, reportingStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, toolsStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, layoutStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, moreStrip, currentThemeString, logger);
        AddToolStripToTabPanel(homeTab, searchAndGridStrip, currentThemeString, logger);

        logger?.LogDebug("[RIBBON_FACTORY] All tool strips added to HomeTab panel");

        if (homeTab.Panel != null)
        {
            homeTab.Panel.AutoSize = true;
            homeTab.Panel.Padding = new Padding(6, 4, 6, 4);
        }

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
            ribbon.EndInit();
            ribbon.ResumeLayout();

            // [FIX] Explicitly select the Home tab to ensure its panel is displayed
            ribbon.SelectedTab = homeTab;

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
    /// Includes New, Open, Save, Export, Info, Settings tabs and Exit button.
    /// </summary>
    private static Syncfusion.Windows.Forms.BackStageView? CreateBackStage(
        WileyWidget.WinForms.Forms.MainForm form,
        RibbonControlAdv ribbon,
        ILogger? logger)
    {
        try
        {
            if (form == null)
            {
                logger?.LogError("[RIBBON_FACTORY] Cannot create BackStage - form reference is null");
                return null;
            }

            form.components ??= new System.ComponentModel.Container();
            var backStageView = new Syncfusion.Windows.Forms.BackStageView(form.components);
            var backStage = new Syncfusion.Windows.Forms.BackStage
            {
                BackStagePanelWidth = (int)DpiAware.LogicalToDeviceUnits(220f)
            };
            backStageView.BackStage = backStage;
            backStageView.HostForm = form;
            backStageView.HostControl = null;
            if (ribbon != null)
            {
                if (ribbon.IsHandleCreated)
                {
                    backStageView.HostControl = ribbon;
                }
                else
                {
                    ribbon.HandleCreated += (_, _) => backStageView.HostControl = ribbon;
                }
            }

            // NOTE: Do NOT apply SfSkinManager theming to BackStage/BackStageView directly.
            // Syncfusion BackStage has rendering issues with certain theme names (Office2019Colorful, etc.)
            // causing NullReferenceException in OnPaintPanelBackground. The BackStage will inherit
            // theme styling from the parent ribbon and form through CSS/theme cascade.
            // See: https://help.syncfusion.com/windowsforms/ribbon/backstage

            // === INFO TAB (Default) ===
            var infoTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Info",
                Name = "BackStage_Info"
            };

            var infoPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 1,
                RowCount = 5
            };

            var appNameLabel = new Label
            {
                Text = "Wiley Widget",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            var versionLabel = new Label
            {
                Text = $"Version {Application.ProductVersion}",
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            var descLabel = new Label
            {
                Text = "Municipal Financial Management System",
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            var copyrightLabel = new Label
            {
                Text = $"© {DateTime.Now.Year} All rights reserved.",
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Dock = DockStyle.Top
            };

            infoPanel.Controls.Add(appNameLabel, 0, 0);
            infoPanel.Controls.Add(versionLabel, 0, 1);
            infoPanel.Controls.Add(descLabel, 0, 2);
            infoPanel.Controls.Add(new Label { Height = 20 }, 0, 3);
            infoPanel.Controls.Add(copyrightLabel, 0, 4);

            var infoGroup = new GroupBox
            {
                Text = "About",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            infoGroup.Controls.Add(infoPanel);
            infoTab.Controls.Add(infoGroup);

            // === NEW TAB ===
            var newTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "New",
                Name = "BackStage_New"
            };

            var newPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = 2
            };
            newPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            newPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            newPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            newPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var newBudgetBtn = new SfButton { Text = "New Budget" };
            newBudgetBtn.Size = new Size(150, 40);
            newBudgetBtn.Click += (s, e) => SafeExecute(() => form?.CreateNewBudget(), "NewBudget", logger);

            var newAccountBtn = new SfButton { Text = "New Account" };
            newAccountBtn.Size = new Size(150, 40);
            newAccountBtn.Click += (s, e) => SafeExecute(() => form?.CreateNewAccount(), "NewAccount", logger);

            var newReportBtn = new SfButton { Text = "New Report" };
            newReportBtn.Size = new Size(150, 40);
            newReportBtn.Click += (s, e) => SafeExecute(() => form?.CreateNewReport(), "NewReport", logger);

            var createGroup = new GroupBox
            {
                Text = "Create",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            var createFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };
            createFlow.Controls.Add(newBudgetBtn);
            createFlow.Controls.Add(newAccountBtn);
            createFlow.Controls.Add(newReportBtn);
            createGroup.Controls.Add(createFlow);

            var templateGroup = new GroupBox
            {
                Text = "Templates",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            templateGroup.Controls.Add(new Label
            {
                Text = "Templates will appear here.",
                AutoSize = true,
                Dock = DockStyle.Top
            });

            newPanel.Controls.Add(createGroup, 0, 0);
            newPanel.Controls.Add(templateGroup, 1, 0);

            newTab.Controls.Add(newPanel);

            // === OPEN TAB ===
            var openTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Open",
                Name = "BackStage_Open"
            };

            var openPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = 1
            };
            openPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            openPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var openBudgetBtn = new SfButton { Text = "Open Budget..." };
            openBudgetBtn.Size = new Size(200, 40);
            openBudgetBtn.Click += (s, e) => SafeExecute(() => form?.OpenBudget(), "OpenBudget", logger);

            var openReportBtn = new SfButton { Text = "Open Report..." };
            openReportBtn.Size = new Size(200, 40);
            openReportBtn.Click += (s, e) => SafeExecute(() => form?.OpenReport(), "OpenReport", logger);

            var openGroup = new GroupBox
            {
                Text = "Open",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            var openFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };
            openFlow.Controls.Add(openBudgetBtn);
            openFlow.Controls.Add(openReportBtn);
            openGroup.Controls.Add(openFlow);

            var recentGroup = new GroupBox
            {
                Text = "Recent",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            recentGroup.Controls.Add(new Label
            {
                Text = "Recent files will appear here.",
                AutoSize = true,
                Dock = DockStyle.Top
            });

            openPanel.Controls.Add(openGroup, 0, 0);
            openPanel.Controls.Add(recentGroup, 1, 0);

            openTab.Controls.Add(openPanel);

            // === SETTINGS TAB ===
            var settingsTab = new Syncfusion.Windows.Forms.BackStageTab
            {
                Text = "Settings",
                Name = "BackStage_Settings"
            };

            var settingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = 1
            };
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var appSettingsBtn = new SfButton { Text = "Application Settings" };
            appSettingsBtn.Size = new Size(200, 40);
            appSettingsBtn.Click += (s, e) => SafeExecute(() => form?.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right), "ShowSettings", logger);

            var settingsGroup = new GroupBox
            {
                Text = "Application",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            settingsGroup.Controls.Add(appSettingsBtn);

            var preferencesGroup = new GroupBox
            {
                Text = "Preferences",
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            preferencesGroup.Controls.Add(new Label
            {
                Text = "Preferences will appear here.",
                AutoSize = true,
                Dock = DockStyle.Top
            });

            settingsPanel.Controls.Add(settingsGroup, 0, 0);
            settingsPanel.Controls.Add(preferencesGroup, 1, 0);
            settingsTab.Controls.Add(settingsPanel);

            // === BUTTONS (Save, Export, Exit) ===
            var saveBtn = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Save",
                Name = "BackStage_Save"
            };
            saveBtn.Click += (s, e) => SafeExecute(() => form?.SaveCurrentLayout(), "Save", logger);

            var exportBtn = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Export",
                Name = "BackStage_Export"
            };
            exportBtn.Click += (s, e) => SafeExecute(() => form?.ExportData(), "Export", logger);

            var separator = new Syncfusion.Windows.Forms.BackStageSeparator();

            var exitBtn = new Syncfusion.Windows.Forms.BackStageButton
            {
                Text = "Exit",
                Name = "BackStage_Exit"
            };
            exitBtn.Click += (s, e) => SafeExecute(() => form?.Close(), "Exit", logger);

            separator.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;
            exitBtn.Placement = Syncfusion.Windows.Forms.BackStageItemPlacement.Bottom;

            // Add tabs and buttons to BackStage.Controls per Syncfusion WinForms API
            backStage.Controls.Clear();
            backStage.Controls.Add(infoTab);
            backStage.Controls.Add(newTab);
            backStage.Controls.Add(openTab);
            backStage.Controls.Add(settingsTab);
            backStage.Controls.Add(saveBtn);
            backStage.Controls.Add(exportBtn);
            backStage.Controls.Add(separator);
            backStage.Controls.Add(exitBtn);

            // Set default selected tab (immediate + handle-created retry) to avoid renderer nulls on first paint
            SetBackStageDefaultTab(backStage, infoTab, logger);
            backStage.HandleCreated += (sender, args) => SetBackStageDefaultTab(backStage, infoTab, logger);

            logger?.LogInformation("[RIBBON_FACTORY] BackStage view created successfully with {TabCount} tabs and {ButtonCount} buttons",
                backStage.Controls.OfType<Syncfusion.Windows.Forms.BackStageTab>().Count(),
                backStage.Controls.OfType<Syncfusion.Windows.Forms.BackStageButton>().Count());

            return backStageView;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to create BackStage view");
            return null;
        }
    }

    /// <summary>
    /// Maps application theme names to BackStage renderer theme names.
    /// NOTE: This method is currently unused since BackStage is disabled.
    /// </summary>
    private static string GetBackStageThemeName(string appTheme)
    {
        if (appTheme.Contains("Office2019", StringComparison.OrdinalIgnoreCase))
            return "BackStage2019Renderer";
        if (appTheme.Contains("Office2016", StringComparison.OrdinalIgnoreCase))
            return "BackStage2016Renderer";
        return "BackStageRenderer";
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
            ApplyRibbonStyleForTheme(ribbon, themeName, logger);

            // [VISIBILITY] Add visible definition to the ribbon control
            ribbon.BorderStyle = ToolStripBorderStyle.Etched;

            // OFFICE COMPATIBILITY: Enable modern features found in Syncfusion "Office" demos
            ribbon.ShowCaption = true;
            // Removed non-existent properties: CaptionColor, RibbonDisplayOptionButton
            // Let SfSkinManager handle caption styling based on the active theme
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set RibbonStyle/BorderStyle/Caption");
        }

        try
        {
            // Menu button configuration removed - now controlled in CreateRibbon
            // to avoid theme conflicts when button is disabled
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set MenuButton configuration");
        }

        try
        {
            ribbon.QuickPanelVisible = true;
            ribbon.ShowQuickItemsDropDownButton = true;
            ribbon.TouchMode = false; // [FIX] Disable touch mode to ensure standard height (160) is sufficient
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
            // [FIX] Ensure strip is visible and themed BEFORE adding to the panel
            strip.Visible = true;
            strip.Enabled = true;
            strip.ThemeName = theme;

            // Apply theme to the strip's items as well
            EnsureToolStripItemsVisible(strip, logger);

            // Syncfusion: AddToolStrip handles the internal layout for Ribbon groups
            tab.Panel.AddToolStrip(strip);
            logger?.LogDebug("[RIBBON_FACTORY] ToolStripEx '{StripName}' added to panel", strip.Name);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] AddToolStrip failed for {StripName}", strip.Name);
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

        // BackStage rendering is sensitive to direct SfSkinManager theming.
        // Let it inherit from the ribbon/form instead of forcing visual styles.
        if (IsBackStageControl(root))
        {
            return;
        }

        try
        {
            SfSkinManager.SetVisualStyle(root, themeName);
        }
        catch (ObjectDisposedException odEx)
        {
            logger?.LogDebug(odEx, "[RIBBON_FACTORY] SetVisualStyle failed - control disposed for {ControlName}", root.Name);
        }
        catch (InvalidOperationException ioEx)
        {
            logger?.LogDebug(ioEx, "[RIBBON_FACTORY] SetVisualStyle failed - invalid operation for {ControlName}", root.Name);
        }
        catch (ArgumentException argEx)
        {
            logger?.LogDebug(argEx, "[RIBBON_FACTORY] SetVisualStyle failed - invalid argument for {ControlName}", root.Name);
        }

        if (root is ToolStripEx toolStripEx)
        {
            try
            {
                toolStripEx.ThemeName = themeName;
            }
            catch (ObjectDisposedException odEx)
            {
                logger?.LogDebug(odEx, "[RIBBON_FACTORY] Failed to set ThemeName - control disposed for {StripName}", toolStripEx.Name);
            }
            catch (InvalidOperationException ioEx)
            {
                logger?.LogDebug(ioEx, "[RIBBON_FACTORY] Failed to set ThemeName - invalid operation for {StripName}", toolStripEx.Name);
            }

            EnsureToolStripItemsVisible(toolStripEx, logger);
        }

        foreach (Control child in root.Controls)
        {
            ApplyThemeRecursively(child, themeName, logger);
        }
    }

    private static bool IsBackStageControl(Control control)
    {
        return control is Syncfusion.Windows.Forms.BackStage
            || control is Syncfusion.Windows.Forms.BackStageTab
            || control is Syncfusion.Windows.Forms.BackStageButton
            || control is Syncfusion.Windows.Forms.BackStageSeparator;
    }

    private static void ApplyRibbonStyleForTheme(RibbonControlAdv ribbon, string themeName, ILogger? logger)
    {
        if (ribbon == null)
        {
            return;
        }

        var styleName = themeName?.Contains("Office2019", StringComparison.OrdinalIgnoreCase) == true
            ? "Office2019"
            : "Office2016";

        if (!Enum.TryParse(styleName, true, out RibbonStyle ribbonStyle))
        {
            ribbonStyle = RibbonStyle.Office2016;
        }

        try
        {
            ribbon.RibbonStyle = ribbonStyle;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[RIBBON_FACTORY] Failed to set RibbonStyle for theme {Theme}", themeName);
        }
    }

    private static void SetBackStageDefaultTab(
        Syncfusion.Windows.Forms.BackStage backStage,
        Syncfusion.Windows.Forms.BackStageTab? preferredTab,
        ILogger? logger)
    {
        try
        {
            var tab = preferredTab ?? backStage.Controls.OfType<Syncfusion.Windows.Forms.BackStageTab>().FirstOrDefault();
            if (tab != null)
            {
                backStage.SelectedTab = tab;
            }
        }
        catch (Exception tabEx)
        {
            logger?.LogWarning(tabEx, "[RIBBON_FACTORY] Failed to set default BackStage tab");
        }
    }

    /// <summary>
    /// Creates a ToolStripEx group with Office2019-style layout and theming.
    /// Uses StackWithOverflow for proper button arrangement in square groups.
    /// </summary>
    private static ToolStripEx CreateRibbonGroup(string title, string name, string theme)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? " " : title;
        var groupMinHeight = (int)DpiAware.LogicalToDeviceUnits(100f);

        var strip = new ToolStripEx
        {
            Name = name,
            Text = safeTitle,  // Bottom label for group
            GripStyle = ToolStripGripStyle.Hidden,  // Clean look
            AutoSize = true,
            MinimumSize = new Size(0, groupMinHeight),
            LauncherStyle = LauncherStyle.Metro,
            ShowLauncher = true, // VISIBILITY: Shows the group dialog launcher for better definition
            ImageScalingSize = new Size(32, 32),  // 32px icons for large buttons
            ThemeName = theme,
            CanOverflow = false,
            Dock = DockStyle.None,
            LayoutStyle = ToolStripLayoutStyle.StackWithOverflow, // [OFFICE2019] Stack layout for square grid arrangement
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(1, 0, 1, 0),
            Office12Mode = true
        };

        // Add dummy handler to make Launcher button active (it hides if no handler in some versions)
        strip.LauncherClick += (s, e) =>
        {
            Serilog.Log.Information("[RIBBON] Launcher clicked for group: {GroupName}", title);
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

    /// <summary>
    /// Creates the "File" group with commands for creating, opening, and saving documents.
    /// These commands were previously in the disabled BackStage menu.
    /// </summary>
    private static ToolStripEx CreateFileGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("File", "FileGroup", theme);

        // New Budget
        var newBudgetBtn = CreateLargeNavButton(
            "File_NewBudget", "New\nBudget", "budget", theme,
            () =>
            {
                try
                {
                    form.CreateNewBudget();
                    logger?.LogDebug("[RIBBON_FACTORY] New Budget executed");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[RIBBON_FACTORY] New Budget command failed");
                }
            }, logger);
        newBudgetBtn.Tag = "File:NewBudget";
        newBudgetBtn.Enabled = true;

        // Open Budget
        var openBudgetBtn = CreateLargeNavButton(
            "File_OpenBudget", "Open\nBudget", "budget", theme,
            () =>
            {
                try
                {
                    form.OpenBudget();
                    logger?.LogDebug("[RIBBON_FACTORY] Open Budget executed");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[RIBBON_FACTORY] Open Budget command failed");
                }
            }, logger);
        openBudgetBtn.Tag = "File:OpenBudget";
        openBudgetBtn.Enabled = true;

        // Save Layout
        var saveLayoutBtn = CreateLargeNavButton(
            "File_SaveLayout", "Save\nLayout", "save", theme,
            () =>
            {
                try
                {
                    form.SaveCurrentLayout();
                    logger?.LogDebug("[RIBBON_FACTORY] Save Layout executed");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[RIBBON_FACTORY] Save Layout command failed");
                }
            }, logger);
        saveLayoutBtn.Tag = "File:SaveLayout";
        saveLayoutBtn.Enabled = true;

        // Export Data
        var exportDataBtn = CreateLargeNavButton(
            "File_ExportData", "Export\nData", "reports", theme,
            () =>
            {
                try
                {
                    form.ExportData();
                    logger?.LogDebug("[RIBBON_FACTORY] Export Data executed");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[RIBBON_FACTORY] Export Data command failed");
                }
            }, logger);
        exportDataBtn.Tag = "File:ExportData";
        exportDataBtn.Enabled = true;

        // [OFFICE2019] Square grouping - no separators, buttons arranged in grid
        strip.Items.Add(newBudgetBtn);
        strip.Items.Add(openBudgetBtn);
        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(exportDataBtn);

        logger?.LogDebug("[RIBBON_FACTORY] File group created with 4 commands");

        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton DashboardBtn) CreateCoreNavigationGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Core Navigation", "CoreNavigationGroup", theme);

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

        var paymentsBtn = CreateLargeNavButton(
            "Nav_Payments", "Payments", "quickbooks", theme,
            () => form.ShowPanel<PaymentsPanel>("Payments", DockingStyle.Right), logger);
        paymentsBtn.Tag = "Nav:Payments";
        paymentsBtn.Enabled = true;

        var budgetBtn = CreateLargeNavButton(
            "Nav_Budget", "Budget", "budget", theme,
            () => form.ShowPanel<BudgetPanel>("Budget", DockingStyle.Right), logger);
        budgetBtn.Tag = "Nav:Budget";
        budgetBtn.Enabled = true;

        var budgetOverviewBtn = CreateLargeNavButton(
            "Nav_BudgetOverview", "Budget\nOverview", "budgetoverview", theme,
            () => form.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Right), logger);
        budgetOverviewBtn.Tag = "Nav:BudgetOverview";
        budgetOverviewBtn.Enabled = true;

        // [OFFICE2019] Square grouping - no separators, buttons arranged in grid
        strip.Items.Add(accountsBtn);
        strip.Items.Add(paymentsBtn);
        strip.Items.Add(budgetBtn);
        strip.Items.Add(budgetOverviewBtn);

        return (strip, accountsBtn);
    }

    private static ToolStripEx CreateReportingGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Reporting", "ReportingGroup", theme);

        var analyticsBtn = CreateLargeNavButton(
            "Nav_Analytics", "Analytics", "analytics", theme,
            () => form.ShowPanel<WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right), logger);
        analyticsBtn.Tag = "Nav:Analytics";
        analyticsBtn.Enabled = true;

        var reportsBtn = CreateLargeNavButton(
            "Nav_Reports", "Reports", "reports", theme,
            () => form.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right), logger);
        reportsBtn.Tag = "Nav:Reports";
        reportsBtn.Enabled = true;

        // [OFFICE2019] Square grouping - Analytics and Reports side-by-side
        strip.Items.Add(analyticsBtn);
        strip.Items.Add(reportsBtn);
        return strip;
    }

    private static (ToolStripEx Strip, ToolStripButton QuickBooksBtn, ToolStripButton SettingsBtn) CreateToolsGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Tools", "ToolsGroup", theme);

        // Settings button
        var settingsBtn = CreateLargeNavButton(
            "Nav_Settings", "Settings", "settings", theme,
            () => form.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right), logger);
        settingsBtn.Tag = "Nav:Settings";
        settingsBtn.Enabled = true;

        // JARVIS Chat button (docked/floating)
        var jarvisBtn = CreateLargeNavButton(
            "Nav_JARVIS", "JARVIS\nChat", "jarvis", theme,
            () => form.ShowPanel<WileyWidget.WinForms.Controls.Supporting.JARVISChatUserControl>(
                "JARVIS Chat",
                DockingStyle.Bottom,
                allowFloating: true), logger);
        jarvisBtn.Tag = "Nav:JARVIS";
        jarvisBtn.Enabled = true;

        // JARVIS Chat modal button
        var jarvisModalBtn = CreateLargeNavButton(
            "Action_JARVISModal", "JARVIS\nModal", "jarvis", theme,
            () => form.ShowJarvisChatModal(), logger);
        jarvisModalBtn.Tag = "Action:JARVISModal";
        jarvisModalBtn.Enabled = true;

        // QuickBooks button
        var quickBooksBtn = CreateLargeNavButton(
            "Nav_QuickBooks", "QuickBooks", "quickbooks", theme,
            () => form.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right), logger);
        quickBooksBtn.Tag = "Nav:QuickBooks";
        quickBooksBtn.Enabled = true;

        // [OFFICE2019] Square grouping - Settings, QuickBooks, JARVIS, Modal arranged in grid
        strip.Items.Add(settingsBtn);
        strip.Items.Add(quickBooksBtn);
        strip.Items.Add(jarvisBtn);
        strip.Items.Add(jarvisModalBtn);

        return (strip, quickBooksBtn, settingsBtn);
    }

    private static ToolStripEx CreateLayoutGroup(
        WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Layout", "LayoutGroup", theme);

        // [OFFICE2019] Use large buttons for square grouping instead of small buttons
        var saveLayoutBtn = CreateLargeNavButton(
            "Nav_SaveLayout", "Save\nLayout", "save", theme,
            () => SafeExecute(() => form.SaveCurrentLayout(), "SaveLayout", logger), logger);
        saveLayoutBtn.ToolTipText = "Save current window layout (Ctrl+Shift+S)";
        saveLayoutBtn.Tag = "Layout:Save";
        saveLayoutBtn.AccessibleName = "Save Layout Button";
        saveLayoutBtn.AccessibleDescription = "Saves the current docking layout configuration to user preferences";
        saveLayoutBtn.Enabled = true;

        var resetLayoutBtn = CreateLargeNavButton(
            "Nav_ResetLayout", "Reset\nLayout", "reset", theme,
            () => SafeExecute(() => form.ResetLayout(), "ResetLayout", logger), logger);
        resetLayoutBtn.ToolTipText = "Reset to default window layout (Ctrl+Shift+R)";
        resetLayoutBtn.Tag = "Layout:Reset";
        resetLayoutBtn.AccessibleName = "Reset Layout Button";
        resetLayoutBtn.AccessibleDescription = "Resets the docking layout to factory default configuration";
        resetLayoutBtn.Enabled = true;

        var lockLayoutBtn = CreateLargeNavButton(
            "Nav_LockPanels", "Lock\nPanels", "lock", theme,
            () => SafeExecute(() => form.TogglePanelLocking(), "LockLayout", logger), logger);
        lockLayoutBtn.ToolTipText = "Lock/unlock panel docking (Ctrl+L)";
        lockLayoutBtn.Tag = "Layout:Lock";
        lockLayoutBtn.AccessibleName = "Lock Panels Button";
        lockLayoutBtn.AccessibleDescription = "Toggles panel locking to prevent accidental repositioning";
        lockLayoutBtn.Enabled = true;

        // [OFFICE2019] Square grouping - three buttons arranged in grid
        strip.Items.Add(saveLayoutBtn);
        strip.Items.Add(resetLayoutBtn);
        strip.Items.Add(lockLayoutBtn);

        logger?.LogDebug("[RIBBON_FACTORY] Layout group created with Save/Reset/Lock large buttons");

        return strip;
    }

    private static ToolStripEx CreateMoreGroup(
         WileyWidget.WinForms.Forms.MainForm form, string theme, ILogger? logger)
    {
        var strip = CreateRibbonGroup("Views", "MorePanelsGroup", theme);

        // Create gallery items for dynamic, Office-like presentation
        strip.Items.Add(CreateGalleryItem("War Room", "warroom", () => form.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true), logger));
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(CreateGalleryItem("Customers", "customers", () => form.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right), logger));
        strip.Items.Add(CreateGalleryItem("Utility Bills", "utilitybill", () => form.ShowPanel<UtilityBillPanel>("Utility Bills", DockingStyle.Right), logger));
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(CreateGalleryItem("Revenue Trends", "revenuetrends", () => form.ShowPanel<RevenueTrendsPanel>("Revenue Trends", DockingStyle.Right), logger));
        strip.Items.Add(CreateGalleryItem("Recommended Charge", "recommendedcharge", () => form.ShowPanel<RecommendedMonthlyChargePanel>("Recommended Monthly Charge", DockingStyle.Right), logger));
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(CreateGalleryItem("Dept Summary", "deptsummary", () => form.ShowPanel<WileyWidget.WinForms.Controls.Analytics.DepartmentSummaryPanel>("Department Summary", DockingStyle.Right), logger));
        strip.Items.Add(CreateGalleryItem("Insight Feed", "insightfeed", () => form.ShowPanel<WileyWidget.WinForms.Controls.Analytics.InsightFeedPanel>("Insight Feed", DockingStyle.Right), logger));
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(CreateGalleryItem("Activity Log", "activitylog", () => form.ShowPanel<ActivityLogPanel>("Activity Log", DockingStyle.Bottom, allowFloating: true), logger));
        strip.Items.Add(CreateGalleryItem("Audit Log", "auditlog", () => form.ShowPanel<AuditLogPanel>("Audit Log", DockingStyle.Bottom), logger));

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
             () => SafeExecute(() => form.SortActiveGridByFirstSortableColumn(false), "SortAscending", logger), logger);

        var sortDescBtn = CreateSmallNavButton("Grid_SortDesc", "Sort Desc", null,
             () => SafeExecute(() => form.SortActiveGridByFirstSortableColumn(true), "SortDescending", logger), logger);

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

        // 3. Theme Combo Box (Replaces toggle button for more intuitive selection)
        var themeCombo = new ToolStripComboBoxEx
        {
            Name = "ThemeCombo",
            Text = "Theme",
            AutoSize = false,
            Width = (int)DpiAware.LogicalToDeviceUnits(140f),
            AccessibleName = "Theme Selection",
            AccessibleRole = AccessibleRole.ComboBox,
            AccessibleDescription = "Select application theme (Office2019Colorful, Office2019White, Office2019Black, Office2016Colorful, Office2016White)"
        };

        // Add available themes to combo
        themeCombo.Items.AddRange(new object[]
        {
            "Office2019Colorful",
            "Office2019White",
            "Office2019Black",
            "Office2016Colorful",
            "Office2016White"
        });

        // Set default theme (try to get current theme from ribbon)
        themeCombo.SelectedIndex = 0;

        themeCombo.SelectedIndexChanged += (s, e) =>
        {
            var selectedTheme = themeCombo.Text;
            if (string.IsNullOrWhiteSpace(selectedTheme))
            {
                return;
            }

            try
            {
                // Ensure theme assembly is loaded
                AppThemeColors.EnsureThemeAssemblyLoaded(logger);

                SfSkinManager.ApplicationVisualTheme = selectedTheme;
                SfSkinManager.SetVisualStyle(form, selectedTheme);
                ApplyThemeRecursively(form, selectedTheme, logger);

                // Refresh ribbon and main form
                form.PerformLayout();
                form.Refresh();

                logger?.LogInformation("[RIBBON_FACTORY] Theme changed to {Theme}", selectedTheme);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_FACTORY] Failed to apply theme {Theme}: {Message}", selectedTheme, ex.Message);
            }
        };

        strip.Items.Add(searchStack);
        strip.Items.Add(CreateRibbonSeparator());
        strip.Items.Add(gridStack);
        strip.Items.Add(themeCombo);

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
    /// FLATICON SUPPORT: Automatically uses flat icons for modern ribbon styles (Office2016+).
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
            Padding = new Padding(4, 2, 4, 2),
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(70f), (int)DpiAware.LogicalToDeviceUnits(80f)),
            TextAlign = ContentAlignment.BottomCenter,
            ImageAlign = ContentAlignment.TopCenter,
            Margin = new Padding(3, 1, 3, 1) // Increased for separation
        };

        // [OFFICE2019] Load 32px icon using LoadBackStageIcon for consistency
        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var img = LoadBackStageIcon(iconName, new Size(32, 32), logger);
                if (img != null)
                {
                    btn.Image = img;
                    logger?.LogDebug("[RIBBON_FACTORY] Loaded 32px icon: {IconName}", iconName);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load icon {IconName}", iconName);
            }
        }

        // Fix: Ensure Image is not null to prevent layout collapse (use placeholder)
        if (btn.Image == null)
        {
            btn.Image = CreatePlaceholderIcon(new Size(32, 32));
            logger?.LogDebug("[RIBBON_FACTORY] Created placeholder icon for button: {ButtonName}", name);
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
            Padding = new Padding(4, 2, 4, 2), // Improved definition
            Margin = new Padding(2, 1, 2, 1)  // Improved separation
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

    /// <summary>
    /// Creates a gallery item for the "Views" group with icon and text.
    /// </summary>
    private static ToolStripButton CreateGalleryItem(
        string text, string iconName, Action onClick, ILogger? logger)
    {
        var item = new ToolStripButton(text)
        {
            Text = text,
            TextImageRelation = TextImageRelation.ImageAboveText,
            AutoSize = false,
            Size = new Size((int)DpiAware.LogicalToDeviceUnits(65f), (int)DpiAware.LogicalToDeviceUnits(75f)),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            ImageScaling = ToolStripItemImageScaling.None,
            Padding = new Padding(3, 1, 3, 1),
            Margin = new Padding(2, 1, 2, 1)
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            try
            {
                var img = LoadBackStageIcon(iconName, new Size(32, 32), logger);
                if (img != null)
                {
                    item.Image = img;
                    logger?.LogDebug("[RIBBON_FACTORY] Loaded gallery item icon: {IconName}", iconName);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[RIBBON_FACTORY] Failed to load gallery item icon {IconName}", iconName);
            }
        }

        if (item.Image == null)
        {
            item.Image = CreatePlaceholderIcon(new Size(32, 32));
        }

        item.Click += (s, e) =>
        {
            try
            {
                onClick();
                logger?.LogDebug("[RIBBON_FACTORY] Gallery item '{ItemText}' clicked", text);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[RIBBON_FACTORY] Gallery item '{ItemText}' click failed", text);
            }
        };

        return item;
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

    /// <summary>
    /// Determines if flat icons should be used based on the current ribbon style.
    /// Flat icons are used for modern styles (Office2013, Office2016, Office2019).
    /// </summary>
    private static bool ShouldUseFlatIcons()
    {
        try
        {
            // Check the application theme to determine if flat icons are appropriate
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";

            // Use flat icons for Office2013, Office2016, and Office2019 themes
            return currentTheme.Contains("Office2013", StringComparison.OrdinalIgnoreCase) ||
                   currentTheme.Contains("Office2016", StringComparison.OrdinalIgnoreCase) ||
                   currentTheme.Contains("Office2019", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Default to using flat icons for modern look
            return true;
        }
    }

    /// <summary>
    /// Loads and scales an icon from FlatIcons resources.
    /// Falls back to placeholder if icon not found.
    /// </summary>
    private static Image? LoadBackStageIcon(string iconName, Size targetSize, ILogger? logger)
    {
        try
        {
            var dpiService = GetDpiService();
            if (dpiService != null)
            {
                // Try to load from DPI-aware service first
                var img = dpiService.GetScaledImage(iconName, targetSize);
                if (img != null)
                {
                    logger?.LogDebug("[RIBBON_FACTORY] Loaded icon via DPI service: {IconName}", iconName);
                    return img;
                }
            }

            // Fallback: Try to load from embedded resources in FlatIcons folder
            // Append "flat" suffix for modern Office2016/2019 ribbon styles
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}flat.png";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var originalImage = Image.FromStream(stream);

                // If image is already the target size, return it directly
                if (originalImage.Size == targetSize)
                {
                    logger?.LogDebug("[RIBBON_FACTORY] Loaded icon from embedded resource (exact size): {IconName}", iconName);
                    return originalImage;
                }

                // Scale to target size with high quality
                var scaledImage = new Bitmap(targetSize.Width, targetSize.Height);
                using (var graphics = Graphics.FromImage(scaledImage))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.DrawImage(originalImage, 0, 0, targetSize.Width, targetSize.Height);
                }
                originalImage.Dispose();

                logger?.LogDebug("[RIBBON_FACTORY] Loaded and scaled icon from embedded resource: {IconName}", iconName);
                return scaledImage;
            }

            // Last fallback: Try file system path with flat suffix
            var flatIconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "FlatIcons", $"{iconName}flat.png");

            if (System.IO.File.Exists(flatIconPath))
            {
                var originalImage = Image.FromFile(flatIconPath);

                if (originalImage.Size == targetSize)
                {
                    logger?.LogDebug("[RIBBON_FACTORY] Loaded icon from file (exact size): {IconName}", iconName);
                    return originalImage;
                }

                // Scale to target size
                var scaledImage = new Bitmap(targetSize.Width, targetSize.Height);
                using (var graphics = Graphics.FromImage(scaledImage))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.DrawImage(originalImage, 0, 0, targetSize.Width, targetSize.Height);
                }
                originalImage.Dispose();

                logger?.LogDebug("[RIBBON_FACTORY] Loaded and scaled icon from file: {IconName}", iconName);
                return scaledImage;
            }

            logger?.LogWarning("[RIBBON_FACTORY] Icon resource not found, using placeholder: {IconName}", iconName);
            return CreatePlaceholderIcon(targetSize);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[RIBBON_FACTORY] Failed to load icon, using placeholder: {IconName}", iconName);
            return CreatePlaceholderIcon(targetSize);
        }
    }

    /// <summary>
    /// Creates a simple placeholder icon when resource is missing.
    /// </summary>
    private static Image CreatePlaceholderIcon(Size size)
    {
        var placeholder = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(placeholder))
        {
            graphics.Clear(Color.LightGray);
            using (var pen = new Pen(Color.DarkGray, 2))
            {
                graphics.DrawRectangle(pen, 2, 2, size.Width - 4, size.Height - 4);
            }
            // Draw an 'X' to make it obvious it's a placeholder
            using (var pen = new Pen(Color.DarkGray, 1))
            {
                graphics.DrawLine(pen, 4, 4, size.Width - 4, size.Height - 4);
                graphics.DrawLine(pen, size.Width - 4, 4, 4, size.Height - 4);
            }
        }
        return placeholder;
    }
}
