using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;

using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private static readonly string[] ThemeCycle = new[]
    {
        "Office2019Colorful",
        "Office2019Dark",
        "Office2019Black",
        "Office2019White",
        "Office2019DarkGray"
    };
    private MenuStrip? _menuStrip;
    private ToolStripMenuItem? _recentFilesMenu;
    private RibbonControlAdv? _ribbon;
    private ToolStripTabItem? _homeTab;
    private ToolStripEx? _navigationStrip;
    private StatusBarAdv? _statusBar;
    private StatusBarAdvPanel? _statusLabel;
    private StatusBarAdvPanel? _statusTextPanel;
    private StatusBarAdvPanel? _statePanel;
    private StatusBarAdvPanel? _progressPanel;
    private Syncfusion.Windows.Forms.Tools.ProgressBarAdv? _progressBar;
    private StatusBarAdvPanel? _clockPanel;
    private System.Windows.Forms.Timer? _statusTimer;
    private ToolStripTextBox? _globalSearchTextBox;
    private bool _chromeInitialized;

    /// <summary>
    /// Initialize UI chrome elements (Ribbon, Status Bar, Navigation).
    /// Call this from constructor after configuration is loaded.
    /// </summary>
    private void InitializeChrome()
    {
        if (DesignMode)
        {
            return;
        }

        if (_chromeInitialized)
        {
            _logger?.LogDebug("InitializeChrome skipped - chrome already initialized");
            return;
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Chrome Initialization");

        var chromeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogInformation("InitializeChrome start - handleCreated={HandleCreated}", IsHandleCreated);

        try
        {
            // Enable Per-Monitor V2 DPI Awareness (syncs with app.manifest)
            AutoScaleMode = AutoScaleMode.Dpi;

            // Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainForm";
            KeyPreview = true;
            this.KeyDown -= MainForm_KeyDown;
            this.KeyDown += MainForm_KeyDown;

            // RibbonForm chrome customization (Syncfusion RibbonForm API)
            // Promotes the title-bar application icon from 16×16 (default) to 32×32.
            IconSize = new Size(32, 32);
            // EnableAeroTheme = false is required for TopLeftRadius to render visually.
            EnableAeroTheme = false;
            // Curved top-left corner for visual interest (default = 8; max recommended = 20).
            TopLeftRadius = 20;

            // NOTE: SfForm.Style properties removed - RibbonForm uses different theming API
            // Title bar styling is handled by Syncfusion theming via OfficeColorScheme

            // Initialize components container if needed
            components ??= new System.ComponentModel.Container();
            _logger?.LogInformation("Components container initialized");

            // Establish default Escape key behavior
            EnsureDefaultActionButtons();

            // Single chrome path: Ribbon is primary. Use native menu only when ribbon is disabled.
            if (!_uiConfig.ShowRibbon)
            {
                InitializeMenuBar();
                _logger?.LogInformation("Menu bar initialized (ribbon disabled)");
            }

            if (_uiConfig.ShowRibbon)
            {
                var ribbonPhaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                InitializeRibbon();
                if (!IsEffectivelyUiTestRuntime())
                {
                    _ribbon?.Refresh();
                }
                ribbonPhaseStopwatch.Stop();
                _logger?.LogInformation("Ribbon init in {Ms}ms", ribbonPhaseStopwatch.ElapsedMilliseconds);
                if (_ribbon == null)
                {
                    _logger?.LogError("Ribbon initialization returned null");
                }
                else
                {
                    _logger?.LogInformation("Ribbon initialized");
                }
            }
            else
            {
                _logger?.LogInformation("Ribbon initialization skipped because UI:ShowRibbon is false");
            }

            // Initialize Status Bar
            var statusBarStopwatch = System.Diagnostics.Stopwatch.StartNew();
            InitializeStatusBar();
            statusBarStopwatch.Stop();
            _logger?.LogInformation("StatusBar init in {Ms}ms", statusBarStopwatch.ElapsedMilliseconds);
            _logger?.LogInformation("Status bar initialized");

            // Tabbed MDI layout is already constrained beneath the ribbon; no extra docking host initialization needed.

            // ── PERF FIX: Make Ribbon and Navigation Strip mutually exclusive (saves ~40ms in test mode)
            // Initialize Navigation Strip ONLY when Ribbon is disabled (alternative to Ribbon for test harness)
            if (IsEffectivelyUiTestRuntime() && !_uiConfig.ShowRibbon)
            {
                InitializeNavigationStrip();
                _logger?.LogInformation("Navigation strip initialized (UI test runtime mode, ribbon disabled)");

                // ── CRITICAL FIX: Attach the strip so FlaUI (and users) can actually SEE the buttons ──
                if (_navigationStrip != null && !this.Controls.Contains(_navigationStrip))
                {
                    this.Controls.Add(_navigationStrip);
                    _navigationStrip.Dock = DockStyle.Top;
                    _navigationStrip.BringToFront();
                    _navigationStrip.PerformLayout();
                    _logger?.LogDebug("NavigationStrip attached to form for UI test runtime");
                }
            }
            else if (IsEffectivelyUiTestRuntime() && _uiConfig.ShowRibbon)
            {
                _logger?.LogDebug("NavigationStrip skipped - Ribbon is active (performance optimization)");
            }

            // Start status timer
            InitializeStatusTimer();

            // Req 1: Subscribe to runtime theme changes so ThemeName propagates to all Syncfusion controls.
            // Unsubscription happens in Dispose (see OnThemeServiceChanged).
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= OnThemeServiceChanged;
                _themeService.ThemeChanged += OnThemeServiceChanged;
                _logger?.LogDebug("MainForm subscribed to IThemeService.ThemeChanged");
            }

            _chromeInitialized = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
        }
        finally
        {
            chromeStopwatch.Stop();
            _logger?.LogInformation("InitializeChrome completed in {Ms}ms", chromeStopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for primary navigation, global search, and session theme toggle.
    /// </summary>
    private void InitializeRibbon()
    {
        if (_ribbon != null && !_ribbon.IsDisposed)
        {
            _logger?.LogDebug("InitializeRibbon skipped - existing ribbon already initialized");
            return;
        }

        var isUiTestRuntime = IsEffectivelyUiTestRuntime();

        _logger?.LogInformation("InitializeRibbon: Starting ribbon initialization");
        var ribbonStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            AppThemeColors.EnsureThemeAssemblyLoaded(_logger);

            var ribbon = _controlFactory.CreateRibbonControlAdv("File", r =>
            {
                r.Name = "Ribbon_Main";
                r.AccessibleName = "SfRibbon";
                r.LauncherStyle = LauncherStyle.Metro;
                r.RibbonStyle = RibbonStyle.Office2016;
                r.ShowRibbonDisplayOptionButton = !isUiTestRuntime;
                r.EnableSimplifiedLayoutMode = true;
                r.LayoutMode = RibbonLayoutMode.Normal;
                r.AutoSize = false;
                r.Size = new Size(ClientSize.Width, (int)DpiAware.LogicalToDeviceUnits(180f));
                r.MinimumSize = new Size(0, (int)DpiAware.LogicalToDeviceUnits(120f));
                r.MenuButtonVisible = true;
                r.MenuButtonText = "File";

                if (isUiTestRuntime)
                {
                    r.ShowCaption = false;
                    r.MenuButtonEnabled = false;
                    r.MenuButtonVisible = false;
                }
            });

            ribbon.BeginInit();
            ribbon.SuspendLayout();

            if (ribbon.IsDisposed || ribbon.Disposing)
            {
                throw new InvalidOperationException("Ribbon control disposed during initialization");
            }

            try
            {
                var appTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
                if (appTheme.StartsWith("Office2019", StringComparison.OrdinalIgnoreCase))
                {
                    AppThemeColors.EnsureThemeAssemblyLoaded(_logger);
                }

                ribbon.ThemeName = appTheme;
                SfSkinManager.SetVisualStyle(ribbon, appTheme);
                ApplyRibbonStyleForTheme(ribbon, appTheme, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "InitializeRibbon: Theme load failed, falling back to default theme");
                ribbon.ThemeName = AppThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(ribbon, AppThemeColors.DefaultTheme);
                ApplyRibbonStyleForTheme(ribbon, ribbon.ThemeName, _logger);
            }

            Syncfusion.Windows.Forms.BackStageView? backStageView = null;
            if (!isUiTestRuntime)
            {
                try
                {
                    ribbon.MenuButtonEnabled = true;
                    backStageView = CreateBackStage(this, ribbon, _logger);
                    if (backStageView == null)
                    {
                        ribbon.MenuButtonEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "InitializeRibbon: BackStage initialization failed; disabling File menu");
                    ribbon.MenuButtonEnabled = false;
                    backStageView = null;
                }
            }
            else
            {
                ribbon.MenuButtonEnabled = false;
            }

            if (backStageView != null)
            {
                ribbon.BackStageView = backStageView;
            }

            ConfigureRibbonAppearance(ribbon, _logger, isUiTestRuntime);

            var currentThemeString = ResolveRibbonThemeName(SfSkinManager.ApplicationVisualTheme ?? ribbon.ThemeName, _logger);
            ribbon.ThemeName = currentThemeString;
            SfSkinManager.SetVisualStyle(ribbon, currentThemeString);

            var homeTab = new ToolStripTabItem { Text = "Home", Name = "HomeTab" };
            CompleteToolStripTabItemAPI(homeTab, _logger);

            var financialsTab = new ToolStripTabItem { Text = "Financials", Name = "FinancialsTab" };
            var analyticsTab = new ToolStripTabItem { Text = "Analytics & Reports", Name = "AnalyticsTab" };
            var utilitiesTab = new ToolStripTabItem { Text = "Utilities", Name = "UtilitiesTab" };
            var administrationTab = new ToolStripTabItem { Text = "Administration", Name = "AdministrationTab" };
            CompleteToolStripTabItemAPI(financialsTab, _logger);
            CompleteToolStripTabItemAPI(analyticsTab, _logger);
            CompleteToolStripTabItemAPI(utilitiesTab, _logger);
            CompleteToolStripTabItemAPI(administrationTab, _logger);

            try
            {
                // Explicit AutomationId assignments for UI automation stability
                SetAutomationId(homeTab, "Tab_Home", _logger);
                SetAutomationId(financialsTab, "Tab_Financials", _logger);
                SetAutomationId(analyticsTab, "Tab_Analytics", _logger);
                SetAutomationId(utilitiesTab, "Tab_Utilities", _logger);
                SetAutomationId(administrationTab, "Tab_Administration", _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: setting AutomationId on tabs failed");
            }

            // ── Home tab groups ──────────────────────────────────────────────
            var (dashboardStrip, _) = CreateCoreNavigationGroup(this, currentThemeString, _logger);
            var (layoutStrip, _, _, lockLayoutBtn) = CreateLayoutGroup(this, currentThemeString, _logger);
            var searchAndGridStrip = CreateSearchAndGridGroup(this, currentThemeString, _logger);

            // ── Financials tab groups ────────────────────────────────────────
            var (financialsStrip, _) = CreateFinancialsGroup(this, currentThemeString, _logger);
            var paymentsStrip = CreatePaymentsGroup(this, currentThemeString, _logger);
            var integrationStrip = CreateIntegrationGroup(this, currentThemeString, _logger);

            // ── Analytics & Reports tab groups ───────────────────────────────
            var analyticsStrip = CreateAnalyticsGroup(this, currentThemeString, _logger);
            var reportingStrip = CreateReportingGroup(this, currentThemeString, _logger);
            var operationsStrip = CreateOperationsGroup(this, currentThemeString, _logger);

            // ── Utilities tab groups ─────────────────────────────────────────
            var utilitiesStrip = CreateUtilitiesGroup(this, currentThemeString, _logger);

            // ── Administration tab groups ────────────────────────────────────
            var administrationStrip = CreateAdministrationGroup(this, currentThemeString, _logger);
            var auditLogsStrip = CreateAuditLogsGroup(this, currentThemeString, _logger);

            ribbon.Header.AddMainItem(homeTab);
            ribbon.Header.AddMainItem(financialsTab);
            ribbon.Header.AddMainItem(analyticsTab);
            ribbon.Header.AddMainItem(utilitiesTab);
            ribbon.Header.AddMainItem(administrationTab);

            ToolStripTabItem? layoutContextTab = null;
            ToolStripTabGroup? layoutTabGroup = null;
            try
            {
                (layoutContextTab, layoutTabGroup) = TryCreateLayoutContextualTabGroup(this, ribbon, currentThemeString, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Failed to create layout contextual tab group");
            }

            try
            {
                lockLayoutBtn.Click += (_, _) => ToggleLayoutContextualTab(ribbon, homeTab, layoutContextTab, layoutTabGroup, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Failed to attach LockPanels contextual toggle");
            }

            // Home
            AddToolStripToTabPanel(homeTab, dashboardStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(homeTab, layoutStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(homeTab, searchAndGridStrip, currentThemeString, _logger);

            // Financials
            AddToolStripToTabPanel(financialsTab, financialsStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(financialsTab, paymentsStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(financialsTab, integrationStrip, currentThemeString, _logger);

            // Analytics & Reports
            AddToolStripToTabPanel(analyticsTab, analyticsStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(analyticsTab, reportingStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(analyticsTab, operationsStrip, currentThemeString, _logger);

            // Utilities
            AddToolStripToTabPanel(utilitiesTab, utilitiesStrip, currentThemeString, _logger);

            // Administration
            AddToolStripToTabPanel(administrationTab, administrationStrip, currentThemeString, _logger);
            AddToolStripToTabPanel(administrationTab, auditLogsStrip, currentThemeString, _logger);

            // Launcher handlers must be attached after tabs/groups exist.
            AttachRibbonLauncherHandlers(this, ribbon, _logger);

            foreach (var tab in new[] { homeTab, financialsTab, analyticsTab, utilitiesTab, administrationTab })
            {
                if (tab.Panel != null)
                {
                    tab.Panel.AutoSize = true;
                    tab.Panel.Padding = new Padding(6, 4, 6, 4);
                }
            }

            try
            {
                ApplyThemeRecursively(ribbon, currentThemeString, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Theme recursion failed");
            }

            try
            {
                if (homeTab.Panel != null)
                {
                    if (!isUiTestRuntime)
                    {
                        homeTab.Panel.PerformLayout();
                        homeTab.Panel.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Home tab panel refresh failed");
            }

            ((System.ComponentModel.ISupportInitialize)ribbon).EndInit();
            ribbon.ResumeLayout(false);
            if (!isUiTestRuntime)
            {
                ribbon.PerformLayout();
            }

            try
            {
                ribbon.SelectedTab = homeTab;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "InitializeRibbon: Could not select Home tab");
            }

            if (!isUiTestRuntime)
            {
                ribbon.Invalidate();
            }

            if (ribbon.Header.MainItems.Count == 0)
            {
                var fallbackTab = new ToolStripTabItem { Text = "Home", Name = "FallbackHomeTab" };
                ribbon.Header.AddMainItem(fallbackTab);
            }

            // Assign ribbon fields before initializing QAT so QAT can reliably access RibbonControlAdv.
            _ribbon = ribbon;
            _homeTab = homeTab;

            try
            {
                InitializeQuickAccessToolbar(currentThemeString, isUiTestRuntime);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "InitializeRibbon: QAT initialization failed");
            }

            try
            {
                AttachRibbonLayoutHandlers(this, ribbon, homeTab, layoutContextTab, isUiTestRuntime, _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Failed to attach ribbon layout handlers");
            }

            try
            {
                _logger?.LogDebug("InitializeRibbon: Ribbon HomeTab groups loaded: {GroupCount}", homeTab.Panel?.Controls.OfType<ToolStripEx>().Count() ?? 0);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "InitializeRibbon: Failed to log ribbon group count");
            }

            try { ribbon.AccessibleName = "Ribbon_Main"; } catch (Exception ex) { _logger?.LogDebug(ex, "InitializeRibbon: setting ribbon AccessibleName failed"); }
            try { SetAutomationId(ribbon, "Ribbon_Main", _logger); } catch (Exception ex) { _logger?.LogDebug(ex, "InitializeRibbon: setting ribbon AutomationId failed"); }
            try { ribbon.AccessibleDescription ??= "Main application ribbon for navigation, search, and grid tools"; } catch { }
            try { ribbon.TabIndex = 1; ribbon.TabStop = true; } catch { }

            CacheGlobalSearchTextBox();
            EnsureRibbonAccessibility();

            // Allow FlaUI-driven automation to visually attach the ribbon so UIA can find navigation buttons.
            var skipRibbonVisualAttach = isUiTestRuntime && !IsFlaUiAutomationTest();

            // Add ribbon to form if not already present.
            // In explicit UI test harness runtime, keep ribbon detached to avoid Syncfusion non-client paint crashes.
            if (!skipRibbonVisualAttach && !_ribbon.IsDisposed && !this.Controls.Contains(_ribbon))
            {
                this.Controls.Add(_ribbon);
                _ribbon.Dock = DockStyleEx.Top;
                _ribbon.SendToBack(); // Ensure it is below any future fill-docked controls
                _ribbon.BringToFront(); // But visually on top of content

                if (!isUiTestRuntime)
                {
                    _ribbon.PerformLayout();
                    _ribbon.Refresh();
                }
            }
            else if (skipRibbonVisualAttach)
            {
                _logger?.LogDebug("InitializeRibbon: Skipping ribbon visual attach in UI test harness runtime");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "InitializeRibbon failed to create ribbon");
            _ribbon = null;
            _homeTab = null;
        }
        finally
        {
            ribbonStopwatch.Stop();
            var ribbonTabCount = _ribbon?.Header?.MainItems.Count ?? 0;
            _logger?.LogInformation("Ribbon created in {Ms}ms | Tabs={Count}", ribbonStopwatch.ElapsedMilliseconds, ribbonTabCount);
            _logger?.LogInformation("Ribbon/Chrome init completed in {Ms}ms", ribbonStopwatch.ElapsedMilliseconds);
        }
    }

    private void EnsureChromeZOrder()
    {
        try
        {
            if (_menuStrip != null && !_menuStrip.IsDisposed && _menuStrip.Visible)
            {
                _menuStrip.BringToFront();
            }

            if (_ribbon != null && !_ribbon.IsDisposed && _ribbon.Visible)
            {
                _ribbon.BringToFront();
            }

            if (_navigationStrip != null && !_navigationStrip.IsDisposed && _navigationStrip.Visible)
            {
                _navigationStrip.BringToFront();
            }

            if (_statusBar != null && !_statusBar.IsDisposed && _statusBar.Visible)
            {
                _statusBar.BringToFront();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "EnsureChromeZOrder: failed to enforce chrome z-order");
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        try
        {
            _ribbon?.PerformLayout();
            EnsureChromeZOrder();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "OnResize: failed to reassert ribbon z-order");
        }

        // DockingHostFactory handles host bounds automatically
    }

    private void ThemeToggleFromRibbon(object? sender, EventArgs e)
    {
        _logger?.LogInformation("ThemeToggleFromRibbon: Theme toggle initiated from ribbon");
        try
        {
            // ToggleTheme() will broadcast through ThemeService
            // OnThemeChanged event handler will update all UI elements
            ToggleTheme();
            _logger?.LogInformation("ThemeToggleFromRibbon: Theme toggle completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme toggle from Ribbon failed");
        }
    }

    /// <summary>
    /// Initialize Syncfusion StatusBarAdv for status information.
    /// Delegates to StatusBarFactory for centralized creation and configuration.
    /// Wires panel references back to MainForm for status/progress management.
    /// </summary>
    private void InitializeStatusBar()
    {
        try
        {
            var statusBar = StatusBarFactory.CreateStatusBar(this, _logger, useSyncfusionDocking: _uiConfig.UseSyncfusionDocking);
            statusBar.Name = "ProfessionalStatusBar";
            _statusBar = statusBar;

            // Req 1: SfSkinManager is sole theme authority — propagate active theme to StatusBarAdv.
            var statusTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
            statusBar.ThemeName = statusTheme;
            SfSkinManager.SetVisualStyle(statusBar, statusTheme);

            statusBar.Dock = DockStyle.Bottom;
            statusBar.Margin = Padding.Empty;

            // Add status bar to form controls
            Controls.Add(statusBar);
            statusBar.BringToFront();

            _logger?.LogInformation("StatusBarFactory returned StatusBarAdv with {PanelCount} panels in Panels collection, and {ControlCount} in Controls collection",
                statusBar.Panels?.Length ?? 0, statusBar.Controls.Count);

            var panels = statusBar.Panels;
            if (panels == null || panels.Length < 5)
            {
                _logger?.LogWarning("StatusBarAdv.Panels was insufficient ({Count}); falling back to Controls collection", panels?.Length ?? 0);
                panels = statusBar.Controls.OfType<Syncfusion.Windows.Forms.Tools.StatusBarAdvPanel>().ToArray();
            }

            if (panels is { Length: >= 5 })
            {
                _statusLabel = panels[0];
                _statusTextPanel = panels[1];
                _statePanel = panels[2];
                _progressPanel = panels[3];
                _clockPanel = panels[4];

                _progressBar = _progressPanel?.Controls.OfType<Syncfusion.Windows.Forms.Tools.ProgressBarAdv>().FirstOrDefault();

                if (_progressBar != null && _statusLabel != null && _statusTextPanel != null && _statePanel != null && _progressPanel != null && _clockPanel != null)
                {
                    SetStatusBarPanels(statusBar,
                        _statusLabel,
                        _statusTextPanel,
                        _statePanel,
                        _progressPanel,
                        _progressBar,
                        _clockPanel);
                }
            }

            statusBar.TabStop = false;
            statusBar.TabIndex = 99;
            _logger?.LogDebug("Status bar initialized via StatusBarFactory");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            _logger?.LogError("InitializeStatusBar failed: {Message}", ex.Message);
            _statusBar = null;
            return;
        }
        _logger?.LogInformation("Status bar initialized via StatusBarFactory with {PanelCount} panels", _statusBar?.Panels.Length ?? 0);
        _logger?.LogDebug("Status bar size after init: {Width}x{Height}, HasSizingGrip={HasGrip}",
            _statusBar?.Width, _statusBar?.Height, _statusBar?.SizingGrip);
    }

    /// <summary>
    /// Initialize the navigation <see cref="ToolStripEx"/> with named <see cref="ToolStripButton"/>
    /// controls used for quick navigation.
    /// </summary>
    private void InitializeNavigationStrip()
    {
        // ... (Logic from UI.cs) ...
        if (_navigationStrip != null && !_navigationStrip.IsDisposed)
        {
            _logger?.LogDebug("NavigationStrip already exists — skipping re-init");
            return;
        }

        try
        {
            _navigationStrip = new ToolStripEx
            {
                Name = "NavigationStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                AccessibleName = "Navigation Strip",
                AccessibleDescription = "Main navigation toolbar for switching between application panels",
                AccessibleRole = AccessibleRole.ToolBar,
                AutoSize = true, // ToolStripEx handles height automatically
                TabIndex = 2,
                TabStop = true
            };

            try { SetAutomationId(_navigationStrip, "NavigationStrip", _logger); } catch { }

            // Ensure theme assembly is loaded before applying themes
            AppThemeColors.EnsureThemeAssemblyLoaded(_logger);
            try
            {
                _navigationStrip.ThemeName = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme; // Syncfusion theme integration
            }
            catch (Exception ex)
            {
                // Protect against Syncfusion control theme parsing issues (avoid crashing during ApplyControlTheme)
                _logger?.LogWarning(ex, "[MAINFORM] Failed to set NavigationStrip.ThemeName - falling back to default theme");
                try
                {
                    _navigationStrip.ThemeName = AppThemeColors.DefaultTheme;
                }
                catch { /* swallow - we cannot do more here */ }
            }

            // Helpers for button creation to save space?
            // Just pasting the logic from UI.cs

            var dashboardBtn = new ToolStripButton("Enterprise Vital Signs") { Name = "Nav_VitalSigns", AccessibleName = "Enterprise Vital Signs", Enabled = true };
            try { SetAutomationId(dashboardBtn, dashboardBtn.Name, _logger); } catch { }
            dashboardBtn.Click += (s, e) => this.ShowPanel<EnterpriseVitalSignsPanel>("Enterprise Vital Signs", DockingStyle.Fill, allowFloating: false);

            var accountsBtn = new ToolStripButton("Accounts") { Name = "Nav_Accounts", AccessibleName = "Accounts", Enabled = true };
            try { SetAutomationId(accountsBtn, accountsBtn.Name, _logger); } catch { }
            accountsBtn.Click += (s, e) => this.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);

            var budgetBtn = new ToolStripButton("Budget") { Name = "Nav_Budget", AccessibleName = "Budget", Enabled = true };
            try { SetAutomationId(budgetBtn, budgetBtn.Name, _logger); } catch { }
            budgetBtn.Click += (s, e) => this.ShowPanel<BudgetPanel>("Budget Management & Analysis", DockingStyle.Right, allowFloating: true);

            var chartsBtn = new ToolStripButton("Charts") { Name = "Nav_Charts", AccessibleName = "Charts", Enabled = true };
            try { SetAutomationId(chartsBtn, chartsBtn.Name, _logger); } catch { }
            chartsBtn.Click += (s, e) => this.ShowPanel<AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right, allowFloating: true);

            var analyticsBtn = new ToolStripButton("&Analytics") { Name = "Nav_Analytics", AccessibleName = "Analytics" };
            try { SetAutomationId(analyticsBtn, analyticsBtn.Name, _logger); } catch { }
            analyticsBtn.Click += (s, e) => this.ShowPanel<AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right, allowFloating: true);

            var auditLogBtn = new ToolStripButton("&Audit Log") { Name = "Nav_AuditLog", AccessibleName = "Audit Log" };
            try { SetAutomationId(auditLogBtn, auditLogBtn.Name, _logger); } catch { }
            auditLogBtn.Click += (s, e) => this.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true);

            var customersBtn = new ToolStripButton("Customers") { Name = "Nav_Customers", AccessibleName = "Nav_Customers" };
            try { SetAutomationId(customersBtn, customersBtn.Name, _logger); } catch { }
            customersBtn.Click += (s, e) => this.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);

            var quickBooksBtn = new ToolStripButton("QuickBooks") { Name = "Nav_QuickBooks", AccessibleName = "QuickBooks" };
            try { SetAutomationId(quickBooksBtn, quickBooksBtn.Name, _logger); } catch { }
            quickBooksBtn.Click += (s, e) => this.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);

            var aiChatBtn = new ToolStripButton("AI Chat") { Name = "Nav_AIChat", AccessibleName = "AI Chat" };
            try { SetAutomationId(aiChatBtn, aiChatBtn.Name, _logger); } catch { }
            aiChatBtn.Click += (s, e) => this.ShowPanel<JARVISChatUserControl>("JARVIS Chat", DockingStyle.Right, allowFloating: true);

            var proactiveInsightsBtn = new ToolStripButton("Proactive Insights") { Name = "Nav_ProactiveInsights", AccessibleName = "Proactive Insights" };
            try { SetAutomationId(proactiveInsightsBtn, proactiveInsightsBtn.Name, _logger); } catch { }
            proactiveInsightsBtn.Click += (s, e) => this.ShowPanel<ProactiveInsightsPanel>("Proactive AI Insights", DockingStyle.Right, allowFloating: true);

            var warRoomBtn = new ToolStripButton("War Room") { Name = "Nav_WarRoom", AccessibleName = "War Room" };
            try { SetAutomationId(warRoomBtn, warRoomBtn.Name, _logger); } catch { }
            warRoomBtn.Click += (s, e) => this.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true);

            var settingsBtn = new ToolStripButton("Settings") { Name = "Nav_Settings", AccessibleName = "Settings" };
            try { SetAutomationId(settingsBtn, settingsBtn.Name, _logger); } catch { }
            settingsBtn.Click += (s, e) => this.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);

            var themeToggleBtn = new ToolStripButton
            {
                Name = "ThemeToggle",
                AccessibleName = "Theme Toggle",
                AccessibleDescription = "Toggle between light and dark themes",
                AutoSize = true,
                ToolTipText = "Switch application theme (Ctrl+T)"
            };
            try { SetAutomationId(themeToggleBtn, themeToggleBtn.Name, _logger); } catch { }
            themeToggleBtn.Click += ThemeToggleBtn_Click;
            themeToggleBtn.Text = GetThemeButtonText(SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme);

            // Grid helpers (navigation strip)
            var navGridClearFilter = new ToolStripButton("Clear Grid Filter") { Name = "Nav_ClearGridFilter", AccessibleName = "Clear Grid Filter" };
            try { SetAutomationId(navGridClearFilter, navGridClearFilter.Name, _logger); } catch { }
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid") { Name = "Nav_ExportGrid", AccessibleName = "Export Grid" };
            try { SetAutomationId(navGridExport, navGridExport.Name, _logger); } catch { }
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn, new ToolStripSeparator(), accountsBtn, budgetBtn, chartsBtn, analyticsBtn, auditLogBtn, customersBtn, quickBooksBtn, aiChatBtn, proactiveInsightsBtn, warRoomBtn, new ToolStripSeparator(), settingsBtn, new ToolStripSeparator(), themeToggleBtn, new ToolStripSeparator(), navGridClearFilter, navGridExport
            });

            // Add the strip to the form so it appears in the UIA/control tree.
            // Without this, FlaUI cannot locate any navigation button (the strip
            // exists as a field but is invisible to UIA automation).
            this.Controls.Add(_navigationStrip);
            _navigationStrip.BringToFront();

        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize navigation strip");
            _navigationStrip = null;
        }
    }

    /// <summary>
    /// Initialize status timer to update clock panel.
    /// </summary>
    private void InitializeStatusTimer()
    {
        try
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _statusTimer.Tick += (s, e) =>
            {
                try { if (_clockPanel != null) _clockPanel.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture); } catch { }
            };
            _statusTimer.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to initialize status timer");
        }
    }

    /// <summary>
    /// Initialize MenuStrip for navigation and commands.
    /// Provides a traditional menu bar with File, View, Tools, and Help menus.
    /// Enhanced with Syncfusion theming, Segoe MDL2 Assets icons, and proper renderer configuration.
    /// </summary>
    private void InitializeMenuBar()
    {
        try
        {
            _menuStrip = new MenuStrip
            {
                Name = "MainMenuStrip",
                Dock = DockStyle.Top,
                Visible = true,
                Font = new Font("Segoe UI", 9F),
                RenderMode = ToolStripRenderMode.Professional,
                ShowItemToolTips = true,
                AccessibleName = "Main menu",
                AccessibleDescription = "Main navigation menu bar",
                AccessibleRole = AccessibleRole.MenuBar,
                TabIndex = 0,
                TabStop = true
            };

            // Apply professional color scheme with theme colors
            if (_menuStrip.Renderer is ToolStripProfessionalRenderer professionalRenderer)
            {
                professionalRenderer.RoundedEdges = true;
            }

            // ... (Menu construction logic from UI.cs) ...
            // Simplying slightly for snippet size, but logic assumes existence
            // I will just put a comment that it's the same logic, but for `write_file` I need to be explicit.

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File") { Name = "Menu_File", ToolTipText = "File operations", Image = CreateIconFromText("\uE8E5", 16) };
            _recentFilesMenu = new ToolStripMenuItem("&Recent Files") { Name = "Menu_File_RecentFiles" };
            UpdateMruMenu(_recentFilesMenu);

            var clearRecentMenuItem = new ToolStripMenuItem("&Clear Recent Files", null, (s, e) => ClearMruList()) { Name = "Menu_File_ClearRecent" };
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close()) { Name = "Menu_File_Exit", ShortcutKeys = Keys.Alt | Keys.F4 };

            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { _recentFilesMenu, clearRecentMenuItem, new ToolStripSeparator(), exitMenuItem });

            // View Menu
            var viewMenu = new ToolStripMenuItem("&View") { Name = "Menu_View", Image = CreateIconFromText("\uE8A7", 16) };
            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Enterprise Vital Signs", null, (s, e) => this.ShowPanel<EnterpriseVitalSignsPanel>("Enterprise Vital Signs", DockingStyle.Fill, allowFloating: false)) { Name = "Menu_View_VitalSigns", ShortcutKeys = Keys.Control | Keys.D };

            // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) => this.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true)) { Name = "Menu_View_Accounts", ShortcutKeys = Keys.Control | Keys.A };

            // View > Budget
            var budgetMenuItem = new ToolStripMenuItem("&Budget", null, (s, e) => this.ShowPanel<BudgetPanel>("Budget Management & Analysis", DockingStyle.Right, allowFloating: true)) { Name = "Menu_View_Budget", ShortcutKeys = Keys.Control | Keys.B };

            // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Analytics Hub", null, (s, e) => this.ShowPanel<AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right, allowFloating: true)) { Name = "Menu_View_AnalyticsHub", ShortcutKeys = Keys.Control | Keys.H };

            // View > QuickBooks
            var quickBooksMenuItem = new ToolStripMenuItem("&QuickBooks", null, (s, e) => this.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true)) { Name = "Menu_View_QuickBooks", ShortcutKeys = Keys.Control | Keys.Q };

            // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) => this.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true)) { Name = "Menu_View_Customers", ShortcutKeys = Keys.Control | Keys.U };

            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) => this.Refresh()) { Name = "Menu_View_Refresh", ShortcutKeys = Keys.F5 };

            viewMenu.DropDownItems.AddRange(new ToolStripItem[] { dashboardMenuItem, accountsMenuItem, budgetMenuItem, chartsMenuItem, quickBooksMenuItem, customersMenuItem, new ToolStripSeparator(), refreshMenuItem });

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Tools") { Name = "Menu_Tools", Image = CreateIconFromText("\uE90F", 16) };
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) => this.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true)) { Name = "Menu_Tools_Settings", ShortcutKeys = Keys.Control | Keys.Oemcomma };
            toolsMenu.DropDownItems.Add(settingsMenuItem);

            // Help Menu
            var helpMenu = new ToolStripMenuItem("&Help") { Name = "Menu_Help", Image = CreateIconFromText("\uE897", 16) };
            var documentationMenuItem = new ToolStripMenuItem("&Documentation", null, (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/WileyWidget/WileyWidget/wiki", UseShellExecute = true }); } catch { }
            })
            { Name = "Menu_Help_Documentation", ShortcutKeys = Keys.F1 };

            var aboutMenuItem = new ToolStripMenuItem("&About", null, (s, e) =>
            {
                MessageBox.Show($"{MainFormResources.FormTitle}\n\nVersion 1.0.0\nBuilt with .NET 9\n\n© {DateTime.Now.Year} Wiley Widget", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            })
            { Name = "Menu_Help_About" };

            helpMenu.DropDownItems.AddRange(new ToolStripItem[] { documentationMenuItem, new ToolStripSeparator(), aboutMenuItem });

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });

            ApplyMenuTheme(fileMenu);
            ApplyMenuTheme(viewMenu);
            ApplyMenuTheme(toolsMenu);
            ApplyMenuTheme(helpMenu);

            this.MainMenuStrip = _menuStrip;
            if (_menuStrip != null)
            {
                _menuStrip.ValidateAndConvertImages(_logger);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    private void UpdateMruMenu(ToolStripMenuItem menu)
    {
        if (menu == null || menu.IsDisposed)
        {
            return;
        }

        menu.DropDownItems.Clear();

        if (_mruList.Count == 0)
        {
            var emptyItem = new ToolStripMenuItem("(Empty)")
            {
                Name = "Menu_File_Recent_Empty",
                Enabled = false
            };
            menu.DropDownItems.Add(emptyItem);
            return;
        }

        for (var index = 0; index < _mruList.Count; index++)
        {
            var filePath = _mruList[index];
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            var displayName = Path.GetFileName(filePath);
            var menuItem = new ToolStripMenuItem($"&{index + 1} {displayName}")
            {
                Name = $"Menu_File_Recent_{index + 1}",
                ToolTipText = filePath,
                Tag = filePath
            };

            menuItem.Click += async (_, _) => await OpenRecentFileAsync(filePath).ConfigureAwait(true);
            menu.DropDownItems.Add(menuItem);
        }
    }

    private void RefreshMruMenu()
    {
        if (_recentFilesMenu == null || _recentFilesMenu.IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((System.Action)RefreshMruMenu);
            return;
        }

        UpdateMruMenu(_recentFilesMenu);
    }

    private async Task OpenRecentFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            _mruList.Remove(filePath);
            _windowStateService.SaveMru(_mruList);
            RefreshMruMenu();
            ShowErrorDialog("File Not Found", $"The file '{Path.GetFileName(filePath)}' no longer exists and was removed from recent files.");
            return;
        }

        try
        {
            ApplyStatus($"Opening recent file: {Path.GetFileName(filePath)}");
            await ProcessDroppedFiles(new[] { filePath }, CancellationToken.None).ConfigureAwait(true);
            RefreshMruMenu();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open recent file {FilePath}", filePath);
            ShowErrorDialog("Recent File", $"Failed to open '{Path.GetFileName(filePath)}'.", ex);
        }
    }

    private void ClearMruList()
    {
        try
        {
            _windowStateService.ClearMru();
            _mruList.Clear();
            RefreshMruMenu();
            ApplyStatus("Recent files cleared.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear MRU list");
            ShowErrorDialog("Clear Recent Files", "Unable to clear recent files.", ex);
        }
    }

    private Bitmap? CreateIconFromText(string iconText, int size)
    {
        if (string.IsNullOrWhiteSpace(iconText) || size <= 0) return null;
        try
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                using (var font = new Font("Segoe MDL2 Assets", size * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    // Theme-aware icon color: use the form ForeColor which SfSkinManager sets per theme.
                    // Fallback to a high-contrast system color if ForeColor is not set.
                    var iconColor = this.ForeColor.IsEmpty ? SystemColors.Highlight : this.ForeColor;
                    using (var brush = new SolidBrush(iconColor))
                    {
                        graphics.DrawString(iconText, font, brush, new RectangleF(0, 0, size, size), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                }
            }
            return bitmap;
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "CreateIconFromText failed"); return null; }
    }

    // Removed unused helpers: GetLoadingOverlayColor / GetLoadingLabelColor
    // These helpers had no call sites in the codebase. Keep commented for possible future use.
    // private Color GetLoadingOverlayColor() => SystemColors.Control;
    // private Color GetLoadingLabelColor() => Color.White;

    private string GetCurrentTheme() => SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

    private void ApplyMenuTheme(ToolStripMenuItem menuItem)
    {
        if (menuItem?.DropDown == null) return;
        try
        {
            var dropdown = (ToolStripDropDownMenu)menuItem.DropDown;
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(dropdown, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme); } catch { }
            foreach (ToolStripItem item in dropdown.Items) if (item is ToolStripMenuItem child) ApplyMenuTheme(child);
        }
        catch { }
    }

    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
        // Route navigation strip theme toggle through ribbon toggle handler
        ThemeToggleFromRibbon(sender, e);
    }


    /// <summary>
    /// Toggle the application theme between light and dark modes.
    /// Broadcasts the change through ThemeService, which notifies all subscribers via OnThemeChanged event.
    /// The OnThemeChanged event (in MainForm.Docking.cs) applies theme to all controls and updates toggle button text.
    ///
    /// POLISH ENHANCEMENTS:
    /// - Emoji icon support with fallback to text-only for systems that don't render emojis correctly.
    /// - Support for multiple Office2019 themes without selecting unsupported variants.
    /// </summary>
    public void ToggleTheme()
    {
        try
        {
            var currentTheme = _themeService?.CurrentTheme ?? SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            // Validate current theme before toggling
            currentTheme = AppThemeColors.ValidateTheme(currentTheme, _logger);

            // POLISH: Support multiple themes with better cycling
            var nextTheme = GetNextTheme(currentTheme);

            _logger?.LogInformation("Theme toggle initiated from {CurrentTheme} to {NextTheme}", currentTheme, nextTheme);

            // Best-effort: update local theme toggle immediately so callers observing synchronously see the change
            try
            {
                var buttonText = GetThemeButtonText(nextTheme);
                ToolStripButton? immediateToggle = null;
                try { if (_ribbon != null) immediateToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton; } catch { }
                if (immediateToggle == null)
                {
                    try { immediateToggle = FindToolStripItem(this, "ThemeToggle") as ToolStripButton; } catch { }
                }
                if (immediateToggle != null)
                {
                    immediateToggle.Text = buttonText;
                }
            }
            catch { }

            // Ensure every ThemeToggle button in the control tree is updated immediately (defensive)
            try
            {
                var newText = GetThemeButtonText(nextTheme);
                void UpdateItems(ToolStripItemCollection items)
                {
                    foreach (ToolStripItem it in items)
                    {
                        try
                        {
                            if (it is ToolStripButton tb && string.Equals(tb.Name, "ThemeToggle", StringComparison.OrdinalIgnoreCase))
                            {
                                tb.Text = newText;
                            }
                            if (it is ToolStripPanelItem panel)
                            {
                                UpdateItems(panel.Items);
                            }
                            if (it is ToolStripDropDownItem dd)
                            {
                                UpdateItems(dd.DropDownItems);
                            }
                        }
                        catch { }
                    }
                }

                foreach (Control c in Controls)
                {
                    try
                    {
                        if (c is ToolStrip ts)
                        {
                            UpdateItems(ts.Items);
                        }
                        foreach (ToolStrip childTs in c.Controls.OfType<ToolStrip>())
                        {
                            UpdateItems(childTs.Items);
                        }
                    }
                    catch { }
                }

                if (_ribbon != null)
                {
                    foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
                    {
                        if (tab.Panel == null) continue;
                        foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                        {
                            try { UpdateItems(panel.Items); } catch { }
                        }
                    }
                }
            }
            catch { }

            // Apply theme via service after immediate UI update so tests observing synchronous state pass.
            if (_themeService != null)
            {
                _themeService.ApplyTheme(nextTheme);
                _logger?.LogDebug("Theme applied via ThemeService - OnThemeChanged event will broadcast to all subscribers");
            }
            else
            {
                // Fallback: Apply directly to SfSkinManager if ThemeService is not available
                try
                {
                    SfSkinManager.ApplicationVisualTheme = nextTheme;
                    SfSkinManager.SetVisualStyle(this, nextTheme);
                    _logger?.LogWarning("Theme applied via SfSkinManager fallback - ThemeService not available");
                }
                catch (ArgumentException argEx)
                {
                    _logger?.LogError(argEx, "Invalid theme name '{NextTheme}' rejected by SfSkinManager - falling back to default", nextTheme);
                    try
                    {
                        SfSkinManager.ApplicationVisualTheme = AppThemeColors.DefaultTheme;
                        SfSkinManager.SetVisualStyle(this, AppThemeColors.DefaultTheme);
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger?.LogError(fallbackEx, "Failed to apply fallback theme");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme toggle failed");
        }
    }

    /// <summary>
    /// Req 1: Handles runtime theme changes broadcast by IThemeService.
    /// Cascades the new theme to ALL Syncfusion controls on the form via SfSkinManager,
    /// then explicitly refreshes ThemeName on ribbon and status bar for robustness.
    /// </summary>
    private void OnThemeServiceChanged(object? sender, string newTheme)
    {
        try
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;

            this.InvokeIfRequired(() =>
            {
                try
                {
                    AppThemeColors.EnsureThemeAssemblyLoadedForTheme(newTheme);

                    // Cascade to all child Syncfusion controls (DockingManager, StatusBarAdv, etc.)
                    SfSkinManager.SetVisualStyle(this, newTheme);

                    // Explicit ThemeName refresh on controls that persist their own ThemeName property
                    if (_ribbon != null && !_ribbon.IsDisposed)
                    {
                        _ribbon.ThemeName = newTheme;
                        SfSkinManager.SetVisualStyle(_ribbon, newTheme);
                    }

                    if (_statusBar != null && !_statusBar.IsDisposed)
                    {
                        _statusBar.ThemeName = newTheme;
                        SfSkinManager.SetVisualStyle(_statusBar, newTheme);
                    }

                    RefreshThemeSensitiveControls(newTheme);

                    // Update the theme toggle button text to reflect the new active theme
                    try
                    {
                        var newText = GetThemeButtonText(newTheme);
                        if (_ribbon != null)
                        {
                            if (FindToolStripItem(_ribbon, "ThemeToggle") is ToolStripButton toggle)
                                toggle.Text = newText;
                        }
                    }
                    catch { /* theme button text is cosmetic; do not fail hard */ }

                    _logger?.LogDebug("OnThemeServiceChanged: theme cascaded to all MainForm controls → {Theme}", newTheme);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "OnThemeServiceChanged: failed to apply theme {Theme}", newTheme);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "OnThemeServiceChanged: outer guard failed");
        }
    }

    private void RefreshThemeSensitiveControls(string themeName)
    {
        ApplyThemeToControlTree(this, themeName);

        foreach (var mdiChild in MdiChildren)
        {
            if (mdiChild is { IsDisposed: false })
            {
                SfSkinManager.SetVisualStyle(mdiChild, themeName);
                ApplyThemeToControlTree(mdiChild, themeName);
            }
        }

        foreach (var ownedForm in OwnedForms)
        {
            if (ownedForm is { IsDisposed: false })
            {
                SfSkinManager.SetVisualStyle(ownedForm, themeName);
                ApplyThemeToControlTree(ownedForm, themeName);
                TryForceLayoutOnScopedPanelChildren(ownedForm);
            }
        }

        RefreshDockingThemeState(themeName);

        _ribbon?.Invalidate(true);
        _ribbon?.Update();

        TryForceLayoutOnScopedPanelChildren(this);

        Invalidate(true);
        Update();
    }

    private void RefreshDockingThemeState(string themeName)
    {
        try
        {
            if (_dockingManager?.HostControl is not Control hostControl || hostControl.IsDisposed)
            {
                return;
            }

            SfSkinManager.SetVisualStyle(hostControl, themeName);
            PerformLayoutRecursive(hostControl);
            TryForceLayoutOnScopedPanelChildren(hostControl);
            hostControl.Invalidate(true);
            hostControl.Update();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "RefreshDockingThemeState failed for {Theme}", themeName);
        }
    }

    private static void ApplyThemeToControlTree(Control root, string themeName)
    {
        if (root.IsDisposed)
        {
            return;
        }

        if (root is IThemable themable)
        {
            themable.ApplyTheme(themeName);
        }

        if (root is SfDataGrid dataGrid)
        {
            dataGrid.Invalidate(true);
            dataGrid.Update();
        }
        else if (root is ChartControl chartControl)
        {
            chartControl.Invalidate(true);
            chartControl.Update();
        }
        else if (root is RibbonControlAdv ribbonControl)
        {
            ribbonControl.Invalidate(true);
            ribbonControl.Update();
        }
        else if (root is UserControl or Panel)
        {
            root.Invalidate(true);
            root.Update();
        }

        foreach (Control child in root.Controls)
        {
            ApplyThemeToControlTree(child, themeName);
        }
    }

    /// <summary>
    /// POLISH: Gets the next theme in the cycle.
    /// Cycles through supported Syncfusion Office2019 themes to avoid invalid selections.
    /// </summary>
    private static string GetNextTheme(string currentTheme)
    {
        var index = Array.FindIndex(ThemeCycle, theme => theme.Equals(currentTheme, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return ThemeCycle[0];
        }

        return ThemeCycle[(index + 1) % ThemeCycle.Length];
    }

    /// <summary>
    /// POLISH: Gets the button text for a theme, with emoji and text fallback.
    /// Tests for emoji rendering; falls back to text-only if emojis don't render properly.
    /// </summary>
    private static string GetThemeButtonText(string themeName)
    {
        var useEmoji = SupportsEmojiRendering();

        return themeName switch
        {
            _ when string.Equals(themeName, "Office2019Dark", StringComparison.OrdinalIgnoreCase) =>
                useEmoji ? "☀️ Light" : "Light Mode",
            _ when string.Equals(themeName, "Office2019Colorful", StringComparison.OrdinalIgnoreCase) =>
                useEmoji ? "🌙 Dark" : "Dark Mode",
            _ when string.Equals(themeName, "Office2019Black", StringComparison.OrdinalIgnoreCase) =>
                useEmoji ? "⬜ White" : "White Mode",
            _ when string.Equals(themeName, "Office2019White", StringComparison.OrdinalIgnoreCase) =>
                useEmoji ? "🌗 Gray" : "Gray Mode",
            _ when string.Equals(themeName, "Office2019DarkGray", StringComparison.OrdinalIgnoreCase) =>
                useEmoji ? "🎨 Color" : "Colorful Mode",
            _ => useEmoji ? "⚙️ Theme" : "Theme"
        };
    }

    /// <summary>
    /// POLISH: Detects if the current environment supports emoji rendering.
    /// Returns true if Windows 10+ or if emoji rendering is explicitly enabled.
    /// </summary>
    private static bool SupportsEmojiRendering()
    {
        try
        {
            // Windows 10+ generally supports emoji; earlier versions may have rendering issues
            var osVersion = Environment.OSVersion;
            return osVersion.Version.Major >= 10;
        }
        catch
        {
            return false;  // Err on the side of caution
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ToolStripTextBox searchBox) return;
        if (e.KeyCode == Keys.Enter)
        {
            var searchText = searchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText)) { _ = PerformGlobalSearchAsync(searchText); }
            e.Handled = true;
        }
    }

    private ToolStripTextBox? GetGlobalSearchTextBox()
    {
        if (_globalSearchTextBox != null && !_globalSearchTextBox.IsDisposed)
        {
            return _globalSearchTextBox;
        }

        ToolStripTextBox? resolved = null;
        try
        {
            if (_ribbon != null)
            {
                resolved = FindToolStripItem(_ribbon, "GlobalSearch") as ToolStripTextBox;
            }
        }
        catch { }

        if (resolved == null)
        {
            try
            {
                resolved = FindToolStripItem(this, "GlobalSearch") as ToolStripTextBox;
            }
            catch { }
        }

        if (resolved != null && !resolved.IsDisposed)
        {
            _globalSearchTextBox = resolved;
            if (string.IsNullOrWhiteSpace(resolved.AccessibleName))
            {
                resolved.AccessibleName = "Global search";
            }

            if (string.IsNullOrWhiteSpace(resolved.AccessibleDescription))
            {
                resolved.AccessibleDescription = "Enter a search term and press Enter to search across modules";
            }
        }

        return _globalSearchTextBox;
    }

    private void CacheGlobalSearchTextBox()
    {
        _ = GetGlobalSearchTextBox();
    }

    private void EnsureRibbonAccessibility()
    {
        if (_ribbon == null)
        {
            return;
        }

        try
        {
            _ribbon.AccessibleName ??= "Main ribbon";
            _ribbon.AccessibleDescription ??= "Primary application commands and search";

            foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
            {
                if (string.IsNullOrWhiteSpace(tab.AccessibleName))
                {
                    tab.AccessibleName = string.IsNullOrWhiteSpace(tab.Text) ? tab.Name : tab.Text;
                }

                if (tab.Panel == null)
                {
                    continue;
                }

                foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                {
                    ApplyToolStripAccessibility(panel.Items, tab.Text ?? tab.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to apply ribbon accessibility metadata");
        }
    }

    private void ApplyToolStripAccessibility(ToolStripItemCollection items, string? groupName)
    {
        foreach (ToolStripItem item in items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.AccessibleName))
                {
                    item.AccessibleName = !string.IsNullOrWhiteSpace(item.Text)
                        ? item.Text
                        : (!string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.GetType().Name);
                }

                if (string.IsNullOrWhiteSpace(item.AccessibleDescription))
                {
                    item.AccessibleDescription = !string.IsNullOrWhiteSpace(groupName)
                        ? $"{item.AccessibleName} in {groupName}"
                        : item.AccessibleName;
                }

                if (item is ToolStripTextBox textBox && string.Equals(textBox.Name, "GlobalSearch", StringComparison.OrdinalIgnoreCase))
                {
                    textBox.AccessibleName ??= "Global search";
                    textBox.AccessibleDescription ??= "Enter a search term and press Enter to search across modules";
                    _globalSearchTextBox ??= textBox;
                }

                if (item is ToolStripPanelItem panelItem)
                {
                    ApplyToolStripAccessibility(panelItem.Items, groupName);
                }
                else if (item is ToolStripDropDownItem dropDown)
                {
                    ApplyToolStripAccessibility(dropDown.DropDownItems, groupName);
                }
            }
            catch { }
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // Handle global keyboard shortcuts
        if (e.Control && e.KeyCode == Keys.T)
        {
            // Ctrl+T: Toggle theme
            e.Handled = true;
            ThemeToggleFromRibbon(sender, e);
        }
    }

    private ToolStripItem? FindToolStripItem(Control container, string name)
    {
        if (container == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (container is ToolStrip strip)
        {
            return FindToolStripItemInCollection(strip.Items, name);
        }

        if (container is RibbonControlAdv ribbon)
        {
            foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
            {
                if (tab?.Panel == null)
                {
                    continue;
                }

                foreach (Control child in tab.Panel.Controls)
                {
                    var result = FindToolStripItem(child, name);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
        }

        foreach (Control child in container.Controls)
        {
            var result = FindToolStripItem(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static ToolStripItem? FindToolStripItemInCollection(ToolStripItemCollection items, string name)
    {
        foreach (ToolStripItem item in items)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            if (item is ToolStripPanelItem panelItem)
            {
                var panelResult = FindToolStripItemInCollection(panelItem.Items, name);
                if (panelResult != null)
                {
                    return panelResult;
                }
            }

            if (item is ToolStripDropDownItem dropDownItem)
            {
                var dropDownResult = FindToolStripItemInCollection(dropDownItem.DropDownItems, name);
                if (dropDownResult != null)
                {
                    return dropDownResult;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// ✅ NEW: Diagnostic method to verify ribbon state and configuration (for troubleshooting).
    /// Call this from InitializeRibbon() after ribbon creation to identify any state issues.
    /// </summary>
    private void DiagnoseRibbon()
    {
        if (_ribbon == null)
        {
            _logger?.LogError("[RIBBON_DIAGNOSTICS] Ribbon is NULL");
            return;
        }

        try
        {
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] ===== RIBBON STATE REPORT =====");
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] IsDisposed: {Disposed}", _ribbon.IsDisposed);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] IsDisposing: {Disposing}", _ribbon.Disposing);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] IsHandleCreated: {HasHandle}", _ribbon.IsHandleCreated);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] Size: {Width}x{Height}", _ribbon.Width, _ribbon.Height);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] Dock: {Dock}", _ribbon.Dock);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] Visible: {Visible}", _ribbon.Visible);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] OfficeColorScheme: {Scheme}", _ribbon.OfficeColorScheme);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] ThemeName: {Theme}", _ribbon.ThemeName);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] RibbonStyle: {Style}", _ribbon.RibbonStyle);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] LauncherStyle: {Launcher}", _ribbon.LauncherStyle);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] Tabs Count: {TabCount}", _ribbon.Header?.MainItems.Count ?? 0);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] Parent: {Parent}", _ribbon.Parent?.GetType().Name ?? "null");
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] In Controls Collection: {InCollection}", this.Controls.Contains(_ribbon));
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] TabIndex: {TabIndex}", _ribbon.TabIndex);
            _logger?.LogInformation("[RIBBON_DIAGNOSTICS] ===== END RIBBON STATE =====");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RIBBON_DIAGNOSTICS] Exception during diagnostics");
        }
    }

    /// <summary>
    /// Determines if FlaUI automation is currently active by checking if FlaUI assemblies are loaded.
    /// Used to conditionally attach ribbon controls for UI automation testing.
    /// </summary>
    private bool IsFlaUiAutomationTest()
    {
        try
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.Contains("FlaUI"));
        }
        catch
        {
            return false;
        }
    }

}
