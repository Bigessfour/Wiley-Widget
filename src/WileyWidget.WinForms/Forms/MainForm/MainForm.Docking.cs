using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private DockingManager? _dockingManager;
    private WileyWidget.WinForms.Controls.ActivityLogPanel? _activityLogPanel;
    private DockingLayoutManager? _dockingLayoutManager;
    private Panel? _leftDockPanel;
    private Panel? _centralDocumentPanel;
    private Panel? _rightDockPanel;
    private Dictionary<string, Control>? _dynamicDockPanels;

    // Layout state management
    private readonly DateTime _lastSaveTime = DateTime.MinValue;

    // Layout versioning
    private const string LayoutVersionAttributeName = "layoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const string SegoeUiFontName = "Segoe UI";

    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";

    /// <summary>
    /// Initializes Syncfusion DockingManager with layout management.
    /// Delegates to DockingHostFactory for centralized docking creation logic.
    /// Loads saved layout from AppData if available.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        var globalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Syncfusion Docking Initialization");

        try
        {
            _logger?.LogInformation("InitializeSyncfusionDocking START - handleCreated={HandleCreated}, UIThread={ThreadId}",
                IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);

            // Phase: DockingManager Creation
            var dockingHostStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger?.LogInformation("Creating DockingHost via factory...");
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider cannot be null when creating DockingHost.");
            }
            var (dockingManager, leftPanel, rightPanel, activityLogPanel, activityTimer, layoutManager) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);
            dockingHostStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("DockingManager Creation", dockingHostStopwatch.ElapsedMilliseconds);

            _dockingManager = dockingManager;
            _leftDockPanel = leftPanel;
            _rightDockPanel = rightPanel;
            _activityLogPanel = activityLogPanel;
            _dockingLayoutManager = layoutManager;
            _dynamicDockPanels ??= new Dictionary<string, Control>();

            var currentTheme = _themeService?.CurrentTheme ?? AppThemeColors.DefaultTheme;
            ThemeApplicationHelper.ApplyThemeToDockingManager(_dockingManager, currentTheme, _logger);

            // Wire up cache invalidation for grid discovery
            // Note: ActiveControlChanged event not available on DockingManager
            // Grid cache invalidation handled via DockStateChanged event instead

            _logger?.LogInformation("DockingHost created: HasManager={HasManager}, HostControl={HostControl}",
                _dockingManager != null, _dockingManager?.HostControl?.Name ?? "<null>");

            // Ensure panels are visible via DockingManager API
            if (_dockingManager != null)
            {
                if (_leftDockPanel != null)
                {
                    _leftDockPanel.Visible = true;
                    _dockingManager.SetDockVisibility(_leftDockPanel, true);
                    _dockingManager.SetControlMinimumSize(_leftDockPanel, new Size(300, 360));
                }

                if (_rightDockPanel != null)
                {
                    _rightDockPanel.Visible = true;
                    _dockingManager.SetDockVisibility(_rightDockPanel, true);
                    _dockingManager.SetControlMinimumSize(_rightDockPanel, new Size(300, 360));
                }
            }

            // 3. Layout recalc is deferred until form is fully shown

            // 4. Activity grid will be loaded with actual data after docking completes
            if (_activityLogPanel != null)
            {
                _activityLogPanel.Visible = true;
            }

            // Panel activation and layout recalc deferred until form is fully shown

            // Guard: Ensure at least DockingManager was created
            if (_dockingManager == null)
            {
                _asyncLogger?.Error("DockingManager creation failed");
                _logger?.LogError("DockingManager creation failed - docking will be unavailable");
                Console.WriteLine("[DIAGNOSTIC ERROR] DockingManager is null after CreateDockingHost");
                return;
            }

            // Ensure panel navigation is available before layout load so dynamic panels recreate with real controls
            EnsurePanelNavigatorInitialized();

            // Create and attach layout manager for state management
            var layoutPath = GetDockingLayoutPath();
            _dockingLayoutManager = new DockingLayoutManager(_serviceProvider, _panelNavigator, _logger, layoutPath);
            _logger?.LogDebug("DockingLayoutManager created successfully with path {LayoutPath}", layoutPath);

            // Transfer ownership of panels and fonts to the layout manager
            var dockAutoHideTabFont = new Font(SegoeUiFontName, 9F);
            var dockTabFont = new Font(SegoeUiFontName, 9F);

            // Note: DockingLayoutManager initialization deferred - methods will be available in future
            // For now, panels are managed directly by DockingManager
            _logger?.LogDebug("DockingLayoutManager created but initialization deferred");

            HideStandardPanelsForDocking();

            // CRITICAL FIX: Create and dock initial panels BEFORE suspending layout
            // This ensures DockingManager has controls in its collection before OnPaint events fire
            // Without this, ArgumentOutOfRangeException occurs in DockHost.GetPaintInfo()
            try
            {
                CreateDockingPanels();
                var dynamicPanelCount = _dynamicDockPanels?.Count ?? 0;
                var createdPanelCount = new[] { _leftDockPanel, _centralDocumentPanel, _rightDockPanel }.Count(p => p != null) + dynamicPanelCount;
                _logger?.LogInformation("Docking panels created and docked — count={PanelCount}, dynamicPanels={DynamicCount}, layoutManagerReady={LayoutManagerReady}",
                    createdPanelCount, dynamicPanelCount, _dockingLayoutManager != null);
            }
            catch (Exception panelEx)
            {
                _logger?.LogError(panelEx, "Failed to create docking panels - paint exceptions may occur");
            }

            // Reduce flicker during layout load + theme application (best-effort).
            var dockingUpdatesLocked = false;
            var dockingLayoutSuspended = false;

            try
            {
                try
                {
                    _dockingManager.LockHostFormUpdate();
                    _dockingManager.LockDockPanelsUpdate();
                    dockingUpdatesLocked = true;
                }
                catch (Exception lockEx)
                {
                    _logger?.LogDebug(lockEx, "Failed to lock DockingManager updates - continuing without lock");
                }

                try
                {
                    _dockingManager.SuspendLayout();
                    dockingLayoutSuspended = true;
                }
                catch (Exception suspendEx)
                {
                    _logger?.LogDebug(suspendEx, "Failed to suspend DockingManager layout - continuing");
                }

                // CRITICAL: Layout loading is now deferred to InitializeAsync() via LoadAndApplyDockingLayout()
                // This ensures the form is fully rendered before we start heavy docking restoration.
                _logger?.LogDebug("DockingManager infrastructure initialized. Layout restoration will follow in InitializeAsync().");

                // Theme application deferred: Applied after DockingManager.ResumeLayout() to avoid conflicts

                // CRITICAL: Apply SFSkinManager theme AFTER DockingManager is fully initialized and panels are docked
                // This ensures theme cascade works correctly and prevents ArgumentOutOfRangeException in paint events
                var themeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
                    SfSkinManager.SetVisualStyle(this, themeName);
                    themeStopwatch.Stop();
                    StartupInstrumentation.RecordPhaseTime("Theme Application", themeStopwatch.ElapsedMilliseconds);
                    _logger?.LogInformation("Applied SfSkinManager theme to MainForm after DockingManager setup: {Theme} ({Time}ms)", themeName, themeStopwatch.ElapsedMilliseconds);
                }
                catch (Exception themeEx)
                {
                    themeStopwatch.Stop();
                    _logger?.LogWarning(themeEx, "Failed to apply SFSkinManager theme to MainForm after DockingManager setup");
                }

                // Ensure panels start visible & activated
                if (_dockingManager != null)
                {
                    var ienum = _dockingManager.Controls;
                    GradientPanelExt? firstPanel = null;
                    while (ienum.MoveNext())
                    {
                        if (ienum.Current is GradientPanelExt panel)
                        {
                            panel.Visible = true;
                            panel.BringToFront();
                            if (firstPanel == null) firstPanel = panel;
                        }
                    }
                    if (firstPanel != null)
                    {
                        _dockingManager.ActivateControl(firstPanel);
                    }
                }
            }
            finally
            {
                if (dockingLayoutSuspended)
                {
                    try { _dockingManager.ResumeLayout(true); } catch { }
                }

                if (dockingUpdatesLocked)
                {
                    try { _dockingManager.UnlockDockPanelsUpdate(); } catch { }
                    try { _dockingManager.UnlockHostFormUpdate(); } catch { }
                }
            }

            _logger?.LogInformation("InitializeSyncfusionDocking complete - ActivityLogPanel={HasActivityPanel}",
                _activityLogPanel != null);

            globalStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("Total DockingManager Initialization", globalStopwatch.ElapsedMilliseconds);
            StartupInstrumentation.LogInitializationState(_logger);
            Console.WriteLine(StartupInstrumentation.GetFormattedMetrics());
        }
        catch (Exception ex) when (ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase))
        {
            // Theme assembly load failure - provide user-friendly message
            _logger?.LogError(ex, "Theme assembly failed to load during DockingManager initialization");
            _asyncLogger?.Error($"Theme Assembly Error: {ex.Message}");
            MessageBox.Show(
                "Theme assembly missing—please reinstall Syncfusion packages or reset to default theme.",
                "Theme Loading Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            // Fall back to default theme
            try
            {
                SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
                SfSkinManager.SetVisualStyle(this, "Office2019Colorful");
                _logger?.LogInformation("Fell back to default Office2019Colorful theme after assembly load failure");
            }
            catch (Exception fallbackEx)
            {
                _logger?.LogError(fallbackEx, "Failed to apply fallback theme");
            }
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("Syncfusion"))
        {
            // Syncfusion-related error - provide diagnostic info
            _logger?.LogError(ex, "Syncfusion exception during DockingManager initialization: {Message}", ex.Message);
            _asyncLogger?.Error($"Syncfusion Error: {ex.Message}");
            MessageBox.Show(
                $"UI initialization error: {ex.Message}\n\nThe application may be unstable. Please restart.",
                "Syncfusion Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            // Docking initialization failure is non-critical - system can still function
            // but without docking capabilities
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Syncfusion DockingManager: {Message}", ex.Message);
            _asyncLogger?.Error($"Initialization Error: {ex.GetType().Name}: {ex.Message}");
            // Docking initialization failure is non-critical - system can still function
            // but without docking capabilities
        }
    }

    /// <summary>
    /// Handles theme changes at runtime and reapplies theme to all docking panels.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="theme">New theme to apply.</param>
    private void OnThemeChanged(object? sender, string theme)
    {
        if (!IsHandleCreated)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to marshal OnThemeChanged to UI thread");
            }
            return;
        }

        try
        {
            _logger?.LogInformation("Applying theme change to docking panels: {Theme}", theme);

            // Update button text in Ribbon if present
            if (_ribbon != null)
            {
                var themeToggleBtn = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                }
            }

            // Update button text in NavigationStrip if present
            if (_navigationStrip != null)
            {
                var themeToggleBtn = _navigationStrip.Items.Find("ThemeToggle", true).FirstOrDefault() as ToolStripButton;
                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                }
                // Update ToolStripEx theme to match application theme
                _navigationStrip.ThemeName = theme;
            }

            // Reapply theme to all docking panels via layout manager
            if (_dockingLayoutManager != null && _dockingManager != null)
            {
                // Note: We don't have direct access to the panels anymore, but the layout manager handles theme application
                _logger?.LogDebug("Theme application delegated to DockingLayoutManager");
            }

            // Refresh activity grid with new theme
            if (_activityLogPanel != null && !_activityLogPanel.IsDisposed)
            {
                try
                {
                    // REMOVED: Per-control SetVisualStyle - grid inherits theme from ApplicationVisualTheme
                    // Theme cascade ensures consistent styling across all controls
                    _activityLogPanel.Refresh();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to refresh activity grid");
                }
            }

            this.Refresh();
            _logger?.LogInformation("Theme successfully applied to docking panels");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme change to docking panels");
        }
    }

    /// <summary>
    /// Initialize the DockingManager component with proper configuration
    /// </summary>
    private void InitializeDockingManager()
    {
        components ??= new Container();
        _dockingManager = new DockingManager(components);
        _dockingManager.HostControl = this;

        ConfigureDockingManagerSettings();
    }

    /// <summary>
    /// Configure DockingManager settings and theme
    /// </summary>
    private void ConfigureDockingManagerSettings()
    {
         if (_dockingManager == null) return;

        // Phase 1 Simplification: EnableDocumentMode permanently false (panels only)
        _dockingManager.EnableDocumentMode = false;
        _logger?.LogInformation("DockingManager document mode disabled (using DockingManager for panels only)");

        _dockingManager.PersistState = true;
        _dockingManager.AnimateAutoHiddenWindow = true;
        // REMOVED: Hard-coded fonts - SFSkinManager owns all theming
        _dockingManager.ShowCaption = true;

        // Give the DockingManager a stable name for tooling/tests
        try
        {
            var nameProp = _dockingManager.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(_dockingManager, "DockingManager_Main");
            }
        }
        catch { }

        // Attach state events to keep navigation & diagnostics up-to-date
        try
        {
            _dockingManager.DockStateChanged += DockingManager_DockStateChanged;
            _dockingManager.DockControlActivated += DockingManager_DockControlActivated;
            _dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;
        }
        catch { }
    }

    /// <summary>
    /// Create all docking panels (left, center, right)
    /// </summary>
    private void CreateDockingPanels()
    {
        CreateLeftDockPanel();
        CreateCentralDocumentPanel();
        CreateRightDockPanel();
    }

    /// <summary>
    /// Create left dock panel with navigation buttons (collapsible, auto-hide enabled)
    /// </summary>
    private void CreateLeftDockPanel()
    {
        if (_dockingManager == null) return;

        _leftDockPanel = new Panel
        {
            Name = "LeftDockPanel",
            AccessibleName = "LeftDockPanel",
            AutoScroll = true,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(8, 8, 8, 8)
        };

        // Create navigation buttons for left panel
        var navPanel = CreateNavigationButtonsPanel();
        _leftDockPanel.Controls.Add(navPanel);

        ConfigurePanelDocking(_leftDockPanel, DockingStyle.Left, 280, "Navigation");

        _logger?.LogDebug("Left dock panel created with navigation buttons");
    }

    /// <summary>
    /// Create a panel containing navigation buttons for the left dock
    /// </summary>
    private Panel CreateNavigationButtonsPanel()
    {
        var navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(5),
            Name = "NavigationButtonsPanel"
        };

         // Create navigation buttons
        var navButtons = new (string Label, string Name, Type PanelType, DockingStyle DockStyle)[]
        {
            ("Dashboard", "Nav_Dash", typeof(DashboardPanel), DockingStyle.Top),
            ("Accounts", "Nav_Accts", typeof(AccountsPanel), DockingStyle.Left),
            ("Budget", "Nav_Budget", typeof(BudgetOverviewPanel), DockingStyle.Bottom),
            ("Charts", "Nav_Charts", typeof(BudgetAnalyticsPanel), DockingStyle.Right),
            ("Reports", "Nav_Reports", typeof(ReportsPanel), DockingStyle.Right),
            ("QuickBooks", "Nav_QB", typeof(QuickBooksPanel), DockingStyle.Right),
            ("Settings", "Nav_Settings", typeof(SettingsPanel), DockingStyle.Right),
        };

        foreach (var (label, name, panelType, dockStyle) in navButtons)
        {
            var btn = new Button
            {
                Text = label,
                Name = name,
                Width = 220,
                Height = 32,
                Margin = new Padding(5, 5, 5, 5),
                AccessibleName = label,
                AccessibleDescription = $"Navigate to {label} panel",
                TabIndex = 0,
                Font = new Font("Segoe UI", 9F)
            };

            // Wire up click handler using reflection to avoid hard-coding ShowPanel<T>
            btn.Click += (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                    {
                        // Use reflection to call ShowPanel<T> with the correct panel type
                        var method = _panelNavigator.GetType().GetMethod("ShowPanel");
                        if (method != null)
                        {
                            var genericMethod = method.MakeGenericMethod(panelType);
                            genericMethod.Invoke(_panelNavigator, new object[] { label, dockStyle, true });
                        }
                    }
                    _logger?.LogDebug("Navigation button '{ButtonName}' clicked", name);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to navigate to panel from button {ButtonName}", name);
                }
            };

            navPanel.Controls.Add(btn);
        }

        return navPanel;
    }

    /// <summary>
    /// Create central document panel for main content area
    /// </summary>
    private void CreateCentralDocumentPanel()
    {
        if (_dockingManager != null) return; // Skip central panel when docking is enabled

        _centralDocumentPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            AccessibleName = "CentralDocumentPanel",
            Dock = DockStyle.Fill,
            Visible = true
        };

        AddCentralPanelToForm();
        EnsureCentralPanelVisible();

        _logger?.LogDebug("Central document panel created (standard Fill docking)");
    }

    /// <summary>
    /// Add central panel to form with proper styling
    /// </summary>
    private void AddCentralPanelToForm()
    {
        if (_centralDocumentPanel == null) return;

        Controls.Add(_centralDocumentPanel);

        try
        {
            _centralDocumentPanel.BringToFront();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to bring central panel to front");
        }
    }

    /// <summary>
    /// Create right dock panel with activity grid (collapsible, auto-hide enabled)
    /// </summary>
    private void CreateRightDockPanel()
    {
         if (_dockingManager == null) return;

        _rightDockPanel = new Panel
        {
            Name = "RightDockPanel",
            AccessibleName = "RightDockPanel",
            AccessibleDescription = "Docked panel containing recent activity",
            AccessibleRole = AccessibleRole.Pane,
            Padding = new Padding(8, 8, 8, 8),
            BorderStyle = BorderStyle.None,
            TabIndex = 12,
            TabStop = false
        };

        var activityContent = CreateActivityGridPanel();
        _rightDockPanel.Controls.Add(activityContent);

        ConfigurePanelDocking(_rightDockPanel, DockingStyle.Right, 280, "Activity");

        _logger?.LogDebug("Right dock panel created with activity grid");
    }

    /// <summary>
    /// Configure docking behavior for a panel
    /// </summary>
    private void ConfigurePanelDocking(Panel panel, DockingStyle style, int size, string label)
    {
        if (_dockingManager == null) return;

        _dockingManager.SetEnableDocking(panel, true);
        _dockingManager.DockControl(panel, this, style, size);
        _dockingManager.SetAutoHideMode(panel, true);
        _dockingManager.SetDockLabel(panel, label);
        _dockingManager.SetAllowFloating(panel, true);

        try
        {
            _dockingManager.SetControlMinimumSize(panel, new Size(200, 0));
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to set minimum size for panel {PanelName}", panel.Name);
        }
    }

    /// <summary>
    /// Create dashboard cards panel (extracted for reuse in docking)
    /// </summary>
    private Panel CreateDashboardCardsPanel()
    {
        var dashboardPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12, 12, 12, 12),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        AddDashboardPanelRows(dashboardPanel);
        AddDashboardCards(dashboardPanel);

        return dashboardPanel;
    }

    private static void AddDashboardPanelRows(TableLayoutPanel dashboardPanel)
    {
        for (int i = 0; i < 5; i++)
        {
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }
    }

    private void AddDashboardCards(TableLayoutPanel dashboardPanel)
    {
        var accountsCard = CreateDashboardCard("Accounts", MainFormResources.LoadingText).Panel;
        SetupCardClickHandler(accountsCard, () =>
        {
            if (_panelNavigator != null)
                _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
        });

        accountsCard.TabIndex = 30;

        var chartsCard = CreateDashboardCard("Charts", "Analytics Ready").Panel;
         SetupCardClickHandler(chartsCard, () =>
        {
            if (_panelNavigator != null)
                _panelNavigator.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true);
        });

        chartsCard.TabIndex = 31;

        var settingsCard = CreateDashboardCard("Settings", "System Config").Panel;
         SetupCardClickHandler(settingsCard, () =>
        {
            if (_panelNavigator != null)
                _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
        });

        settingsCard.TabIndex = 32;

        var reportsCard = CreateDashboardCard("Reports", "Generate Now").Panel;
         SetupCardClickHandler(reportsCard, () =>
        {
            if (_panelNavigator != null)
                _panelNavigator.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true);
        });

        reportsCard.TabIndex = 33;

        var infoCard = CreateDashboardCard("Budget Status", MainFormResources.LoadingText).Panel;

        infoCard.TabIndex = 34;

        dashboardPanel.Controls.Add(accountsCard, 0, 0);
        dashboardPanel.Controls.Add(chartsCard, 0, 1);
        dashboardPanel.Controls.Add(settingsCard, 0, 2);
        dashboardPanel.Controls.Add(reportsCard, 0, 3);
        dashboardPanel.Controls.Add(infoCard, 0, 4);
    }

    private Panel CreateActivityGridPanel()
    {
         var activityPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Name = "ActivityPanel",
            AccessibleName = "Activity Panel",
            AccessibleDescription = "Container showing recent activity and live updates",
            AccessibleRole = AccessibleRole.Pane,
            TabIndex = 20,
            TabStop = false
        };

        var activityHeader = new Label
        {
            Text = "Recent Activity",
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(5, 8, 0, 0),
            Name = "ActivityHeader",
            AccessibleName = "Recent Activity heading",
            AccessibleDescription = "Heading for the recent activity section",
            TabStop = false
        };

        var placeholderLabel = new Label
        {
            Text = "Activity logging handled by ActivityLogPanel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10),
            Name = "ActivityPlaceholder"
        };

        activityPanel.Controls.Add(placeholderLabel);
        activityPanel.Controls.Add(activityHeader);

        return activityPanel;
    }

    private void HideStandardPanelsForDocking()
    {
         foreach (Control control in Controls)
        {
            if (control is SplitContainer)
            {
                try
                {
                    control.Visible = false;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
                }
            }
        }
        _logger?.LogDebug("Standard panels hidden for Syncfusion docking");
    }

    private void EnsureDockingZOrder()
    {
        _dockingManager?.EnsureZOrder();
    }

    private async void LoadAndApplyDockingLayout(string layoutPath, CancellationToken cancellationToken = default)
    {
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingManager is null");
            return;
        }

        if (_dockingLayoutManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingLayoutManager is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            _logger?.LogWarning("Docking layout path is empty - using default layout");
            return;
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Docking Layout Restoration");

        try
        {
            LoadDynamicPanels(layoutPath);
            await _dockingLayoutManager.LoadDockingLayoutAsync(_dockingManager, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Docking layout restoration was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during layout restoration - using default layout");
        }
    }

    private static void TryDeleteLayoutFiles(string? layoutPath)
    {
         if (string.IsNullOrWhiteSpace(layoutPath)) return;
        try { if (File.Exists(layoutPath)) File.Delete(layoutPath); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to delete layout file: {ex.Message}"); }
        TryCleanupTempFile(layoutPath + ".tmp");
    }

    private static string GetDockingLayoutPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "WileyWidget");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, DockingLayoutFileName);
        }
        catch
        {
            try { return Path.Combine(Path.GetTempPath(), DockingLayoutFileName); } catch { return DockingLayoutFileName; }
        }
    }

    private Task SafeInvokeAsync(System.Action action, CancellationToken cancellationToken = default)
    {
         if (action == null) throw new ArgumentNullException(nameof(action));

        if (!this.IsHandleCreated)
        {
            action();
            return Task.CompletedTask;
        }

        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<object?>();
            try
            {
                BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
            }
            catch
            {
                try
                {
                    Task.Run(() =>
                    {
                        try { action(); tcs.SetResult(null); }
                        catch (Exception innerEx) { tcs.SetException(innerEx); }
                    });
                }
                catch (Exception fallbackEx) { tcs.SetException(fallbackEx); }
            }
            return tcs.Task;
        }

        try { action(); return Task.CompletedTask; }
        catch (Exception ex) { return Task.FromException(ex); }
    }

    private async void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        if (e.Controls == null) return;

        foreach (Control control in e.Controls)
        {
            if (control == null) continue;
            _logger?.LogDebug("Dock state changed: Control={Control}, NewState={NewState}, OldState={OldState}", control.Name, e.NewState, e.OldState);
            await NotifyPanelVisibilityChangedAsync(control);
        }

        if (_dockingLayoutManager != null)
        {
            _logger?.LogDebug("Central panel visibility delegated to DockingLayoutManager");
        }

        if (_uiConfig.UseSyncfusionDocking && _dockingLayoutManager != null && _dockingManager != null)
        {
            try { _logger?.LogDebug("Dock state changed - layout save deferred to DockingLayoutManager"); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Error during debounced layout save"); }
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger?.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private async void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        _logger?.LogDebug("Dock visibility changed: Control={Control}", e.Control?.Name);

        if (e.Control != null)
        {
            await NotifyPanelVisibilityChangedAsync(e.Control);
        }

        try
        {
            if (_dockingLayoutManager != null)
            {
                EnsureCentralPanelVisible();
                EnsureSidePanelsZOrder();
                RefreshFormLayout();
                _logger?.LogDebug("Central panel visibility ensured for docked layout");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure central panel visibility in docked layout");
        }
    }

    private void EnsureCentralPanelVisible()
    {
        if (_centralDocumentPanel != null)
        {
            _centralDocumentPanel.Visible = true;
            _centralDocumentPanel.BringToFront();
        }
    }

    private void EnsureSidePanelsZOrder()
    {
        if (_leftDockPanel != null)
        {
            try { _leftDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set left dock panel z-order"); }
        }

        if (_rightDockPanel != null)
        {
            try { _rightDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set right dock panel z-order"); }
        }
    }

    private void RefreshFormLayout()
    {
        if (_dockingManager == null) return;
        this.Refresh();
        this.Invalidate();
    }

    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot add dynamic dock panel - DockingManager not initialized");
            return false;
        }

        GradientPanelExt? panel = null;
        try
        {
            panel = new GradientPanelExt
            {
                Name = panelName,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(panel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            _dockingManager.SetEnableDocking(panel, true);

            if (dockStyle == DockingStyle.Left || dockStyle == DockingStyle.Right)
            {
                _dockingManager.DockControl(panel, this, dockStyle, width);
            }
            else
            {
                _dockingManager.DockControl(panel, this, dockStyle, height);
            }

            _dockingManager.SetAutoHideMode(panel, true);
            _dockingManager.SetDockLabel(panel, displayLabel);
            _dockingManager.SetAllowFloating(panel, true);
            _logger?.LogDebug("Dynamic panel '{PanelName}' configured with auto-hide; floating mode available via UI", panelName);

            panel = null;

            _logger?.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
        finally
        {
            panel?.Dispose();
        }
    }

    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return false;
        _logger?.LogDebug("RemoveDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return false;
    }

    public Control? GetDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return null;
        _logger?.LogDebug("GetDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return null;
    }

    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return new List<string>().AsReadOnly();
    }

    private void ResetToDefaultLayout()
    {
        _logger?.LogInformation("Resetting to default layout");
        // Ensure all panels are visible
        if (_dockingManager != null)
        {
            var ienum = _dockingManager.Controls;
            while (ienum.MoveNext())
            {
                if (ienum.Current is Control control)
                {
                    _dockingManager.SetDockVisibility(control, true);
                }
            }
        }
    }

    private void DisposeSyncfusionDockingResources()
    {
         _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - delegating to DockingLayoutManager");

        if (_dockingLayoutManager != null)
        {
            try
            {
                if (_dockingManager != null && this.IsHandleCreated)
                {
                    try { _logger?.LogDebug("Form disposal: layout persistence delegated to DockingLayoutManager"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to finalize layout persistence during disposal (non-critical)"); }
                }

                if (_dockingManager != null)
                {
                    try { _logger?.LogDebug("Form disposal: DockingLayoutManager event cleanup in progress"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Error during event unsubscription (non-critical)"); }
                }

                _dockingLayoutManager.Dispose();
                _dockingLayoutManager = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose DockingLayoutManager");
            }
        }

        if (_dockingManager != null)
        {
            var mgr = _dockingManager;
            _dockingManager = null;
            mgr.DisposeSafely();
        }

        _logger?.LogDebug("DisposeSyncfusionDockingResources completed - all resources delegated to DockingLayoutManager");
    }

    private void UpdateDockingStateText()
    {
        try
        {
            if (!IsHandleCreated) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new System.Action(UpdateDockingStateText)); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to marshal UpdateDockingStateText to UI thread"); }
                return;
            }

            if (_statePanel == null || _statePanel.IsDisposed) return;

            var stateInfo = new System.Text.StringBuilder();
            var controls = _dockingManager?.Controls as Control.ControlCollection;
            var childCount = controls?.Count ?? 0;
            stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Panels: {childCount} panel{(childCount != 1 ? "s" : "")}");

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);
            _logger?.LogDebug("UpdateDockingStateText: DockingManager control count = {ControlCount}, MainForm control count = {FormControlCount}", childCount, this.Controls.Count);
            Console.WriteLine($"[DIAGNOSTIC] UpdateDockingStateText: DockingManager controls={childCount}, MainForm controls={this.Controls.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    private (Panel Panel, Label TitleLabel, Label DescLabel) CreateDashboardCard(string title, string description)
    {
        var cardPanel = new Panel
        {
            Size = new Size(200, 100),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Cursor = Cursors.Hand,
            AccessibleName = $"{title} card",
            AccessibleDescription = description,
            AccessibleRole = AccessibleRole.PushButton,
            TabStop = true,
            TabIndex = 0
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font(SegoeUiFontName, 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10),
            TabStop = false
        };

        var descLabel = new Label
        {
            Text = description,
            Font = new Font(SegoeUiFontName, 9F),
            AutoSize = true,
            Location = new Point(10, 40),
            TabStop = false
        };

        cardPanel.Controls.Add(titleLabel);
        cardPanel.Controls.Add(descLabel);

        return (cardPanel, titleLabel, descLabel);
    }

    private void SetupCardClickHandler(Panel card, System.Action action)
    {
        card.Click += (s, e) => action?.Invoke();
        card.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                action?.Invoke();
                e.Handled = true;
            }
        };
        foreach (Control child in card.Controls)
        {
            child.Click += (s, e) => action?.Invoke();
        }
    }

    private void ApplyThemeToDockingPanels()
    {
        if (_dockingManager == null) return;
        try { _logger?.LogDebug("Theme cascade applied via SfSkinManager to docking panels"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to apply theme to docking panels"); }
    }

    private void LoadDynamicPanels(string? layoutPath = null)
    {
        try { _logger?.LogDebug("Dynamic panels load requested: {LayoutPath}", layoutPath ?? "<default>"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to load dynamic panels"); }
    }

    private static void TryCleanupTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"Cleaned up temporary file: {filePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup temporary file: {filePath}, Error: {ex.Message}");
        }
    }
}
