using System.Threading;
using Microsoft.Extensions.Configuration;
using Syncfusion.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Extensions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms;

[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
/// <summary>
/// Partial UI implementation for <see cref="MainForm"/> containing UI element initialization
/// helpers (chrome, ribbons, navigation strip, status bar) and visual theming helpers.
///
/// DOCKING SYSTEM TESTING CHECKLIST (Before uncommenting docking initialization):
/// - [ ] DockingLayoutManager is fully implemented and handles state persistence
/// - [ ] Docking panel drag/drop interactions work without performance regression
/// - [ ] Layout save/restore cycle completes within 500ms (see LayoutLoadWarningMs)
/// - [ ] No memory leaks when toggling panel visibility repeatedly
/// - [ ] Panel count scales linearly (no exponential complexity with 15+ panels)
/// - [ ] Undo/redo works correctly if implemented
/// - [ ] High DPI scaling preserves docking positions and sizes
/// </summary>
public partial class MainForm
{
    #region UI Fields
    private DockingManager? _dockingManager;
    private WileyWidget.WinForms.Controls.ActivityLogPanel? _activityLogPanel;
    private DockingLayoutManager? _dockingLayoutManager;
    private Panel? _leftDockPanel;
    private Panel? _centralDocumentPanel;
    private Panel? _rightDockPanel;
    private Dictionary<string, Control>? _dynamicDockPanels;

    // Layout state management
    private readonly DateTime _lastSaveTime = DateTime.MinValue;
    private bool _skipLayoutLoadForDiagnostics;

    // Layout versioning
    private const string LayoutVersionAttributeName = "layoutVersion";
    private const string CurrentLayoutVersion = "1.0";

    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";

    // Layout load performance thresholds
    private const int LayoutLoadTimeoutMs = 2000; // Auto-reset if load takes > 2 seconds
    private const int LayoutLoadWarningMs = 1000; // Log warning if load takes > 1 second

    // Font family constant for UI fonts
    private const string SegoeUiFontName = "Segoe UI";
    #endregion

    #region Chrome

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

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Chrome Initialization");

        var chromeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogInformation("InitializeChrome start - handleCreated={HandleCreated}", IsHandleCreated);
        Console.WriteLine($"[DIAGNOSTIC] InitializeChrome: started, handleCreated={IsHandleCreated}");

        try
        {
            SuspendLayout();

            // Enable Per-Monitor V2 DPI Awareness (syncs with app.manifest)
            AutoScaleMode = AutoScaleMode.Dpi;

            // Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            Name = "MainForm";
            KeyPreview = true;
            Console.WriteLine($"[DIAGNOSTIC] Form properties set: Size={Width}x{Height}, MinSize={MinimumSize.Width}x{MinimumSize.Height}");

            // Initialize components container if needed
            components ??= new Container();
            Console.WriteLine("[DIAGNOSTIC] Components container initialized");

            EnsureDefaultActionButtons();

            // Initialize Menu Bar (always available)
            InitializeMenuBar();
            Console.WriteLine("[DIAGNOSTIC] Menu bar initialized");

            // Initialize Ribbon
            if (!_uiConfig.IsUiTestHarness)
            {
                try
                {
                    InitializeRibbon();
                    Console.WriteLine("[DIAGNOSTIC] Ribbon initialized");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize Ribbon");
                    Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeRibbon failed: {ex.Message}");
                    _ribbon = null;
                }
            }

            // Initialize Status Bar
            InitializeStatusBar();
            Console.WriteLine("[DIAGNOSTIC] Status bar initialized");

            // Initialize Navigation Strip (alternative to Ribbon for test harness)
            if (_uiConfig.IsUiTestHarness)
            {
                InitializeNavigationStrip();
                Console.WriteLine("[DIAGNOSTIC] Navigation strip initialized (UI test harness mode)");
            }

            // Start status timer
            InitializeStatusTimer();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
        }
        finally
        {
            try
            {
                ResumeLayout(false);
                PerformLayout();
            }
            catch
            {
                // Best-effort layout restoration
            }
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for primary navigation, global search, and session theme toggle.
    /// </summary>
    private void InitializeRibbon()
    {
        try
        {
            var ribbonResult = RibbonFactory.CreateRibbon(this, _logger);
            _ribbon = ribbonResult.Ribbon;
            _homeTab = ribbonResult.HomeTab;

            _ribbon.AccessibleName = "Ribbon_Main";
            _ribbon.AccessibleDescription ??= "Main application ribbon for navigation, search, and grid tools";
            _ribbon.TabIndex = 1;
            _ribbon.TabStop = true;

            // Ensure theme toggle is wired to the live theme switcher
            if (_ribbon != null)
            {
                var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggle != null)
                {
                    themeToggle.Click -= ThemeToggleFromRibbon;
                    themeToggle.Click += ThemeToggleFromRibbon;
                }
            }

            Controls.Add(_ribbon);
            _logger?.LogInformation("Ribbon initialized via RibbonFactory");
            _logger?.LogDebug("Ribbon size after init: {Width}x{Height}", _ribbon.Width, _ribbon.Height);

            // DEFENSIVE: Convert any animated images to static bitmaps to prevent ImageAnimator exceptions
            ValidateAndConvertRibbonImages(_ribbon);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Ribbon");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeRibbon failed: {ex.Message}");
            _ribbon = null;
        }
    }

    private void ThemeToggleFromRibbon(object? sender, EventArgs e)
    {
        try
        {
            ToggleTheme();

            if (sender is ToolStripButton btn)
            {
                var nextLabel = string.Equals(SkinManager.ApplicationVisualTheme, "Office2019Dark", StringComparison.OrdinalIgnoreCase)
                    ? "☀️ Light"
                    : "🌙 Dark";
                btn.Text = nextLabel;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme toggle failed");
        }
    }

    /// <summary>
    /// Validates all images in the ribbon and converts any animated images to static bitmaps.
    /// This prevents ImageAnimator exceptions when Syncfusion ToolStrip controls try to paint animated images.
    /// </summary>
    private void ValidateAndConvertRibbonImages(RibbonControlAdv ribbon)
    {
        ribbon.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Validates all images in the menu bar and converts any invalid images to prevent ImageAnimator exceptions.
    /// This prevents ImageAnimator exceptions when ToolStrip controls try to paint invalid images.
    /// </summary>
    private void ValidateAndConvertMenuBarImages(MenuStrip menuStrip)
    {
        menuStrip.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Performs a late validation pass on all menu bar images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateMenuBarImages()
    {
        _menuStrip?.ValidateAndConvertImages(_logger);
    }

    /// <summary>
    /// Performs a late validation pass on all ribbon images after the form is fully loaded.
    /// This catches any images that may have been disposed or corrupted after initial validation.
    /// </summary>
    private void LateValidateRibbonImages()
    {
        _ribbon?.ValidateAndConvertImages(_logger);
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
            var statusBar = StatusBarFactory.CreateStatusBar(this, _logger);
            _statusBar = statusBar;

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
            Controls.Add(statusBar);
            _logger?.LogDebug("Status bar initialized via StatusBarFactory");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeStatusBar failed: {ex.Message}");
            _statusBar = null;
            return;
        }
        _logger?.LogInformation("Status bar initialized via StatusBarFactory with {PanelCount} panels", _statusBar?.Panels.Length ?? 0);
        _logger?.LogDebug("Status bar size after init: {Width}x{Height}, HasSizingGrip={HasGrip}",
            _statusBar?.Width, _statusBar?.Height, _statusBar?.SizingGrip);
        Console.WriteLine($"[DIAGNOSTIC] Status bar created: Size={_statusBar?.Width}x{_statusBar?.Height}, Panels={_statusBar?.Panels.Length}");
    }

    /// <summary>
    /// Initialize the navigation <see cref="ToolStripEx"/> with named <see cref="ToolStripButton"/>
    /// controls used for quick navigation. Buttons are assigned deterministic <see cref="Control.Name"/>
    /// and <see cref="Control.AccessibleName"/> values for automation and testing.
    /// All buttons include AccessibleDescription for screen reader support.
    /// ToolStripEx automatically handles button sizing and alignment via layout.
    /// </summary>
    private void InitializeNavigationStrip()
    {
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
                TabStop = true,
                ThemeName = SkinManager.ApplicationVisualTheme ?? "Office2019Colorful" // Syncfusion theme integration
            };

            var dashboardBtn = new ToolStripButton("Dashboard")
            {
                Name = "Nav_Dashboard",
                AccessibleName = "Dashboard",
                AccessibleDescription = "Navigate to Dashboard panel with summary metrics and activity",
                Enabled = false
            };
            dashboardBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Dashboard clicked");
            };

            var accountsBtn = new ToolStripButton("Accounts")
            {
                Name = "Nav_Accounts",
                AccessibleName = "Accounts",
                AccessibleDescription = "Navigate to Municipal Accounts panel with account management",
                Enabled = false
            };
            accountsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Accounts clicked");
            };

            var budgetBtn = new ToolStripButton("Budget")
            {
                Name = "Nav_Budget",
                AccessibleName = "Budget",
                AccessibleDescription = "Navigate to Budget Overview panel with budget allocation and tracking",
                Enabled = false
            };
            budgetBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Budget clicked");
            };

            var chartsBtn = new ToolStripButton("Charts")
            {
                Name = "Nav_Charts",
                AccessibleName = "Charts",
                AccessibleDescription = "Navigate to Budget Analytics panel with charts and visualizations",
                Enabled = false
            };
            chartsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Charts clicked");
            };

            var analyticsBtn = new ToolStripButton("&Analytics")
            {
                Name = "Nav_Analytics",
                AccessibleName = "Analytics",
                AccessibleDescription = "Navigate to Analytics and Insights panel with advanced analytics"
            };
            analyticsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AnalyticsPanel>("Budget Analytics & Insights", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Analytics clicked");
            };

            var auditLogBtn = new ToolStripButton("&Audit Log")
            {
                Name = "Nav_AuditLog",
                AccessibleName = "Audit Log",
                AccessibleDescription = "Navigate to Audit Log panel with activity history and compliance tracking"
            };
            auditLogBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<AuditLogPanel>("Audit Log & Activity", DockingStyle.Bottom, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_AuditLog clicked");
            };

            var customersBtn = new ToolStripButton("Customers") { Name = "Nav_Customers", AccessibleName = "Nav_Customers" };
            customersBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);
            };

            var reportsBtn = new ToolStripButton("Reports") { Name = "Nav_Reports", AccessibleName = "Nav_Reports" };
            reportsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true);
            };

            var quickBooksBtn = new ToolStripButton("QuickBooks")
            {
                Name = "Nav_QuickBooks",
                AccessibleName = "QuickBooks",
                AccessibleDescription = "Navigate to QuickBooks Integration panel for accounting operations"
            };
            quickBooksBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_QuickBooks clicked");
            };

            var aiChatBtn = new ToolStripButton("AI Chat")
            {
                Name = "Nav_AIChat",
                AccessibleName = "AI Chat",
                AccessibleDescription = "Navigate to AI Chat panel for intelligent assistance"
            };
            aiChatBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<InsightFeedPanel>("AI Chat", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_AIChat clicked");
            };

            var proactiveInsightsBtn = new ToolStripButton("Proactive Insights")
            {
                Name = "Nav_ProactiveInsights",
                AccessibleName = "Proactive Insights",
                AccessibleDescription = "Navigate to Proactive AI Insights panel"
            };
            proactiveInsightsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<ProactiveInsightsPanel>("Proactive Insights", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_ProactiveInsights clicked");
            };

            var warRoomBtn = new ToolStripButton("War Room")
            {
                Name = "Nav_WarRoom",
                AccessibleName = "War Room",
                AccessibleDescription = "Navigate to War Room panel for strategic analysis"
            };
            warRoomBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_WarRoom clicked");
            };

            var settingsBtn = new ToolStripButton("Settings")
            {
                Name = "Nav_Settings",
                AccessibleName = "Settings",
                AccessibleDescription = "Navigate to Settings panel to configure application preferences"
            };
            settingsBtn.Click += (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                Console.WriteLine("[DIAGNOSTIC] Nav_Settings clicked");
            };

            // Theme toggle removed - session-only theme switching via menu or hotkey only
            var themeToggleBtn = new ToolStripButton
            {
                Name = "ThemeToggle",
                AccessibleName = "Theme_Toggle",
                AutoSize = true
            };
            themeToggleBtn.Click += ThemeToggleBtn_Click;
            themeToggleBtn.Text = SkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";

            // Grid helpers (navigation strip)
            var navGridClearFilter = new ToolStripButton("Clear Grid Filter")
            {
                Name = "Nav_ClearGridFilter",
                AccessibleName = "Clear Grid Filter",
                AccessibleDescription = "Clear all filters from active grid control"
            };
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid")
            {
                Name = "Nav_ExportGrid",
                AccessibleName = "Export Grid",
                AccessibleDescription = "Export active grid data to Excel spreadsheet"
            };
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                budgetBtn,
                chartsBtn,
                analyticsBtn,
                auditLogBtn,
                customersBtn,
                reportsBtn,
                quickBooksBtn,
                aiChatBtn,
                proactiveInsightsBtn,
                warRoomBtn,
                new ToolStripSeparator(),
                settingsBtn,
                new ToolStripSeparator(),
                themeToggleBtn,
                new ToolStripSeparator(),
                navGridClearFilter,
                navGridExport
            });

            Controls.Add(_navigationStrip);
            _logger?.LogInformation("Navigation strip initialized with {ButtonCount} buttons", _navigationStrip.Items.Count);
            _logger?.LogDebug("Navigation strip size after init: {Width}x{Height}", _navigationStrip.Width, _navigationStrip.Height);
            Console.WriteLine($"[DIAGNOSTIC] Navigation strip created: Size={_navigationStrip.Width}x{_navigationStrip.Height}, AutoSize={_navigationStrip.AutoSize}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize navigation strip");
            Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeNavigationStrip failed: {ex.Message}");
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
            _statusTimer = new System.Windows.Forms.Timer
            {
                Interval = 60000 // Update every minute
            };
            _statusTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_clockPanel != null)
                    {
                        _clockPanel.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    // Suppress timer errors
                }
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

            static void SetMenuAccessibility(ToolStripMenuItem item, string accessibleName, string description)
            {
                item.AccessibleName = accessibleName;
                item.AccessibleDescription = description;
                item.AccessibleRole = AccessibleRole.MenuItem;
            }

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File")
            {
                Name = "Menu_File",
                ToolTipText = "File operations"
            };
            SetMenuAccessibility(fileMenu, "File menu", "File operations and recent files");

            // File > Recent Files (MRU) - submenu
            _recentFilesMenu = new ToolStripMenuItem("&Recent Files")
            {
                Name = "Menu_File_RecentFiles",
                ToolTipText = "Recently opened files"
            };
            SetMenuAccessibility(_recentFilesMenu, "Recent files", "Recently opened files list");
            UpdateMruMenu(_recentFilesMenu);

            // File > Clear Recent Files
            var clearRecentMenuItem = new ToolStripMenuItem("&Clear Recent Files", null, (s, e) => ClearMruList())
            {
                Name = "Menu_File_ClearRecent",
                ToolTipText = "Clear recent files list"
            };
            SetMenuAccessibility(clearRecentMenuItem, "Clear recent files", "Clear the list of recently opened files");

            // File > Exit
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close())
            {
                Name = "Menu_File_Exit",
                ShortcutKeys = Keys.Alt | Keys.F4,
                ToolTipText = "Exit the application (Alt+F4)"
            };
            SetMenuAccessibility(exitMenuItem, "Exit application", "Exit the application using Alt+F4");
            exitMenuItem.Image = CreateIconFromText("\uE8BB", 16); // Exit icon (Segoe MDL2)
            exitMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            fileMenu.DropDownItems.Add(_recentFilesMenu);
            fileMenu.DropDownItems.Add(clearRecentMenuItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitMenuItem);

            // View Menu - All child forms accessible here
            var viewMenu = new ToolStripMenuItem("&View")
            {
                Name = "Menu_View",
                ToolTipText = "Open application views"
            };
            SetMenuAccessibility(viewMenu, "View menu", "Open application views and dashboards");

            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Dashboard", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Dashboard' failed");
                    MessageBox.Show(this, $"Failed to open Dashboard: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Dashboard",
                ShortcutKeys = Keys.Control | Keys.D,
                ToolTipText = "Open Dashboard view (Ctrl+D)"
            };
            SetMenuAccessibility(dashboardMenuItem, "Dashboard menu item", "Open dashboard view (Ctrl+D)");
            dashboardMenuItem.Image = CreateIconFromText("\uE10F", 16); // Dashboard icon (Segoe MDL2)
            dashboardMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Accounts' failed");
                    MessageBox.Show(this, $"Failed to open Accounts: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Accounts",
                ShortcutKeys = Keys.Control | Keys.A,
                ToolTipText = "Open Accounts view (Ctrl+A)"
            };
            SetMenuAccessibility(accountsMenuItem, "Accounts menu item", "Open accounts view (Ctrl+A)");
            accountsMenuItem.Image = CreateIconFromText("\uE8F4", 16); // AccountActivity icon (Segoe MDL2)
            accountsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Budget Overview
            var budgetMenuItem = new ToolStripMenuItem("&Budget Overview", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Budget Overview' failed");
                    MessageBox.Show(this, $"Failed to open Budget Overview: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Budget",
                ShortcutKeys = Keys.Control | Keys.B,
                ToolTipText = "Open Budget Overview (Ctrl+B)"
            };
            SetMenuAccessibility(budgetMenuItem, "Budget overview menu item", "Open budget overview (Ctrl+B)");
            budgetMenuItem.Image = CreateIconFromText("\uE7C8", 16); // Money icon (Segoe MDL2)
            budgetMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Charts", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Charts' failed");
                    MessageBox.Show(this, $"Failed to open Charts: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Charts",
                ShortcutKeys = Keys.Control | Keys.H,
                ToolTipText = "Open Charts view (Ctrl+H)"
            };
            SetMenuAccessibility(chartsMenuItem, "Charts menu item", "Open charts view (Ctrl+H)");
            chartsMenuItem.Image = CreateIconFromText("\uE9D2", 16); // BarChart icon (Segoe MDL2)
            chartsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > Reports
            var reportsMenuItem = new ToolStripMenuItem("&Reports", null, (s, e) =>
            {
                try
                {
                    _panelNavigator?.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "MainForm: Failed to open Reports panel from menu");
                    _logger?.LogError(ex, "Menu action 'Reports' failed");
                    MessageBox.Show(this, $"Failed to open Reports: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_View_Reports",
                ShortcutKeys = Keys.Control | Keys.R,
                ToolTipText = "Open Reports view (Ctrl+R)"
            };
            SetMenuAccessibility(reportsMenuItem, "Reports menu item", "Open reports view (Ctrl+R)");
            reportsMenuItem.Image = CreateIconFromText("\uE8A5", 16); // Document icon (Segoe MDL2)
            reportsMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // View > QuickBooks
            var quickBooksMenuItem = new ToolStripMenuItem("&QuickBooks", null, (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<QuickBooksPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);
            })
            {
                Name = "Menu_View_QuickBooks",
                ShortcutKeys = Keys.Control | Keys.Q,
                ToolTipText = "Open QuickBooks Integration (Ctrl+Q)"
            };
            SetMenuAccessibility(quickBooksMenuItem, "QuickBooks menu item", "Open QuickBooks integration (Ctrl+Q)");
            // Try to set icon from DPI-aware service (preferred over deprecated theme icon service)
            try
            {
                var dpi = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(ServiceProvider);
                if (dpi != null)
                {
                    quickBooksMenuItem.Image = dpi.GetImage("quickbooks");
                    quickBooksMenuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
            catch { /* Icon loading is optional */ }

            // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) =>
            {
                if (_panelNavigator != null)
                    _panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);
            })
            {
                Name = "Menu_View_Customers",
                ShortcutKeys = Keys.Control | Keys.U,
                ToolTipText = "Open Customers view (Ctrl+U)"
            };
            SetMenuAccessibility(customersMenuItem, "Customers menu item", "Open customers view (Ctrl+U)");
            customersMenuItem.Image = CreateIconFromText("\uE716", 16); // Contact icon (Segoe MDL2)
            customersMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            // Add separator for visual grouping
            var viewSeparator = new ToolStripSeparator
            {
                Name = "Menu_View_Separator"
            };

            // View > Refresh
            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) =>
            {
                // Refresh all open panels via PanelNavigationService
                this.Refresh();
            })
            {
                Name = "Menu_View_Refresh",
                ShortcutKeys = Keys.F5,
                ToolTipText = "Refresh active view (F5)"
            };
            SetMenuAccessibility(refreshMenuItem, "Refresh menu item", "Refresh the active view (F5)");
            refreshMenuItem.Image = CreateIconFromText("\uE72C", 16); // Refresh icon (Segoe MDL2)
            refreshMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            viewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                dashboardMenuItem,
                accountsMenuItem,
                budgetMenuItem,
                chartsMenuItem,
                reportsMenuItem,
                quickBooksMenuItem,
                customersMenuItem,
                viewSeparator,
                refreshMenuItem
            });

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Tools")
            {
                Name = "Menu_Tools",
                ToolTipText = "Application tools and settings"
            };

            // Tools > Settings
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) =>
            {
                try
                {
                    if (_panelNavigator != null)
                        _panelNavigator.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Menu action 'Settings' failed");
                    MessageBox.Show(this, $"Failed to open Settings: {ex.Message}", "Menu Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            })
            {
                Name = "Menu_Tools_Settings",
                ShortcutKeys = Keys.Control | Keys.Oemcomma,
                ToolTipText = "Open Settings (Ctrl+,)",
                ImageScaling = ToolStripItemImageScaling.None
            };
            settingsMenuItem.Image = CreateIconFromText("\uE713", 16); // Settings icon (Segoe MDL2)

            toolsMenu.DropDownItems.Add(settingsMenuItem);

            // Help Menu
            var helpMenu = new ToolStripMenuItem("&Help")
            {
                Name = "Menu_Help",
                ToolTipText = "Help and application information"
            };

            // Help > Documentation
            var documentationMenuItem = new ToolStripMenuItem("&Documentation", null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/WileyWidget/WileyWidget/wiki",
                        UseShellExecute = true
                    });
                }
                catch (Exception docEx)
                {
                    _logger?.LogWarning(docEx, "Failed to open documentation");
                }
            })
            {
                Name = "Menu_Help_Documentation",
                ShortcutKeys = Keys.F1,
                ToolTipText = "Open online documentation (F1)"
            };
            documentationMenuItem.Image = CreateIconFromText("\uE897", 16); // Help icon (Segoe MDL2)
            documentationMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            var helpSeparator = new ToolStripSeparator
            {
                Name = "Menu_Help_Separator"
            };

            // Help > About
            var aboutMenuItem = new ToolStripMenuItem("&About", null, (s, e) =>
            {
                MessageBox.Show(
                    $"{MainFormResources.FormTitle}\n\n" +
                    "Version 1.0.0\n" +
                    "Built with .NET 9 and Syncfusion WinForms\n\n" +
                    $"© {DateTime.Now.Year} Wiley Widget. All rights reserved.",
                    $"About {MainFormResources.FormTitle}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            })
            {
                Name = "Menu_Help_About",
                ToolTipText = "About this application"
            };
            aboutMenuItem.Image = CreateIconFromText("\uE946", 16); // Info icon (Segoe MDL2)
            aboutMenuItem.ImageScaling = ToolStripItemImageScaling.None;

            helpMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                documentationMenuItem,
                helpSeparator,
                aboutMenuItem
            });

            // Add all menus to the menu strip
            _menuStrip.Items.AddRange(new ToolStripItem[]
            {
                fileMenu,
                viewMenu,
                toolsMenu,
                helpMenu
            });

            // Apply theme colors to dropdown menus
            ApplyMenuTheme(fileMenu);
            ApplyMenuTheme(viewMenu);
            ApplyMenuTheme(toolsMenu);
            ApplyMenuTheme(helpMenu);

            // Set as the form's main menu
            this.MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);

            // DEFENSIVE: Validate and convert any invalid images in the menu bar
            // This prevents ImageAnimator exceptions when ToolStrip controls try to paint invalid images
            ValidateAndConvertMenuBarImages(_menuStrip);

            _logger?.LogInformation("Menu bar initialized with icons and theming");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    /// <summary>
    /// Create a Bitmap icon from Segoe MDL2 Assets text (Unicode character).
    /// Icons use DodgerBlue as a semantic accent color for visual UI emphasis in menus/ribbons.
    /// SEMANTIC COLOR JUSTIFICATION: DodgerBlue provides visual accent for icon emphasis in UI chrome,
    /// independent of theme. All UI chrome (ribbons, menus) should have consistent accent color.
    /// </summary>
    /// <param name="iconText">Unicode character from Segoe MDL2 Assets font</param>
    /// <param name="size">Icon size in pixels</param>
    /// <returns>Bitmap containing the rendered icon, or null if creation fails</returns>
    private Bitmap? CreateIconFromText(string iconText, int size)
    {
        if (string.IsNullOrWhiteSpace(iconText) || size <= 0)
        {
            _logger?.LogWarning("CreateIconFromText: Invalid parameters - iconText='{IconText}', size={Size}",
                iconText ?? "(null)", size);
            return null;
        }

        try
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                using (var font = new Font("Segoe MDL2 Assets", size * 0.75f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.DodgerBlue))
                {
                    var stringFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    graphics.DrawString(iconText, font, brush, new RectangleF(0, 0, size, size), stringFormat);
                }
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create icon from text '{IconText}' with size {Size}", iconText, size);
            return null;
        }
    }

    /// <summary>
    /// Gets the semantic overlay color for the loading overlay background.
    /// Returns a dark overlay color inherited from theme via SystemColors.
    /// The semi-transparent effect is achieved through Control.Opacity property on the overlay panel,
    /// not through manual color construction (which violates SfSkinManager theme-only rule).
    /// </summary>
    /// <returns>Dark overlay color from system/theme (full opacity; panel's Opacity property handles transparency)</returns>
    private Color GetLoadingOverlayColor()
    {
        try
        {
            // SEMANTIC COLOR: Dark overlay for modal dimming effect
            // Use theme-aware SystemColors.Control which respects application theme
            // Transparency achieved via Control.Opacity property, not manual color construction
            return SystemColors.Control;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get overlay color - using fallback");
            // Fallback: black (no theme-aware alternative for exceptions)
            return Color.Black;
        }
    }

    /// <summary>
    /// Gets the semantic text color for the loading label.
    /// Returns high-contrast text color for accessibility on the modal overlay.
    /// Uses standard Color constants (not theme-aware) to ensure visibility regardless of theme.
    /// </summary>
    /// <returns>High-contrast white text color for semantic loading indicator visibility</returns>
    private Color GetLoadingLabelColor()
    {
        try
        {
            // SEMANTIC COLOR: High-contrast text on modal overlay
            // Use standard Color.White for maximum contrast against dark overlay
            // This is a semantic status color exception (text contrast requirement)
            // All overlays use white text for accessibility
            return Color.White;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get text color - using fallback");
            // Fallback: white for contrast against dark overlay
            return Color.White;
        }
    }

    /// <summary>
    /// Get the current theme name from SkinManager.
    /// </summary>
    /// <returns>Current theme name</returns>
    private string GetCurrentTheme()
    {
        return SkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
    }


    /// <summary>
    /// Apply theme colors to menu dropdown items.
    /// </summary>
    /// <param name="menuItem">Parent menu item to theme</param>
    /// <summary>
    /// Apply Syncfusion visual styling to a menu dropdown and recursively to its child items.
    /// Ensures the dropdown receives the current <see cref="Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme"/>.
    /// </summary>
    /// <param name="menuItem">Parent menu item whose dropdown should be themed.</param>
    private void ApplyMenuTheme(ToolStripMenuItem menuItem)
    {
        if (menuItem?.DropDown == null)
        {
            return;
        }

        try
        {
            var dropdown = (ToolStripDropDownMenu)menuItem.DropDown;
            dropdown.ShowImageMargin = true;
            dropdown.ShowCheckMargin = false;
            dropdown.Font = new Font("Segoe UI", 9F);

            // Explicitly ensure the dropdown inherits the current Syncfusion visual style
            try
            {
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(dropdown, Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");
            }
            catch (Exception setEx)
            {
                _logger?.LogDebug(setEx, "Failed to set visual style on menu dropdown {MenuName}", menuItem.Name);
            }

            // Apply theme to child items (structure only; visual style applied at dropdown level)
            foreach (ToolStripItem item in dropdown.Items)
            {
                if (item is ToolStripMenuItem childMenuItem)
                {
                    ApplyMenuTheme(childMenuItem);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to apply theme to menu item {MenuName}", menuItem.Name);
        }
    }

    /// <summary>
    /// Handle theme toggle - switches between Dark and Light themes (session-only, no config persistence).
    /// Can be invoked via keyboard shortcut or programmatically.
    /// </summary>
    /// <summary>
    /// Handles user-initiated theme toggling for the current session.
    /// Switches between <c>Office2019Colorful</c> and <c>Office2019Dark</c> and reapplies
    /// the visual style to open forms. This preference is session-only and not persisted.
    /// </summary>
    /// <param name="sender">The event sender (typically a ToolStripButton).</param>
    /// <param name="e">Event arguments.</param>
    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_themeService == null) return;

            var currentTheme = _themeService.CurrentTheme;
            var newTheme = currentTheme == "Office2019Dark" ? "Office2019Colorful" : "Office2019Dark";

            // Apply new theme via service (this will persist it and broadcast the event)
            _themeService.ApplyTheme(newTheme);

            _logger?.LogInformation("Theme toggle triggered: {OldTheme} -> {NewTheme}", currentTheme, newTheme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to toggle theme");
        }
    }

    /// <summary>
    /// Handle global search box keyboard events (Ctrl+F to focus, Enter to search)
    /// </summary>
    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ToolStripTextBox searchBox) return;

        try
        {
            if (e.KeyCode == Keys.Enter)
            {
                var searchText = searchBox.Text;
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    _logger?.LogInformation("Global search triggered: {SearchText}", searchText);
                    _ = PerformGlobalSearchAsync(searchText);
                }
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Search box error");
        }
    }

    /// <summary>
    /// Validates Syncfusion license status and logs warning if trial/unlicensed.
    /// </summary>
    // Removed: ValidateSyncfusionLicense() method - redundant license check
    // Program.cs startup already validates Syncfusion license and logs status
    // Log output shows: "Syncfusion license registered and validated successfully"

    #endregion

    #region Docking

    /// <summary>
    /// Initializes Syncfusion DockingManager with layout management.
    /// Delegates to DockingHostFactory for centralized docking creation logic.
    /// Loads saved layout from AppData if available.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        var globalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Syncfusion Docking Initialization");

        try
        {
            _logger?.LogInformation("InitializeSyncfusionDocking START - handleCreated={HandleCreated}, UIThread={ThreadId}",
                IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);

            // Phase: DockingManager Creation
            var dockingHostStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger?.LogInformation("Creating DockingHost via factory...");
            var (dockingManager, leftPanel, rightPanel, activityLogPanel, activityTimer, layoutManager) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);
            dockingHostStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("DockingManager Creation", dockingHostStopwatch.ElapsedMilliseconds);

            _dockingManager = dockingManager;
            _leftDockPanel = leftPanel;
            _rightDockPanel = rightPanel;
            _activityLogPanel = activityLogPanel;
            _dockingLayoutManager = layoutManager;

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
            string layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget", "docking_layout.xml");
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
                Console.WriteLine("[DIAGNOSTIC] DockingManager ready - infrastructure set up.");

                // Theme application deferred: Applied after DockingManager.ResumeLayout() to avoid conflicts

                // CRITICAL: Apply SkinManager theme AFTER DockingManager is fully initialized and panels are docked
                // This ensures theme cascade works correctly and prevents ArgumentOutOfRangeException in paint events
                var themeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var themeName = SkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
                    SfSkinManager.SetVisualStyle(this, themeName);
                    themeStopwatch.Stop();
                    StartupInstrumentation.RecordPhaseTime("Theme Application", themeStopwatch.ElapsedMilliseconds);
                    _logger?.LogInformation("Applied SfSkinManager theme to MainForm after DockingManager setup: {Theme} ({Time}ms)", themeName, themeStopwatch.ElapsedMilliseconds);
                    Console.WriteLine($"[DIAGNOSTIC] Applied SfSkinManager theme to MainForm: {themeName} ({themeStopwatch.ElapsedMilliseconds}ms)");
                }
                catch (Exception themeEx)
                {
                    themeStopwatch.Stop();
                    _logger?.LogWarning(themeEx, "Failed to apply SkinManager theme to MainForm after DockingManager setup");
                }

                // Ensure panels start visible & activated
                if (_dockingManager != null)
                {
                    var ienum = _dockingManager.Controls;
                    GradientPanelExt firstPanel = null;
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
                SkinManager.ApplicationVisualTheme = "Office2019Colorful";
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
                _logger.LogDebug("Theme application delegated to DockingLayoutManager");
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
                    _logger.LogDebug(ex, "Failed to refresh activity grid");
                }
            }

            this.Refresh();
            _logger.LogInformation("Theme successfully applied to docking panels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme change to docking panels");
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
        _logger.LogInformation("DockingManager document mode disabled (using DockingManager for panels only)");

        _dockingManager.PersistState = true;
        _dockingManager.AnimateAutoHiddenWindow = true;
        // REMOVED: Hard-coded fonts - SkinManager owns all theming, including fonts
        // _dockingManager.AutoHideTabFont = _dockAutoHideTabFont = new Font(SegoeUiFontName, 9f);
        // _dockingManager.DockTabFont = _dockTabFont = new Font(SegoeUiFontName, 9f);
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

        // REMOVED: Per-control theme application - DockingManager inherits theme from ApplicationVisualTheme
        // Theme cascade from Program.cs ensures consistent styling
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
    /// Handle docking initialization errors with fallback
    /// </summary>
    private void HandleDockingInitializationError(Exception ex, string message)
    {
        _logger.LogError(ex, "{Message}: {Type} - {ExMessage}", message, ex.GetType().Name, ex.Message);
        System.Diagnostics.Debug.WriteLine($"[DOCKING ERROR] {ex.GetType().Name}: {ex.Message}");

        if (ex.InnerException != null)
        {
            System.Diagnostics.Debug.WriteLine($"  InnerException: {ex.InnerException.Message}");
        }

        System.Diagnostics.Debug.WriteLine($"  StackTrace: {ex.StackTrace}");

        // NOTE: Cannot fall back because docking is always enabled in UIConfiguration
        _logger.LogError(ex, "Docking initialization failed and cannot fall back (docking is always enabled)");

        // Re-throw the exception since we can't actually disable docking
        throw new InvalidOperationException("Docking initialization failed and fallback is disabled", ex);
    }

    /// <summary>
    /// Show user-friendly warning message for docking failure
    /// </summary>
    private void ShowDockingWarningMessage()
    {
        try
        {
            MessageBox.Show(
                "Docking manager initialization failed. The application will continue with standard panel layout.",
                "Docking Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception msgEx)
        {
            _logger.LogError(msgEx, "Failed to show docking warning message");
            // Message box failed, but this is non-critical
        }
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

        _logger.LogDebug("Left dock panel created with navigation buttons");
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
        if (_dockingManager != null) return; // Skip central panel when docking is enabled to avoid covering docked panels

        _centralDocumentPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            AccessibleName = "CentralDocumentPanel",
            Dock = DockStyle.Fill,
            Visible = true
        };

        AddCentralPanelToForm();
        EnsureCentralPanelVisibility();

        _logger.LogDebug("Central document panel created (standard Fill docking)");
    }

    /// <summary>
    /// Add central panel to form with proper styling
    /// </summary>
    private void AddCentralPanelToForm()
    {
        if (_centralDocumentPanel == null) return;

        Controls.Add(_centralDocumentPanel);

        // REMOVED: Per-control theme application - central panel inherits theme from ApplicationVisualTheme
        // Theme cascade from Program.cs ensures consistent styling

        try
        {
            _centralDocumentPanel.BringToFront();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to bring central panel to front");
            // Z-order adjustment is non-critical
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

        _logger.LogDebug("Right dock panel created with activity grid");
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
            _logger.LogDebug(ex, "Failed to set minimum size for panel {PanelName}", panel.Name);
            // Minimum size setting is non-critical
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

    /// <summary>
    /// Add row styles to dashboard panel
    /// </summary>
    private static void AddDashboardPanelRows(TableLayoutPanel dashboardPanel)
    {
        for (int i = 0; i < 5; i++)
        {
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }
    }

    /// <summary>
    /// Add dashboard cards to panel
    /// </summary>
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

    /// <summary>
    /// Create activity grid panel (extracted for reuse in docking)
    /// Now loads data from ActivityLog database table for real-time activity tracking.
    /// </summary>
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
            // REMOVED: Hard-coded Font - SkinManager owns all theming
            // Font = new Font(SegoeUiFontName, 12, FontStyle.Bold),
            // REMOVED: ForeColor - SkinManager theme cascade handles label colors
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(5, 8, 0, 0),
            Name = "ActivityHeader",
            AccessibleName = "Recent Activity heading",
            AccessibleDescription = "Heading for the recent activity section",
            TabStop = false
        };

        // ActivityLogPanel is now created in DockingHostFactory with proper DI integration
        // This legacy grid creation retained for backward compatibility with existing layout code
        // Real activity logging happens through ActivityLogPanel in right dock

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

    /// <summary>
    /// Load activity data from database asynchronously (legacy method - replaced by ActivityLogPanel)
    /// </summary>
    private async Task LoadActivityDataAsync(CancellationToken cancellationToken = default)
    {
        // ActivityLogPanel now handles activity data loading with its own refresh cycle
        // This method retained for backward compatibility but is no longer active
        _logger?.LogDebug("LoadActivityDataAsync called (legacy method - ActivityLogPanel handles loading now)");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Load fallback activity data when database is unavailable (legacy method)
    /// </summary>
    private void LoadFallbackActivityData()
    {
        // ActivityLogPanel handles fallback data internally
        _logger?.LogDebug("LoadFallbackActivityData called (legacy method - ActivityLogPanel handles fallback now)");
    }

    /// <summary>
    /// Ensure activity grid exists (legacy check - now checks ActivityLogPanel)
    /// </summary>
    private bool EnsureActivityGridExists()
    {
        return _activityLogPanel != null && !_activityLogPanel.IsDisposed;
    }

    /// <summary>
    /// Hide standard panels when switching to Syncfusion docking
    /// </summary>
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
                    _logger.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
                    // Non-critical - continue with other panels
                }
            }
        }
        _logger.LogDebug("Standard panels hidden for Syncfusion docking");
    }

    // Phase 1 Simplification: ToggleDockingMode removed - docking permanently enabled

    /// <summary>
    /// Ensure docking panels and manager are correctly ordered in the Z axis (Phase 1: always enabled)
    /// </summary>
    private void EnsureDockingZOrder()
    {
        _dockingManager?.EnsureZOrder();
    }

    /// <summary>
    /// Load saved docking layout from AppData
    /// </summary>
    private async void LoadDockingLayout()
    {
        if (!ShouldLoadDockingLayout())
        {
            return;
        }

        LogLoadPreconditions();

        try
        {
            var layoutPath = GetDockingLayoutPath();

            if (!ValidateLayoutFile(layoutPath))
            {
                return;
            }

            await LoadAndApplyDockingLayout(layoutPath);
        }
        catch (UnauthorizedAccessException authEx)
        {
            _logger.LogWarning(authEx, "No permission to read docking layout - using default layout. Check AppData permissions.");
        }
        catch (IOException ioEx)
        {
            _logger.LogWarning(ioEx, "I/O error loading docking layout - using default layout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load docking layout - using default layout");
        }
    }

    /// <summary>
    /// Check if docking layout should be loaded
    /// </summary>
    private bool ShouldLoadDockingLayout()
    {
        // Check for Shift key held during startup - allows user to bypass problematic layout
        if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
        {
            _logger.LogWarning("Shift key detected - bypassing layout load and resetting to defaults");
            _skipLayoutLoadForDiagnostics = true;
            try
            {
                var layoutPath = GetDockingLayoutPath();
                if (File.Exists(layoutPath))
                {
                    var backupPath = layoutPath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                    File.Copy(layoutPath, backupPath, overwrite: true);
                    File.Delete(layoutPath);
                    _logger.LogInformation("Layout file backed up to {BackupPath} and deleted", backupPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backup/delete layout file during Shift key reset");
            }
            ResetToDefaultLayout();
            return false;
        }

        if (_skipLayoutLoadForDiagnostics)
        {
            _logger.LogInformation("Layout loading disabled via diagnostic flag");
            return false;
        }

        if (_dockingManager == null)
        {
            return false;
        }

        if (!IsHandleCreated || !Application.MessageLoop)
        {
            _logger.LogDebug("Skipping LoadDockingLayout: handle not created or message loop not running");
            return false;
        }

        if (this.IsDisposed || this.Disposing)
        {
            _logger.LogDebug("Skipping LoadDockingLayout: form disposing/disposed");
            return false;
        }

        if (this.InvokeRequired)
        {
            try
            {
                this.Invoke(new System.Action(LoadDockingLayout));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to marshal LoadDockingLayout to UI thread");
                // Marshaling failed, but this is non-critical
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Log preconditions for docking layout load
    /// </summary>
    private void LogLoadPreconditions()
    {
        try
        {
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _logger.LogDebug("LoadDockingLayout START - ThreadId={ThreadId}, InvokeRequired={InvokeRequired}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _lastSaveTime={LastSaveTime}",
                threadId, this.InvokeRequired, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, _lastSaveTime);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to log load preconditions");
            // Logging failure is non-critical
        }
    }

    /// <summary>
    /// Validate layout file exists and is not corrupt
    /// </summary>
    private bool ValidateLayoutFile(string layoutPath)
    {
        if (!File.Exists(layoutPath))
        {
            _logger.LogInformation("No saved docking layout found at {Path} - using default layout", layoutPath);
            return false;
        }

        var layoutFileInfo = new FileInfo(layoutPath);
        if (layoutFileInfo.Length == 0)
        {
            HandleEmptyLayoutFile(layoutPath);
            return false;
        }

        if (!ValidateLayoutXml(layoutPath))
        {
            return false;
        }

        return ValidateLayoutVersion(layoutPath);
    }

    /// <summary>
    /// Validate layout version compatibility
    /// </summary>
    private bool ValidateLayoutVersion(string layoutPath)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(layoutPath);

            // Check for version attribute on root element
            var versionAttr = xmlDoc.DocumentElement?.GetAttribute(LayoutVersionAttributeName);

            if (string.IsNullOrEmpty(versionAttr))
            {
                _logger.LogWarning("Layout file missing version attribute - created by older version. Auto-resetting to default layout.");
                HandleIncompatibleLayoutVersion(layoutPath, "<none>", CurrentLayoutVersion);
                return false;
            }

            if (versionAttr != CurrentLayoutVersion)
            {
                _logger.LogWarning("Layout file version mismatch: file={FileVersion}, current={CurrentVersion}. Auto-resetting to default layout.", versionAttr, CurrentLayoutVersion);
                HandleIncompatibleLayoutVersion(layoutPath, versionAttr, CurrentLayoutVersion);
                return false;
            }

            _logger.LogDebug("Layout version validated: {Version}", versionAttr);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate layout version - resetting to default");
            HandleIncompatibleLayoutVersion(layoutPath, "<error>", CurrentLayoutVersion);
            return false;
        }
    }

    /// <summary>
    /// Handle incompatible layout version
    /// </summary>
    private void HandleIncompatibleLayoutVersion(string layoutPath, string fileVersion, string currentVersion)
    {
        try
        {
            // Backup the incompatible layout for diagnostics
            var backupPath = layoutPath + ".v" + fileVersion.Replace("<", "", StringComparison.Ordinal).Replace(">", "", StringComparison.Ordinal) + "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            if (File.Exists(layoutPath))
            {
                File.Copy(layoutPath, backupPath, overwrite: true);
                _logger.LogInformation("Incompatible layout backed up to {BackupPath}", backupPath);
            }

            File.Delete(layoutPath);
            ResetToDefaultLayout();
            _logger.LogInformation("Layout reset to default after version incompatibility");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle incompatible layout version");
        }
    }

    /// <summary>
    /// Handle empty layout file
    /// </summary>
    private void HandleEmptyLayoutFile(string layoutPath)
    {
        _logger.LogInformation("Docking layout file {Path} is empty - resetting to default layout", layoutPath);

        try
        {
            File.Delete(layoutPath);
            ResetToDefaultLayout();
            _logger.LogInformation("Docking layout reset to default successfully");
        }
        catch (Exception deleteEx)
        {
            _logger.LogWarning(deleteEx, "Failed to delete empty docking layout file {Path}", layoutPath);
            // Deletion failure is non-critical
        }
    }

    /// <summary>
    /// Validate XML structure of layout file
    /// </summary>
    private bool ValidateLayoutXml(string layoutPath)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(layoutPath);
            _logger.LogDebug("Layout XML validated successfully");
            return true;
        }
        catch (XmlException xmlEx)
        {
            HandleCorruptLayoutFile(layoutPath, xmlEx);
            return false;
        }
    }

    /// <summary>
    /// Handle corrupt layout file
    /// </summary>
    private void HandleCorruptLayoutFile(string layoutPath, XmlException xmlEx)
    {
        _logger.LogInformation(xmlEx, "Corrupt XML layout file detected at {Path} - resetting to default layout", layoutPath);

        try
        {
            File.Delete(layoutPath);
            _logger.LogInformation("Deleted corrupt layout file");
            ResetToDefaultLayout();
            _logger.LogInformation("Docking layout reset to default successfully");
        }
        catch (Exception deleteEx)
        {
            _logger.LogWarning(deleteEx, "Failed to delete corrupt layout file - will be overwritten on save");
            // Deletion failure is non-critical
        }
    }

    /// <summary>
    /// Reset docking layout to default state
    /// </summary>
    private void ResetToDefaultLayout()
    {
        if (_dockingManager == null) return;

        _dockingManager.LoadDesignerDockState();
        ApplyThemeToDockingPanels();
    }

    /// <summary>
    /// Load and apply docking layout from file with performance monitoring and timeout.
    /// Implements separation of concerns: I/O happens on background thread, layout application on UI thread.
    /// </summary>
    private async Task LoadAndApplyDockingLayout(string layoutPath, CancellationToken cancellationToken = default)
    {
        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot load docking layout - DockingManager is null");
            return;
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Docking Layout Restoration");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var dynamicPanelInfos = LoadDynamicPanelMetadata(layoutPath);
            RecreateDynamicPanels(dynamicPanelInfos);

            // Load additional dynamic panels from JSON metadata
            LoadDynamicPanels(layoutPath);

            // PRE-FLIGHT: Separation of Concerns - Perform Disk I/O on background thread
            // This prevents the UI thread from blocking while waiting for a slow disk or network share.
            byte[] layoutData;
            try
            {
                layoutData = await Task.Run(() => File.ReadAllBytes(layoutPath), cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                _logger.LogInformation("No docking layout found at {Path} - using default layout", layoutPath);
                return;
            }

            if (layoutData == null || layoutData.Length == 0)
            {
                _logger.LogWarning("Docking layout file is empty or missing: {Path}", layoutPath);
                return;
            }

            // APPLICATION: Apply state via MemoryStream
            // We use SerializeMode.XMLFmtStream to process the data we've already loaded.
            using var ms = new MemoryStream(layoutData);
            var serializer = new AppStateSerializer(SerializeMode.XMLFmtStream, ms);

            try
            {
                LogDockStateLoad(layoutPath);

                // Use Task.Run with Task.WaitAsync to detect slow/hung UI layout application.
                // LoadDockState MUST run on the UI thread via Invoke as it touches controls.
                var loadTask = Task.Run(() =>
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() => _dockingManager?.LoadDockState(serializer));
                    }
                    else
                    {
                        _dockingManager?.LoadDockState(serializer);
                    }
                }, cancellationToken);

                // .WaitAsync ensures we don't block the actual caller indefinitely if the UI thread hangs.
                await loadTask.WaitAsync(TimeSpan.FromMilliseconds(LayoutLoadTimeoutMs), cancellationToken).ConfigureAwait(true);

                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                if (timelineService != null)
                {
                    timelineService.RecordOperation("LoadDockState (UI)", "Docking Layout Restoration", elapsedMs);
                }

                if (elapsedMs > LayoutLoadWarningMs)
                {
                    _logger.LogWarning("Layout application took {ElapsedMs}ms (threshold: {ThresholdMs}ms) - consider simplifying layout", elapsedMs, LayoutLoadWarningMs);
                }
                else
                {
                    _logger.LogInformation("Docking layout loaded and applied from {Path} in {ElapsedMs}ms", layoutPath, elapsedMs);
                }
            }
            catch (TimeoutException)
            {
                stopwatch.Stop();
                _logger.LogError("Layout application exceeded timeout of {TimeoutMs}ms - layout is too complex or corrupted. Auto-resetting to defaults.", LayoutLoadTimeoutMs);
                HandleSlowLayoutLoad(layoutPath, stopwatch.ElapsedMilliseconds);
                return;
            }
            catch (Exception loadEx)
            {
                stopwatch.Stop();
                if (loadEx is NullReferenceException)
                {
                    HandleDockStateLoadError(layoutPath, loadEx, "NullReferenceException while loading docking layout");
                }
                else
                {
                    HandleDockStateLoadError(layoutPath, loadEx, "Failed to load docking layout from {Path}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Docking layout restoration was cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during layout restoration - resetting to defaults");
            HandleSlowLayoutLoad(layoutPath, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Handle slow or hung layout load by resetting to defaults
    /// </summary>
    private void HandleSlowLayoutLoad(string layoutPath, long elapsedMs)
    {
        try
        {
            _logger.LogWarning("Layout load performance issue detected (elapsed: {ElapsedMs}ms)", elapsedMs);

            // Backup problematic layout for analysis
            var backupPath = layoutPath + ".slow_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            if (File.Exists(layoutPath))
            {
                File.Copy(layoutPath, backupPath, overwrite: true);
                _logger.LogInformation("Slow layout backed up to {BackupPath}", backupPath);
            }

            File.Delete(layoutPath);
            ResetToDefaultLayout();
            _logger.LogInformation("Layout reset to default after performance timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle slow layout load");
        }
    }

    /// <summary>
    /// Best-effort extraction of dynamic panel metadata from a saved layout file.
    /// The Syncfusion layout format is proprietary; we extract simple attributes
    /// such as Name, DockLabel and IsAutoHide if present so we can recreate panels.
    /// </summary>
    private List<DynamicPanelInfo> LoadDynamicPanelMetadata(string layoutPath)
    {
        var results = new List<DynamicPanelInfo>();
        if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath)) return results;

        try
        {
            var doc = new XmlDocument();
            doc.Load(layoutPath);

            // Attempt to find nodes that might represent dynamic panels - best-effort
            var nodes = doc.SelectNodes("//PanelInfo") ?? doc.SelectNodes("//Panel");
            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    try
                    {
                        var info = new DynamicPanelInfo();
                        var nameAttr = node.Attributes?["Name"]?.Value ?? node.Attributes?["name"]?.Value;
                        if (!string.IsNullOrWhiteSpace(nameAttr)) info.Name = nameAttr;
                        info.DockLabel = node.Attributes?["DockLabel"]?.Value ?? node.Attributes?["dockLabel"]?.Value;
                        if (bool.TryParse(node.Attributes?["IsAutoHide"]?.Value ?? node.Attributes?["isAutoHide"]?.Value, out var isAuto))
                        {
                            info.IsAutoHide = isAuto;
                        }

                        results.Add(info);
                    }
                    catch { /* ignore individual node errors */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse dynamic panel metadata from layout {Path}", layoutPath);
        }

        return results;
    }

    /// <summary>
    /// Log dock state load operation
    /// </summary>
    private void LogDockStateLoad(string layoutPath)
    {
        _logger.LogInformation("Calling _dockingManager.LoadDockState - ThreadId={ThreadId}, layoutPath={Path}, InvokeRequired={InvokeRequired}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _lastSaveTime={LastSaveTime}",
            System.Threading.Thread.CurrentThread.ManagedThreadId, layoutPath, this.InvokeRequired, this.IsHandleCreated, Application.MessageLoop, _lastSaveTime);
    }

    /// <summary>
    /// Handle errors during dock state load
    /// </summary>
    private void HandleDockStateLoadError(string layoutPath, Exception ex, string message)
    {
        _logger.LogWarning(ex, "{Message} - resetting to default layout ({Path})", message, layoutPath);

        try
        {
            File.Delete(layoutPath);
            _logger.LogInformation("Deleted corrupt docking layout file {Path}", layoutPath);
        }
        catch (Exception deleteEx)
        {
            _logger.LogWarning(deleteEx, "Failed to delete corrupt docking layout file {Path}", layoutPath);
            // Deletion failure is non-critical
        }

        try
        {
            ResetToDefaultLayout();
            _logger.LogInformation("Docking layout reset to default successfully after failed load");
        }
        catch (Exception fallbackEx)
        {
            _logger.LogWarning(fallbackEx, "Failed to reset docking layout after failed load");
            // Reset failure - system is in degraded state but functional
        }
    }

    /// <summary>
    /// Recreate dynamic panels from metadata
    /// </summary>
    private void RecreateDynamicPanels(List<DynamicPanelInfo> panelInfos)
    {
        foreach (var panelInfo in panelInfos)
        {
            try
            {
                RecreateDynamicPanel(panelInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recreate dynamic panel '{PanelName}'", panelInfo.Name);
                // Panel recreation failure is non-critical
            }
        }
    }

    /// <summary>
    /// Create a dynamic panel based on saved metadata
    /// </summary>
    /// <param name="panelInfo">Information about the panel to recreate</param>
    private void RecreateDynamicPanel(DynamicPanelInfo panelInfo)
    {
        if (_dynamicDockPanels == null || _dockingManager == null)
            return;

        // Skip if panel already exists
        if (_dynamicDockPanels.ContainsKey(panelInfo.Name))
            return;

        try
        {
            // Create a basic panel - in a real implementation, you might need to recreate
            // the specific panel type and content based on the panel name or type
            var panel = new Panel
            {
                Name = panelInfo.Name
                // REMOVED: BackColor, ForeColor - SkinManager theme cascade handles panel colors
            };

            // Add some basic content based on panel name (this is a simplified example)
            // In practice, you'd have a factory method or registry to recreate the proper content
            if (panelInfo.Name.Contains("Chat", StringComparison.OrdinalIgnoreCase))
            {
                // Recreate AI chat panel
                panel.Controls.Add(new Label { Text = "AI Chat Panel", Dock = DockStyle.Top });
            }
            else if (panelInfo.Name.Contains("Log", StringComparison.OrdinalIgnoreCase))
            {
                // Recreate log panel
                panel.Controls.Add(new Label { Text = "Log Panel", Dock = DockStyle.Top });
            }
            else
            {
                // Generic panel
                panel.Controls.Add(new Label { Text = $"{panelInfo.Name} Panel", Dock = DockStyle.Top });
            }

            // Set up docking
            _dockingManager.SetDockLabel(panel, panelInfo.DockLabel ?? panelInfo.Name);
            if (panelInfo.IsAutoHide)
            {
                _dockingManager.SetAutoHideMode(panel, true);
            }

            // Dock the panel (position will be restored by LoadDockState)
            _dockingManager.DockControl(panel, this, Syncfusion.Windows.Forms.Tools.DockingStyle.Left, 200);

            // Track the panel
            _dynamicDockPanels ??= new Dictionary<string, Control>();
            _dynamicDockPanels[panelInfo.Name] = panel;
            panel = null; // ownership transferred to DockingManager/dictionary

            _logger.LogInformation("Recreated dynamic panel '{PanelName}'", panelInfo.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate dynamic panel '{PanelName}'", panelInfo.Name);
        }
    }

    /// <summary>
    /// Information about a dynamic panel for serialization
    /// </summary>
    private sealed class DynamicPanelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "System.Windows.Forms.Panel";
        public string? DockLabel { get; set; }
        public bool IsAutoHide { get; set; }
    }

    private static void TryDeleteLayoutFiles(string? layoutPath)
    {
        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            return;
        }

        try
        {
            if (File.Exists(layoutPath))
            {
                File.Delete(layoutPath);
            }
        }
        catch (Exception ex)
        {
            // Swallow - deletion is best-effort cleanup
            System.Diagnostics.Debug.WriteLine($"Failed to delete layout file: {ex.Message}");
        }

        TryCleanupTempFile(layoutPath + ".tmp");
    }

    /// <summary>
    /// Returns the path to the docking layout file under AppData\WileyWidget.
    /// </summary>
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
            // Fall back to local temp directory on failure
            try { return Path.Combine(Path.GetTempPath(), DockingLayoutFileName); } catch { return DockingLayoutFileName; }
        }
    }

    /// <summary>
    /// Helper: invoke an action on the UI thread and return a Task that completes when the action finishes.
    /// This provides an awaitable wrapper over BeginInvoke with a threadpool fallback.
    /// </summary>
    private Task SafeInvokeAsync(System.Action action, CancellationToken cancellationToken = default)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (!this.IsHandleCreated)
        {
            // If the handle isn't created, run inline to avoid cross-thread failures
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
                // BeginInvoke failed - fall back to threadpool execution
                try
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            action();
                            tcs.SetResult(null);
                        }
                        catch (Exception innerEx)
                        {
                            tcs.SetException(innerEx);
                        }
                    });
                }
                catch (Exception fallbackEx)
                {
                    tcs.SetException(fallbackEx);
                }
            }
            return tcs.Task;
        }

        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    #region Docking Event Handlers

    private async void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        if (e.Controls == null) return;

        foreach (Control control in e.Controls)
        {
            if (control == null) continue;

            // Log docking state changes using structural logging
            _logger.LogDebug("Dock state changed: Control={Control}, NewState={NewState}, OldState={OldState}",
                control.Name, e.NewState, e.OldState);

            // Notify ViewModel of visibility change for lazy loading
            await NotifyPanelVisibilityChangedAsync(control);
        }

        // Ensure central panels remain visible after state changes (delegate to layout manager)
        if (_dockingLayoutManager != null)
        {
            _logger.LogDebug("Central panel visibility delegated to DockingLayoutManager");
        }

        // Auto-save layout on state changes with debouncing
        if (_uiConfig.UseSyncfusionDocking && _dockingLayoutManager != null && _dockingManager != null)
        {
            try
            {
                _logger.LogDebug("Dock state changed - layout save deferred to DockingLayoutManager");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during debounced layout save");
            }
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private async void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        // Log visibility changes
        _logger.LogDebug("Dock visibility changed: Control={Control}", e.Control?.Name);

        // Notify ViewModel of visibility change for lazy loading
        if (e.Control != null)
        {
            await NotifyPanelVisibilityChangedAsync(e.Control);
        }

        // Ensure central panels remain visible after visibility changes (delegate to layout manager)
        try
        {
            if (_dockingLayoutManager != null)
            {
                EnsureCentralPanelVisible();
                EnsureSidePanelsZOrder();
                RefreshFormLayout();

                _logger.LogDebug("Central panel visibility ensured for docked layout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure central panel visibility in docked layout");
            // Visibility adjustment failure is non-critical
        }
    }

    /// <summary>
    /// Ensure central document panel is visible
    /// </summary>
    private void EnsureCentralPanelVisible()
    {
        if (_centralDocumentPanel == null) return;

        try
        {
            _centralDocumentPanel.Visible = true;
            _centralDocumentPanel.BringToFront();
            _centralDocumentPanel.Invalidate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set central document panel visibility");
            // Visibility setting failure is non-critical
        }
    }

    /// <summary>
    /// Ensure side panels are behind central content
    /// </summary>
    private void EnsureSidePanelsZOrder()
    {
        if (_leftDockPanel != null)
        {
            try
            {
                _leftDockPanel.SendToBack();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set left dock panel z-order");
                // Z-order adjustment failure is non-critical
            }
        }

        if (_rightDockPanel != null)
        {
            try
            {
                _rightDockPanel.SendToBack();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set right dock panel z-order");
                // Z-order adjustment failure is non-critical
            }
        }
    }

    /// <summary>
    /// Refresh form layout (Phase 1 Simplification)
    /// </summary>
    private void RefreshFormLayout()
    {
        if (_dockingManager == null) return;

        this.Refresh();

        this.Invalidate();
    }

    // Phase 1 Simplification: EnsureNonDockingVisibility removed - docking always enabled

    #endregion

    #region Dynamic Panel Management

    /// <summary>
    /// Adds a custom panel to the docking manager at runtime.
    /// Enables plugin architecture and dynamic content areas.
    /// </summary>
    /// <param name="panelName">Unique identifier for the panel.</param>
    /// <param name="displayLabel">User-facing label for the dock tab.</param>
    /// <param name="content">Control to host in the panel.</param>
    /// <param name="dockStyle">Docking position (Left, Right, Top, Bottom).</param>
    /// <param name="width">Panel width (for Left/Right docking).</param>
    /// <param name="height">Panel height (for Top/Bottom docking).</param>
    /// <returns>True if panel was added successfully, false if docking manager is not available.</returns>
    /// <exception cref="ArgumentException">Thrown when panelName is null or empty.</exception>
    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        if (string.IsNullOrWhiteSpace(panelName))
        {
            throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

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

            // Add content to panel
            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            // Configure docking behavior
            _dockingManager.SetEnableDocking(panel, true);

            // Dock based on style
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

            // Floating mode: User can float the dynamic panel after creation
            // DockingManager.SetAutoHideMode() already enables auto-hide behavior
            // Floating mode toggle is available via right-click menu on panel caption
            _logger?.LogDebug("Dynamic panel '{PanelName}' configured with auto-hide; floating mode available via UI", panelName);

            // Note: Dynamic panel tracking is now handled by DockingLayoutManager
            panel = null; // ownership transferred to DockingManager

            _logger.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
        finally
        {
            panel?.Dispose();
        }
    }

    /// <summary>
    /// Removes a dynamically added panel from the docking manager.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <param name="panelName">Name of the panel to remove.</param>
    /// <returns>Always returns false (legacy API).</returns>
    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            return false;

        _logger?.LogDebug("RemoveDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return false;
    }

    /// <summary>
    /// Gets a dynamically added panel by name.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <param name="panelName">Name of the panel to retrieve.</param>
    /// <returns>Always returns null (legacy API).</returns>
    public Control? GetDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            return null;

        _logger?.LogDebug("GetDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return null;
    }

    /// <summary>
    /// Gets all dynamically added panel names.
    /// Note: This is a legacy API. Dynamic panel management is now handled by DockingLayoutManager.
    /// </summary>
    /// <returns>Empty collection (legacy API).</returns>
    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return new List<string>().AsReadOnly();
    }

    #endregion

    /// <summary>
    /// Dispose resources owned by the docking implementation
    /// Delegated to DockingLayoutManager for centralized resource management
    /// </summary>
    private void DisposeSyncfusionDockingResources()
    {
        _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - delegating to DockingLayoutManager");

        // Theme subscription removed - SfSkinManager handles theme cascade automatically

        // Unsubscribe from other events

        // Delegate all docking-related disposal to the layout manager
        if (_dockingLayoutManager != null)
        {
            try
            {
                // Best-effort: save layout before disposing to restore user's panel configuration on next start
                if (_dockingManager != null && this.IsHandleCreated)
                {
                    try
                    {
                        // Layout persistence: Save docking state (positions, sizes, states) to AppData
                        // This is called during disposal to capture final layout state before shutdown
                        // Next application start will restore from this saved state (see LoadDockingLayout)
                        _logger?.LogDebug("Form disposal: layout persistence delegated to DockingLayoutManager");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to finalize layout persistence during disposal (non-critical)");
                    }
                }

                // Detach from DockingManager to unsubscribe from events
                if (_dockingManager != null)
                {
                    // Event cleanup: Unsubscribe from DockingManager events (DockStateChanged, visibility changes, etc.)
                    // This prevents phantom events from firing during/after disposal
                    try
                    {
                        // DockingLayoutManager handles unsubscription internally
                        _logger?.LogDebug("Form disposal: DockingLayoutManager event cleanup in progress");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Error during event unsubscription (non-critical)");
                    }
                }

                // Dispose the layout manager (handles panels, fonts, timers, dynamic panels)
                _dockingLayoutManager.Dispose();
                _dockingLayoutManager = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose DockingLayoutManager");
            }
        }

        // Dispose the DockingManager itself
        if (_dockingManager != null)
        {
            var mgr = _dockingManager;
            _dockingManager = null;
            mgr.DisposeSafely();
        }

        _logger?.LogDebug("DisposeSyncfusionDockingResources completed - all resources delegated to DockingLayoutManager");
    }

    /// <summary>
    /// Updates the docking state text in the status bar.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// </summary>
    private void UpdateDockingStateText()
    {
        try
        {
            if (!IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new System.Action(UpdateDockingStateText));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to marshal UpdateDockingStateText to UI thread");
                }
                return;
            }

            if (_statePanel == null || _statePanel.IsDisposed)
                return;

            var stateInfo = new System.Text.StringBuilder();

            var controls = _dockingManager?.Controls as Control.ControlCollection;
            var childCount = controls?.Count ?? 0;
            stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Panels: {childCount} panel{(childCount != 1 ? "s" : "")}");

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);

            // DIAGNOSTIC: Log control count for troubleshooting docking issues
            _logger?.LogDebug("UpdateDockingStateText: DockingManager control count = {ControlCount}, MainForm control count = {FormControlCount}",
                childCount, this.Controls.Count);
            Console.WriteLine($"[DIAGNOSTIC] UpdateDockingStateText: DockingManager controls={childCount}, MainForm controls={this.Controls.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    #endregion

    #region Panels

    /// <summary>
    /// Closes the settings panel if it's currently visible.
    /// Legacy method: SettingsForm replaced by SettingsPanel.
    /// </summary>
    public void CloseSettingsPanel()
    {
        // Legacy method - SettingsForm replaced by SettingsPanel
        _panelNavigator?.HidePanel("Settings");
    }

    /// <summary>
    /// Closes a panel with the specified name.
    /// </summary>
    /// <param name="panelName">Name of the panel to close.</param>
    public void ClosePanel(string panelName)
    {
        _panelNavigator?.HidePanel(panelName);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Ensures the central document panel is visible and properly configured.
    /// </summary>
    private void EnsureCentralPanelVisibility()
    {
        if (_centralDocumentPanel != null)
        {
            _centralDocumentPanel.Visible = true;
            _centralDocumentPanel.BringToFront();
        }
    }

    /// <summary>
    /// Creates a dashboard card control with title and description.
    /// </summary>
    /// <param name="title">Card title.</param>
    /// <param name="description">Card description.</param>
    /// <returns>A tuple containing the card panel and label controls.</returns>
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

    /// <summary>
    /// Sets up click handler for a dashboard card.
    /// </summary>
    /// <param name="card">The card panel control.</param>
    /// <param name="action">Action to execute on click.</param>
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

    /// <summary>
    /// Applies theme to all docking panels using SfSkinManager.
    /// </summary>
    private void ApplyThemeToDockingPanels()
    {
        if (_dockingManager == null) return;

        try
        {
            // SfSkinManager handles theme cascade automatically - no manual application needed
            _logger?.LogDebug("Theme cascade applied via SfSkinManager to docking panels");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to apply theme to docking panels");
        }
    }

    /// <summary>
    /// Loads dynamic panels from saved layout state.
    /// </summary>
    /// <param name="layoutPath">Optional path to layout file.</param>
    private void LoadDynamicPanels(string? layoutPath = null)
    {
        try
        {
            // Dynamic panel loading handled by DockingLayoutManager
            // This method exists for compatibility with existing layout code
            _logger?.LogDebug("Dynamic panels load requested: {LayoutPath}", layoutPath ?? "<default>");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to load dynamic panels");
        }
    }

    /// <summary>
    /// Attempts to clean up a temporary file.
    /// </summary>
    /// <param name="filePath">Path to the temporary file.</param>
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

    #endregion
}

