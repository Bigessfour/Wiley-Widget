using Microsoft.Extensions.Configuration;
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
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms;

[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class MainForm
{
    #region UI Fields
    private DockingManager? _dockingManager;
    private Panel? _leftDockPanel;
    private Panel? _rightDockPanel;
    private Panel? _centralDocumentPanel;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _activityGrid;
    private System.Windows.Forms.Timer? _activityRefreshTimer;
    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";
    // Layout versioning for compatibility detection
    private const string LayoutVersionAttributeName = "LayoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    // Performance thresholds for layout loading diagnostics
    private const int LayoutLoadTimeoutMs = 1000; // Auto-reset if load takes > 1 second
    private const int LayoutLoadWarningMs = 500;  // Log warning if load takes > 500ms
    // Diagnostic flag to temporarily disable layout loading (set via Shift key on startup)
    private bool _skipLayoutLoadForDiagnostics = false;
    // Fonts used by DockingManager - keep references so we can dispose them
    private Font? _dockAutoHideTabFont;
    private Font? _dockTabFont;
    // Debounce timer for auto-save to prevent I/O spam
    private System.Windows.Forms.Timer? _dockingLayoutSaveTimer;
    // Flag to prevent concurrent saves
    private bool _isSavingLayout = false;
    private readonly object _dockingSaveLock = new();
    // Track last save time to enforce minimum interval
    private DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan MinimumSaveInterval = TimeSpan.FromMilliseconds(2000); // 2 seconds minimum between saves
    // Dictionary to track dynamically added panels
    private Dictionary<string, Panel>? _dynamicDockPanels;
    // Font family constant for UI fonts
    private const string SegoeUiFontName = "Segoe UI";
    private readonly Dictionary<Type, Form> _activeMdiChildren = new();
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

        try
        {
            SuspendLayout();

            // NOTE: Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally.
            // No need to call SetVisualStyle here - it cascades automatically from the global setting.
            // var configuredTheme = _configuration?.GetValue<string>("UI:Theme", ThemeColors.DefaultTheme) ?? ThemeColors.DefaultTheme;
            // REMOVED: Redundant theme application - theme already set globally in Program.InitializeTheme()

            // License validation removed - Program.cs startup already validates Syncfusion license

            // Set form properties
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            // REMOVED: BackColor = ThemeColors.Background; - SfSkinManager owns all color decisions
            Name = "MainForm";

            // Initialize components container if needed
            components ??= new Container();

            // Initialize Menu Bar (always available)
            InitializeMenuBar();

            // Initialize Ribbon
            if (!_uiConfig.IsUiTestHarness)
            {
                InitializeRibbon();
            }

            // Initialize Status Bar
            InitializeStatusBar();

            // Initialize Navigation Strip (alternative to Ribbon for test harness)
            if (_uiConfig.IsUiTestHarness)
            {
                InitializeNavigationStrip();
            }

            // Start status timer
            InitializeStatusTimer();

            ResumeLayout(false);
            PerformLayout();

            _logger?.LogDebug("UI chrome initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize UI chrome");
            // Continue without chrome - application can still function
        }
    }

    /// <summary>
    /// Initialize Syncfusion RibbonControlAdv for main navigation.
    /// </summary>
    private void InitializeRibbon()
    {
        try
        {
            _ribbon = new RibbonControlAdv
            {
                Name = "Ribbon_Main",
                AccessibleName = "Ribbon_Main",
                Dock = (DockStyleEx)DockStyle.Top,
                OfficeColorScheme = ToolStripEx.ColorScheme.Managed,
                RibbonStyle = RibbonStyle.Office2016
            };

            // Apply theme
            try
            {
                SfSkinManager.SetVisualStyle(_ribbon, AppThemeColors.DefaultTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to apply theme to Ribbon");
            }

            // Create Home Tab
            _homeTab = new ToolStripTabItem
            {
                Name = "HomeTab",
                Text = "&Home"
            };

            // Ensure Panel is initialized
            if (_homeTab.Panel == null)
            {
                _homeTab.Panel = new RibbonPanel();
                // Note: Theme is inherited from parent ribbon, no need to apply separately
            }

            // Create Home Tab Panel
            var homePanel = new ToolStripEx
            {
                Name = "HomePanel"
            };

            // Add navigation buttons
            var dashboardBtn = new ToolStripButton
            {
                Name = "Nav_Dashboard",
                AccessibleName = "Nav_Dashboard",  // Automation ID for UI tests
                Text = "📊 " + MainFormResources.Dashboard,
                ToolTipText = "Open Dashboard",
                AutoSize = true
            };
            dashboardBtn.Click += (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);

            var accountsBtn = new ToolStripButton
            {
                Name = "Nav_Accounts",
                AccessibleName = "Nav_Accounts",  // Automation ID for UI tests
                Text = "💼 " + MainFormResources.Accounts,
                ToolTipText = "Open Accounts",
                AutoSize = true
            };
            accountsBtn.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false);

            var budgetBtn = new ToolStripButton
            {
                Name = "Nav_Budget",
                AccessibleName = "Nav_Budget",  // Automation ID for UI tests
                Text = "💰 Budget Overview",
                ToolTipText = "Open Budget Overview",
                AutoSize = true
            };
            budgetBtn.Click += (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>(allowMultiple: false);

            var chartsBtn = new ToolStripButton
            {
                Name = "Nav_Charts",
                AccessibleName = "Nav_Charts",  // Automation ID for UI tests
                Text = "📈 " + MainFormResources.Charts,
                ToolTipText = "Open Charts",
                AutoSize = true
            };
            chartsBtn.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false);

            var customersBtn = new ToolStripButton
            {
                Name = "Nav_Customers",
                AccessibleName = "Nav_Customers",  // Automation ID for UI tests
                Text = "👥 Customers",
                ToolTipText = "Open Customers",
                AutoSize = true
            };
            customersBtn.Click += (s, e) => ShowChildForm<CustomersForm, CustomersViewModel>(allowMultiple: false);

            var reportsBtn = new ToolStripButton
            {
                Name = "Nav_Reports",
                AccessibleName = "Nav_Reports",  // Automation ID for UI tests
                Text = "📄 " + MainFormResources.Reports,
                ToolTipText = "Open Reports",
                AutoSize = true
            };
            reportsBtn.Click += (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false);

            var aiChatBtn = new ToolStripButton
            {
                Name = "Nav_AIChat",
                AccessibleName = "Nav_AIChat",  // Automation ID for UI tests
                Text = "AI Chat",
                ToolTipText = "Open AI Chat Assistant",
                AutoSize = true
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    aiChatBtn.Image = iconService.GetIcon("chat", AppTheme.Office2019Colorful, 16);
                }
            }
            catch { /* Icon loading is optional */ }
            aiChatBtn.Click += (s, e) => ShowChildForm<ChatWindow, object>(allowMultiple: false);

            var quickBooksBtn = new ToolStripButton
            {
                Name = "Nav_QuickBooks",
                AccessibleName = "Nav_QuickBooks",  // Automation ID for UI tests
                Text = "QuickBooks",
                ToolTipText = "Open QuickBooks Integration",
                AutoSize = true
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    quickBooksBtn.Image = iconService.GetIcon("quickbooks", AppTheme.Office2019Colorful, 16);
                }
            }
            catch { /* Icon loading is optional */ }
            quickBooksBtn.Click += (s, e) => ShowChildForm<QuickBooksForm, object>(allowMultiple: false);

            var settingsBtn = new ToolStripButton
            {
                Name = "Nav_Settings",
                AccessibleName = "Nav_Settings",  // Automation ID for UI tests
                Text = "⚙️ " + MainFormResources.Settings,
                ToolTipText = "Open Settings",
                AutoSize = true
            };
            settingsBtn.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false);

            // Theme toggle removed - session-only theme switching via menu or hotkey only

            homePanel.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                budgetBtn,
                chartsBtn,
                customersBtn,
                reportsBtn,
                aiChatBtn,
                quickBooksBtn,
                new ToolStripSeparator(),
                settingsBtn,
                new ToolStripSeparator(),
                // Grid commands
                new ToolStripLabel { Text = "Grid:" , Name = "Grid_Label" },
                new ToolStripButton { Name = "Grid_SortAsc", Text = "Sort Asc", AutoSize = true },
                new ToolStripButton { Name = "Grid_SortDesc", Text = "Sort Desc", AutoSize = true },
                new ToolStripButton { Name = "Grid_ApplyTestFilter", Text = "Apply Filter", AutoSize = true },
                new ToolStripButton { Name = "Grid_ClearFilter", Text = "Clear Filter", AutoSize = true },
                new ToolStripButton { Name = "Grid_ExportExcel", Text = "Export Grid", AutoSize = true }
            });

            // Wire grid command events
            var sortAscBtn = homePanel.Items.Find("Grid_SortAsc", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var sortDescBtn = homePanel.Items.Find("Grid_SortDesc", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var applyFilterBtn = homePanel.Items.Find("Grid_ApplyTestFilter", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var clearFilterBtn = homePanel.Items.Find("Grid_ClearFilter", searchAllChildren: true).FirstOrDefault() as ToolStripButton;
            var exportGridBtn = homePanel.Items.Find("Grid_ExportExcel", searchAllChildren: true).FirstOrDefault() as ToolStripButton;

            if (sortAscBtn != null)
                sortAscBtn.Click += (s, e) => SortActiveGridByFirstSortableColumn(descending: false);
            if (sortDescBtn != null)
                sortDescBtn.Click += (s, e) => SortActiveGridByFirstSortableColumn(descending: true);
            if (applyFilterBtn != null)
                applyFilterBtn.Click += (s, e) => ApplyTestFilterToActiveGrid();
            if (clearFilterBtn != null)
                clearFilterBtn.Click += (s, e) => ClearActiveGridFilter();
            if (exportGridBtn != null)
                exportGridBtn.Click += async (s, e) => await ExportActiveGridToExcel();

            _homeTab.Panel.AddToolStrip(homePanel);
            _ribbon.Header.AddMainItem(_homeTab);

            Controls.Add(_ribbon);
            _logger?.LogDebug("Ribbon initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Ribbon");
            _ribbon = null;
        }
    }

    /// <summary>
    /// Initialize Syncfusion StatusBarAdv for status information.
    /// </summary>
    private void InitializeStatusBar()
    {
        try
        {
            _statusBar = new StatusBarAdv
            {
                Name = "StatusBar_Main",
                AccessibleName = "StatusBar_Main",
                Dock = DockStyle.Bottom,
                BeforeTouchSize = new Size(1400, 26)
            };

            // Apply theme
            try
            {
                SfSkinManager.SetVisualStyle(_statusBar, AppThemeColors.DefaultTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to apply theme to StatusBar");
            }

            // Status label (left)
            _statusLabel = new StatusBarAdvPanel
            {
                Name = "StatusLabel",
                Text = "Ready",
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Left
            };

            // Status text panel (center)
            _statusTextPanel = new StatusBarAdvPanel
            {
                Name = "StatusTextPanel",
                Text = string.Empty,
                Size = new System.Drawing.Size(200, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Center
            };

            // State panel (MDI/Docking indicator)
            _statePanel = new StatusBarAdvPanel
            {
                Name = "StatePanel",
                Text = string.Empty,
                Size = new System.Drawing.Size(100, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Left
            };

            // Clock panel (right)
            _clockPanel = new StatusBarAdvPanel
            {
                Name = "ClockPanel",
                Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
                Size = new System.Drawing.Size(80, 27),
                HAlign = Syncfusion.Windows.Forms.Tools.HorzFlowAlign.Right
            };

            _statusBar.Controls.Add(_statusLabel);
            _statusBar.Controls.Add(_statusTextPanel);
            _statusBar.Controls.Add(_statePanel);
            _statusBar.Controls.Add(_clockPanel);

            Controls.Add(_statusBar);
            _logger?.LogDebug("Status bar initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Status Bar");
            _statusBar = null;
        }
    }

    /// <summary>
    /// Initialize simple navigation strip for test harness mode.
    /// </summary>
    private void InitializeNavigationStrip()
    {
        try
        {
            _navigationStrip = new ToolStripEx
            {
                Name = "NavigationStrip",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden
            };

            var dashboardBtn = new ToolStripButton("Dashboard") { Name = "Nav_Dashboard", AccessibleName = "Nav_Dashboard" };
            dashboardBtn.Click += (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);

            var accountsBtn = new ToolStripButton("Accounts") { Name = "Nav_Accounts", AccessibleName = "Nav_Accounts" };
            accountsBtn.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false);

            var budgetBtn = new ToolStripButton("Budget") { Name = "Nav_Budget", AccessibleName = "Nav_Budget" };
            budgetBtn.Click += (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>(allowMultiple: false);

            var chartsBtn = new ToolStripButton("Charts") { Name = "Nav_Charts", AccessibleName = "Nav_Charts" };
            chartsBtn.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false);

            var customersBtn = new ToolStripButton("Customers") { Name = "Nav_Customers", AccessibleName = "Nav_Customers" };
            customersBtn.Click += (s, e) => ShowChildForm<CustomersForm, CustomersViewModel>(allowMultiple: false);

            var reportsBtn = new ToolStripButton("Reports") { Name = "Nav_Reports", AccessibleName = "Nav_Reports" };
            reportsBtn.Click += (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false);

            var aiChatBtn = new ToolStripButton("AI Chat") { Name = "Nav_AIChat", AccessibleName = "Nav_AIChat" };
            aiChatBtn.Click += (s, e) => ShowChildForm<ChatWindow, object>(allowMultiple: false);

            var quickBooksBtn = new ToolStripButton("QuickBooks") { Name = "Nav_QuickBooks", AccessibleName = "Nav_QuickBooks" };
            quickBooksBtn.Click += (s, e) => ShowChildForm<QuickBooksForm, object>(allowMultiple: false);

            var settingsBtn = new ToolStripButton("Settings") { Name = "Nav_Settings", AccessibleName = "Nav_Settings" };
            settingsBtn.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false);

            var dockingToggleBtn = new ToolStripButton("Docking") { Name = "Nav_DockingToggle", AccessibleName = "Nav_DockingToggle" };
            dockingToggleBtn.Click += (s, e) => ToggleDocking();

            var mdiToggleBtn = new ToolStripButton("MDI") { Name = "Nav_MdiToggle", AccessibleName = "Nav_MdiToggle" };
            mdiToggleBtn.Click += (s, e) => ToggleMdiMode();

            // Theme toggle removed - session-only theme switching via menu or hotkey only

            // Grid test helpers (navigation strip)
            var navGridApplyFilter = new ToolStripButton("Apply Grid Filter") { Name = "Nav_ApplyGridFilter" };
            navGridApplyFilter.Click += (s, e) => ApplyTestFilterToActiveGrid();

            var navGridClearFilter = new ToolStripButton("Clear Grid Filter") { Name = "Nav_ClearGridFilter" };
            navGridClearFilter.Click += (s, e) => ClearActiveGridFilter();

            var navGridExport = new ToolStripButton("Export Grid") { Name = "Nav_ExportGrid" };
            navGridExport.Click += async (s, e) => await ExportActiveGridToExcel();

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardBtn,
                new ToolStripSeparator(),
                accountsBtn,
                budgetBtn,
                chartsBtn,
                customersBtn,
                reportsBtn,
                aiChatBtn,
                quickBooksBtn,
                new ToolStripSeparator(),
                settingsBtn,
                new ToolStripSeparator(),
                dockingToggleBtn,
                mdiToggleBtn,
                new ToolStripSeparator(),
                navGridApplyFilter,
                navGridClearFilter,
                navGridExport
            });

            Controls.Add(_navigationStrip);
            _logger?.LogDebug("Navigation strip initialized successfully");
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
    /// Toggle docking mode (for UI testing) - DISABLED: docking is const true
    /// </summary>
    private void ToggleDocking()
    {
        // NOTE: ToggleDockingMode() removed - _useSyncfusionDocking is const true
        _logger?.LogWarning("ToggleDocking called but docking mode is const true and cannot be toggled");
        UpdateDockingStateText();
    }

    /// <summary>
    /// Toggle MDI mode (for UI testing) - DISABLED: MDI is const true
    /// </summary>
    private void ToggleMdiMode()
    {
        // NOTE: Cannot toggle because UseMdiMode is read-only (backed by const)
        _logger?.LogWarning("ToggleMdiMode called but MDI mode is const true and cannot be toggled");
        UpdateDockingStateText();
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
                AllowMerge = true,
                Font = new Font("Segoe UI", 9F),
                RenderMode = ToolStripRenderMode.Professional,
                ShowItemToolTips = true,
                AccessibleName = "MainMenuStrip",
                AccessibleDescription = "Main navigation menu bar"
            };

            // Apply professional color scheme with theme colors
            if (_menuStrip.Renderer is ToolStripProfessionalRenderer professionalRenderer)
            {
                professionalRenderer.RoundedEdges = true;
            }

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File")
            {
                Name = "Menu_File",
                ToolTipText = "File operations"
            };

            // File > Exit
            var exitMenuItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close())
            {
                Name = "Menu_File_Exit",
                ShortcutKeys = Keys.Alt | Keys.F4,
                ToolTipText = "Exit the application (Alt+F4)",
                Image = CreateIconFromText("\uE8BB", 16), // Exit icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            fileMenu.DropDownItems.Add(exitMenuItem);

            // View Menu - All child forms accessible here
            var viewMenu = new ToolStripMenuItem("&View")
            {
                Name = "Menu_View",
                ToolTipText = "Open application views"
            };

            // View > Dashboard
            var dashboardMenuItem = new ToolStripMenuItem("&Dashboard", null, (s, e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Dashboard",
                ShortcutKeys = Keys.Control | Keys.D,
                ToolTipText = "Open Dashboard view (Ctrl+D)",
                Image = CreateIconFromText("\uE10F", 16), // Dashboard icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Accounts
            var accountsMenuItem = new ToolStripMenuItem("&Accounts", null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Accounts",
                ShortcutKeys = Keys.Control | Keys.A,
                ToolTipText = "Open Accounts view (Ctrl+A)",
                Image = CreateIconFromText("\uE8F4", 16), // AccountActivity icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Budget Overview
            var budgetMenuItem = new ToolStripMenuItem("&Budget Overview", null, (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Budget",
                ShortcutKeys = Keys.Control | Keys.B,
                ToolTipText = "Open Budget Overview (Ctrl+B)",
                Image = CreateIconFromText("\uE7C8", 16), // Money icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Charts
            var chartsMenuItem = new ToolStripMenuItem("&Charts", null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Charts",
                ShortcutKeys = Keys.Control | Keys.H,
                ToolTipText = "Open Charts view (Ctrl+H)",
                Image = CreateIconFromText("\uE9D2", 16), // BarChart icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > Reports
            var reportsMenuItem = new ToolStripMenuItem("&Reports", null, (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Reports",
                ShortcutKeys = Keys.Control | Keys.R,
                ToolTipText = "Open Reports view (Ctrl+R)",
                Image = CreateIconFromText("\uE8A5", 16), // Document icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // View > AI Chat
            var aiChatMenuItem = new ToolStripMenuItem("AI &Chat", null, (s, e) => ShowChildForm<ChatWindow, object>(allowMultiple: false))
            {
                Name = "Menu_View_AIChat",
                ShortcutKeys = Keys.Control | Keys.I,
                ToolTipText = "Open AI Chat Assistant (Ctrl+I)"
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    aiChatMenuItem.Image = iconService.GetIcon("chat", AppTheme.Office2019Colorful, 16);
                    aiChatMenuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
            catch { /* Icon loading is optional */ }

            // View > QuickBooks
            var quickBooksMenuItem = new ToolStripMenuItem("&QuickBooks", null, (s, e) => ShowChildForm<QuickBooksForm, object>(allowMultiple: false))
            {
                Name = "Menu_View_QuickBooks",
                ShortcutKeys = Keys.Control | Keys.Q,
                ToolTipText = "Open QuickBooks Integration (Ctrl+Q)"
            };
            // Try to set icon from theme service
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(Program.Services);
                if (iconService != null)
                {
                    quickBooksMenuItem.Image = iconService.GetIcon("quickbooks", AppTheme.Office2019Colorful, 16);
                    quickBooksMenuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
            catch { /* Icon loading is optional */ }

            // View > Customers
            var customersMenuItem = new ToolStripMenuItem("C&ustomers", null, (s, e) => ShowChildForm<CustomersForm, CustomersViewModel>(allowMultiple: false))
            {
                Name = "Menu_View_Customers",
                ShortcutKeys = Keys.Control | Keys.U,
                ToolTipText = "Open Customers view (Ctrl+U)",
                Image = CreateIconFromText("\uE716", 16), // Contact icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            // Add separator for visual grouping
            var viewSeparator = new ToolStripSeparator
            {
                Name = "Menu_View_Separator"
            };

            // View > Refresh
            var refreshMenuItem = new ToolStripMenuItem("&Refresh", null, (s, e) =>
            {
                // Refresh active child form if any
                if (ActiveMdiChild is Form activeChild)
                {
                    activeChild.Refresh();
                }
            })
            {
                Name = "Menu_View_Refresh",
                ShortcutKeys = Keys.F5,
                ToolTipText = "Refresh active view (F5)",
                Image = CreateIconFromText("\uE72C", 16), // Refresh icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

            viewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                dashboardMenuItem,
                accountsMenuItem,
                budgetMenuItem,
                chartsMenuItem,
                reportsMenuItem,
                aiChatMenuItem,
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
            var settingsMenuItem = new ToolStripMenuItem("&Settings", null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false))
            {
                Name = "Menu_Tools_Settings",
                ShortcutKeys = Keys.Control | Keys.Oemcomma,
                ToolTipText = "Open Settings (Ctrl+,)",
                Image = CreateIconFromText("\uE713", 16), // Settings icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

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
                ToolTipText = "Open online documentation (F1)",
                Image = CreateIconFromText("\uE897", 16), // Help icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

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
                ToolTipText = "About this application",
                Image = CreateIconFromText("\uE946", 16), // Info icon (Segoe MDL2)
                ImageScaling = ToolStripItemImageScaling.None
            };

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

            _logger?.LogDebug("Enhanced menu bar initialized successfully with icons and theming");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize menu bar");
            _menuStrip = null;
        }
    }

    /// <summary>
    /// Create a Bitmap icon from Segoe MDL2 Assets text (Unicode character).
    /// </summary>
    /// <param name="iconText">Unicode character from Segoe MDL2 Assets font</param>
    /// <param name="size">Icon size in pixels</param>
    /// <returns>Bitmap containing the rendered icon</returns>
    private Bitmap CreateIconFromText(string iconText, int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
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

    /// <summary>
    /// Apply theme colors to menu dropdown items.
    /// </summary>
    /// <param name="menuItem">Parent menu item to theme</param>
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
            // REMOVED: Manual BackColor override - let Syncfusion theme handle colors

            // Apply theme to child items
            foreach (ToolStripItem item in dropdown.Items)
            {
                if (item is ToolStripMenuItem childMenuItem)
                {
                    // REMOVED: Manual BackColor/ForeColor overrides - let Syncfusion theme handle colors

                    // Recursively apply to sub-items
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
    private void ThemeToggleBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            var currentTheme = SfSkinManager.ApplicationVisualTheme;
            var newTheme = currentTheme == "Office2019Dark" ? "Office2019Colorful" : "Office2019Dark";

            // Apply new theme globally
            SfSkinManager.ApplicationVisualTheme = newTheme;

            // SESSION-ONLY: Theme preference does NOT persist to configuration.
            // Resets to default (Office2019Colorful) on next application start.
            _logger?.LogInformation("Theme switched to {NewTheme} (session only - no config persistence)", newTheme);

            // Note: Theme toggle button removed from UI (session-only switching via hotkey if implemented)

            // Refresh all open forms to apply new theme
            foreach (Form form in Application.OpenForms)
            {
                try
                {
                    SfSkinManager.SetVisualStyle(form, newTheme);
                    form.Refresh();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to apply theme to form {FormName}", form.Name);
                }
            }

            _logger?.LogInformation("Theme switched from {OldTheme} to {NewTheme}", currentTheme, newTheme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to toggle theme");
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
    /// Initialize Syncfusion DockingManager (Phase 1 Simplification: always enabled)
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Skip docking initialization in test harness mode to avoid graphics issues
            if (_uiConfig.IsUiTestHarness)
            {
                _logger.LogInformation("Skipping DockingManager initialization in test harness mode");
                return;
            }

            InitializeDockingManager();
            CreateDockingPanels();
            HideStandardPanelsForDocking();
            LoadDockingLayout();
            ApplyThemeToDockingPanels();

            // Subscribe to theme changes for runtime theme updates
            ThemeManager.ThemeChanged += OnThemeChanged;

            stopwatch.Stop();
            _logger.LogInformation("DockingManager initialized successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Syncfusion.Windows.Forms.Tools.DockingManagerException dockEx)
        {
            stopwatch.Stop();
            _logger.LogWarning(dockEx, "DockingManager initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            HandleDockingInitializationError(dockEx, "DockingManagerException during initialization");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "DockingManager initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            HandleDockingInitializationError(ex, "Failed to initialize Syncfusion DockingManager");
        }
    }

    /// <summary>
    /// Handle theme changes at runtime and reapply theme to all docking panels
    /// </summary>
    private void OnThemeChanged(object? sender, Theming.AppTheme theme)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
            return;
        }

        try
        {
            _logger.LogInformation("Applying theme change to docking panels: {Theme}", theme);

            // Reapply theme to all docking panels
            ApplyThemeToDockingPanels();

            // Reapply theme to all dynamic panels
            if (_dynamicDockPanels != null)
            {
                foreach (var panel in _dynamicDockPanels.Values)
                {
                    ApplyPanelTheme(panel);
                }
            }

            // Refresh activity grid with new theme
            if (_activityGrid != null && !_activityGrid.IsDisposed)
            {
                try
                {
                    SfSkinManager.SetVisualStyle(_activityGrid, AppThemeColors.DefaultTheme);
                    // REMOVED: Manual BackColor/TextColor overrides - SfSkinManager handles all theme colors
                    _activityGrid.Refresh();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to apply theme to activity grid");
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
        components ??= new System.ComponentModel.Container();
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

        // Phase 1 Simplification: EnableDocumentMode permanently false (standard MDI, no TabbedMDI)
        _dockingManager.EnableDocumentMode = false;
        _logger.LogInformation("DockingManager document mode disabled (using DockingManager for panels only)");

        _dockingManager.PersistState = true;
        _dockingManager.AnimateAutoHiddenWindow = true;
        _dockingManager.AutoHideTabFont = _dockAutoHideTabFont = new Font(SegoeUiFontName, 9f);
        _dockingManager.DockTabFont = _dockTabFont = new Font(SegoeUiFontName, 9f);
        _dockingManager.ShowCaption = true;

        try
        {
            SfSkinManager.SetVisualStyle(_dockingManager, AppThemeColors.DefaultTheme);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to apply theme to DockingManager");
            // Theme application is non-critical, continue
        }
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
    /// Create left dock panel with dashboard cards (collapsible, auto-hide enabled)
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

        var dashboardContent = CreateDashboardCardsPanel();
        _leftDockPanel.Controls.Add(dashboardContent);

        ConfigurePanelDocking(_leftDockPanel, DockingStyle.Left, 280, "Dashboard");

        _logger.LogDebug("Left dock panel created with dashboard cards");
    }

    /// <summary>
    /// Create central document panel for main content area
    /// </summary>
    private void CreateCentralDocumentPanel()
    {
        if (_dockingManager == null) return;

        _centralDocumentPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            AccessibleName = "CentralDocumentPanel",
            Dock = DockStyle.Fill,
            Visible = true
        };

        ConfigureCentralPanelForMode();
        EnsureCentralPanelVisibility();

        _logger.LogDebug("Central document panel created (standard Fill docking, MDI-compatible)");
    }

    /// <summary>
    /// Configure central panel based on current mode (MDI or standard)
    /// </summary>
    private void ConfigureCentralPanelForMode()
    {
        if (_centralDocumentPanel == null) return;

        // Phase 1 Simplification: MDI always enabled
        var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
        if (mdiClient != null)
        {
            ConfigureMdiClient(mdiClient);
        }
        else
        {
            AddCentralPanelToForm();
        }
    }

    /// <summary>
    /// Configure MDI client as central document area
    /// </summary>
    private void ConfigureMdiClient(MdiClient mdiClient)
    {
        try
        {
            mdiClient.Dock = DockStyle.Fill;
            mdiClient.SendToBack();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure MdiClient as central document area - using central panel fallback");
            AddCentralPanelToForm();
        }
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
            SfSkinManager.SetVisualStyle(_centralDocumentPanel, AppThemeColors.DefaultTheme);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to apply theme to central panel - using defaults");
            // Theme application is non-critical
        }

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
            Padding = new Padding(8, 8, 8, 8),
            BorderStyle = BorderStyle.None
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

        // Phase 1 Simplification: MDI always enabled for docked panels
        _dockingManager.SetAsMDIChild(panel, true);
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
        SetupCardClickHandler(accountsCard, () => ShowChildForm<AccountsForm, AccountsViewModel>(allowMultiple: false));

        var chartsCard = CreateDashboardCard("Charts", "Analytics Ready").Panel;
        SetupCardClickHandler(chartsCard, () => ShowChildForm<ChartForm, ChartViewModel>(allowMultiple: false));

        var settingsCard = CreateDashboardCard("Settings", "System Config").Panel;
        SetupCardClickHandler(settingsCard, () => ShowChildForm<SettingsForm, SettingsViewModel>(allowMultiple: false));

        var reportsCard = CreateDashboardCard("Reports", "Generate Now").Panel;
        SetupCardClickHandler(reportsCard, () => ShowChildForm<ReportsForm, ReportsViewModel>(allowMultiple: false));

        var infoCard = CreateDashboardCard("Budget Status", MainFormResources.LoadingText).Panel;

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
            Padding = new Padding(10)
        };

        var activityHeader = new Label
        {
            Text = "Recent Activity",
            Font = new Font(SegoeUiFontName, 12, FontStyle.Bold),
            // REMOVED: ForeColor - SfSkinManager theme cascade handles label colors
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(5, 8, 0, 0)
        };

        _activityGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Name = "ActivityDataGrid",
            AccessibleName = "ActivityDataGrid",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            ShowGroupDropArea = false,
            RowHeight = 36,
            AllowSorting = true,
            AllowFiltering = true
        };
        SfSkinManager.SetVisualStyle(_activityGrid, AppThemeColors.DefaultTheme);

        // Map to ActivityLog properties with flexible column sizing
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridDateTimeColumn { MappingName = "Timestamp", HeaderText = "Time", Format = "HH:mm", Width = 70, MinimumWidth = 60 });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Action", Width = 100, MinimumWidth = 80, AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details", Width = 0, AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "User", HeaderText = "User", Width = 80, MinimumWidth = 60 });

        // Load initial data from database
        _ = LoadActivityDataAsync();

        // Setup auto-refresh timer (every 30 seconds)
        _activityRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000 // 30 seconds
        };
        _activityRefreshTimer.Tick += async (s, e) => await LoadActivityDataAsync();
        _activityRefreshTimer.Start();

        activityPanel.Controls.Add(_activityGrid);
        activityPanel.Controls.Add(activityHeader);

        return activityPanel;
    }

    /// <summary>
    /// Load activity data from database asynchronously.
    /// </summary>
    private async Task LoadActivityDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var activityLogRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IActivityLogRepository>(scope.ServiceProvider);
            if (activityLogRepository == null)
            {
                _logger.LogWarning("ActivityLogRepository not available, using fallback data");
                LoadFallbackActivityData();
                return;
            }

            var activities = await activityLogRepository.GetRecentActivitiesAsync(skip: 0, take: 50);

            if (_activityGrid != null && !_activityGrid.IsDisposed)
            {
                if (_activityGrid.InvokeRequired)
                {
                    _activityGrid.Invoke(() => _activityGrid.DataSource = activities);
                }
                else
                {
                    _activityGrid.DataSource = activities;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load activity data");
            LoadFallbackActivityData();
        }
    }

    private void LoadFallbackActivityData()
    {
        if (_activityGrid == null || _activityGrid.IsDisposed)
            return;

        var activities = new[]
        {
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-5), Activity = "Account Updated", Details = "GL-1001", User = "System" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-15), Activity = "Report Generated", Details = "Budget Q4", User = "Scheduler" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-30), Activity = "QuickBooks Sync", Details = "42 records", User = "Integrator" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddHours(-1), Activity = "User Login", Details = "Admin", User = "Admin" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddHours(-2), Activity = "Backup Complete", Details = "12.5 MB", User = "System" }
        };

        if (_activityGrid.InvokeRequired)
        {
            _activityGrid.Invoke(() => _activityGrid.DataSource = activities);
        }
        else
        {
            _activityGrid.DataSource = activities;
        }
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

    /// <summary>
    /// Show standard panels if docking initialization fails
    /// </summary>
    private void ShowStandardPanelsAfterDockingFailure()
    {
        foreach (Control control in Controls)
        {
            if (control is SplitContainer)
            {
                try
                {
                    control.Visible = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to show standard panel {ControlName} after docking failure", control.Name);
                    // Non-critical - continue with other panels
                }
            }
        }
        _logger.LogDebug("Standard panels restored after docking failure");
    }

    // Phase 1 Simplification: ToggleDockingMode removed - docking permanently enabled

    /// <summary>
    /// Ensure docking panels and manager are correctly ordered in the Z axis (Phase 1: always enabled)
    /// </summary>
    private void EnsureDockingZOrder()
    {
        try
        {
            // Phase 1 Simplification: Docking always enabled
            if (_dockingManager == null) return;

            foreach (var panel in new Control?[] { _leftDockPanel, _centralDocumentPanel, _rightDockPanel })
            {
                if (panel != null && !panel.IsDisposed)
                {
                    try { panel.BringToFront(); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to BringToFront on dynamic panel during EnsureDockingZOrder"); }
                }
            }

            try { if (_dockingManager.HostControl is Control host) { host.BringToFront(); } } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to BringToFront on DockingManager host control during EnsureDockingZOrder"); }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to ensure docking z-order");
        }
    }

    /// <summary>
    /// Load saved docking layout from AppData
    /// </summary>
    private void LoadDockingLayout()
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

            LoadAndApplyDockingLayout(layoutPath);
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
            _logger.LogDebug("LoadDockingLayout START - ThreadId={ThreadId}, InvokeRequired={InvokeRequired}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime}",
                threadId, this.InvokeRequired, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
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
            var xmlDoc = new System.Xml.XmlDocument();
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
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.Load(layoutPath);
            _logger.LogDebug("Layout XML validated successfully");
            return true;
        }
        catch (System.Xml.XmlException xmlEx)
        {
            HandleCorruptLayoutFile(layoutPath, xmlEx);
            return false;
        }
    }

    /// <summary>
    /// Handle corrupt layout file
    /// </summary>
    private void HandleCorruptLayoutFile(string layoutPath, System.Xml.XmlException xmlEx)
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
    /// Load and apply docking layout from file with performance monitoring and timeout
    /// </summary>
    private void LoadAndApplyDockingLayout(string layoutPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var dynamicPanelInfos = LoadDynamicPanelMetadata(layoutPath);
            RecreateDynamicPanels(dynamicPanelInfos);

            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, layoutPath);

            try
            {
                LogDockStateLoad(layoutPath);

                // Use Task.Run with timeout to detect slow/hung layout loads
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
                });

                if (!loadTask.Wait(LayoutLoadTimeoutMs))
                {
                    stopwatch.Stop();
                    _logger.LogError("Layout load exceeded timeout of {TimeoutMs}ms - layout is too complex or corrupted. Auto-resetting to defaults.", LayoutLoadTimeoutMs);
                    HandleSlowLayoutLoad(layoutPath, stopwatch.ElapsedMilliseconds);
                    return;
                }

                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                if (elapsedMs > LayoutLoadWarningMs)
                {
                    _logger.LogWarning("Layout load took {ElapsedMs}ms (threshold: {ThresholdMs}ms) - consider simplifying layout", elapsedMs, LayoutLoadWarningMs);
                }
                else
                {
                    _logger.LogInformation("Docking layout loaded from {Path} in {ElapsedMs}ms", layoutPath, elapsedMs);
                }
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
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during layout load - resetting to defaults");
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
        _logger.LogInformation("Calling _dockingManager.LoadDockState - ThreadId={ThreadId}, layoutPath={Path}, InvokeRequired={InvokeRequired}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
            System.Threading.Thread.CurrentThread.ManagedThreadId, layoutPath, this.InvokeRequired, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
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
                // REMOVED: BackColor, ForeColor - SfSkinManager theme cascade handles panel colors
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
            _dynamicDockPanels[panelInfo.Name] = panel;

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
    /// Inject layout version into saved XML file
    /// </summary>
    private void InjectLayoutVersion(string layoutPath)
    {
        try
        {
            if (!File.Exists(layoutPath))
            {
                return;
            }

            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.Load(layoutPath);

            // Add version attribute to root element
            if (xmlDoc.DocumentElement != null)
            {
                xmlDoc.DocumentElement.SetAttribute(LayoutVersionAttributeName, CurrentLayoutVersion);
                xmlDoc.Save(layoutPath);
                _logger.LogDebug("Injected layout version {Version} into {Path}", CurrentLayoutVersion, layoutPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inject layout version into {Path}", layoutPath);
            // Non-critical - version will be missing but load will still work (older version)
        }
    }

    private void ReplaceDockingLayoutFile(string tempPath, string finalPath)
    {
        if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(finalPath))
        {
            return;
        }

        if (!File.Exists(tempPath))
        {
            TryCleanupTempFile(tempPath);
            return;
        }

        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath, true);

            // Inject layout version after successful save
            InjectLayoutVersion(finalPath);
        }
        catch (Exception ex)
        {
            // File operation failed - log for diagnostics but don't throw
            System.Diagnostics.Debug.WriteLine($"Failed to replace layout file: {ex.Message}");
        }
        finally
        {
            TryCleanupTempFile(tempPath);
        }
    }

    private static void TryCleanupTempFile(string tempPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            // Swallow - temp file cleanup is best-effort
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup temp file: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply Syncfusion theme to docked panels using SkinManager (single authority).
    /// </summary>
    private void ApplyThemeToDockingPanels()
    {
        try
        {
            if (_dockingManager != null)
            {
                try
                {
                    SfSkinManager.SetVisualStyle(_dockingManager, AppThemeColors.DefaultTheme);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DockingManager theme apply fallback");
                    // Theme application failure is non-critical
                }
            }

            ApplyPanelTheme(_leftDockPanel);
            ApplyPanelTheme(_rightDockPanel);
            ApplyPanelTheme(_centralDocumentPanel);

            _logger.LogInformation("Applied SkinManager theme to docking panels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme to docking panels - using default colors");
            // Theme application failure is non-critical - defaults will be used
        }
    }

    private static void ApplyPanelTheme(Control? panel)
    {
        if (panel == null) return;

        try
        {
            SfSkinManager.SetVisualStyle(panel, AppThemeColors.DefaultTheme);
        }
        catch (Exception ex)
        {
            // Theme application failed - SfSkinManager will use defaults
            System.Diagnostics.Debug.WriteLine($"Panel theme application failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper: invoke an action on the UI thread and return a Task that completes when the action finishes.
    /// This provides an awaitable wrapper over BeginInvoke with a threadpool fallback.
    /// </summary>
    private Task SafeInvokeAsync(System.Action action)
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

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        // Log docking state changes
        _logger.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Ensure central panels remain visible after state changes
        EnsureCentralPanelVisibility();

        // Auto-save layout on state changes with debouncing to prevent I/O spam
        if (_uiConfig.UseSyncfusionDocking)
        {
            DebouncedSaveDockingLayout();
        }
    }

    /// <summary>
    /// Debounced save mechanism - waits 1500ms after last state change before saving
    /// Prevents I/O spam during rapid docking operations (e.g., dragging, resizing)
    /// Enforces minimum 2-second interval between saves and prevents concurrent saves
    /// </summary>
    private void DebouncedSaveDockingLayout()
    {
        try
        {
            _logger.LogDebug("DebouncedSaveDockingLayout invoked - ThreadId={ThreadId}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
                System.Threading.Thread.CurrentThread.ManagedThreadId, _isSavingLayout, _lastSaveTime);
        }
        catch (Exception ex)
        {
            // Logging failure is non-critical
            System.Diagnostics.Debug.WriteLine($"Failed to log debounce info: {ex.Message}");
        }

        if (_isSavingLayout)
        {
            _logger.LogDebug("Skipping debounced save - save already in progress");
            return;
        }

        var timeSinceLastSave = DateTime.Now - _lastSaveTime;
        if (timeSinceLastSave < MinimumSaveInterval)
        {
            _logger.LogDebug("Skipping debounced save - too soon since last save ({Elapsed}ms ago)",
                timeSinceLastSave.TotalMilliseconds);
            return;
        }

        _dockingLayoutSaveTimer?.Stop();

        if (_dockingLayoutSaveTimer == null)
        {
            _dockingLayoutSaveTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _dockingLayoutSaveTimer.Tick += OnSaveTimerTick;
        }

        _dockingLayoutSaveTimer.Start();
    }

    /// <summary>
    /// Timer tick handler - performs actual save after debounce period
    /// </summary>
    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _dockingLayoutSaveTimer?.Stop();

        if (_isSavingLayout)
        {
            _logger.LogDebug("Skipping timer save - save already in progress");
            return;
        }

        try
        {
            _logger.LogDebug("OnSaveTimerTick - performing debounced save (ThreadId={ThreadId})", System.Threading.Thread.CurrentThread.ManagedThreadId);
            SaveDockingLayout();
            _lastSaveTime = DateTime.Now;
            _logger.LogDebug("Debounced auto-save completed - Time={Time}", _lastSaveTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-save docking layout after debounce period");
            // Auto-save failure is non-critical - manual save still available
        }
    }

    /// <summary>
    /// Save the current docking layout to disk.
    /// Thread-safe and idempotent; marshals to UI thread if called from background threads.
    /// Uses a temp-file replace strategy to avoid partial writes.
    /// </summary>
    private void SaveDockingLayout()
    {
        // Marshal to UI thread if necessary
        if (InvokeRequired)
        {
            try { BeginInvoke(new System.Action(SaveDockingLayout)); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to schedule SaveDockingLayout via BeginInvoke"); }
            return;
        }

        // Concurrency guard
        lock (_dockingSaveLock)
        {
            if (_isSavingLayout)
            {
                _logger.LogDebug("SaveDockingLayout skipped - save already in progress");
                return;
            }
            _isSavingLayout = true;
        }

        try
        {
            if (_dockingManager == null)
            {
                _logger.LogDebug("SaveDockingLayout skipped - no docking manager present");
                return;
            }

            var layoutPath = GetDockingLayoutPath();
            var tempPath = layoutPath + ".tmp";

            try
            {
                // Attempt to use Syncfusion API to save dock state via AppStateSerializer
                var serializerType = typeof(Syncfusion.Runtime.Serialization.AppStateSerializer);
                var serializer = Activator.CreateInstance(serializerType, new object[] { Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, tempPath })!;
                var saveMethod = _dockingManager.GetType().GetMethod("SaveDockState", new Type[] { serializerType });

                if (saveMethod != null)
                {
                    saveMethod.Invoke(_dockingManager, new object[] { serializer });
                }
                else
                {
                    // Fallback: write minimal XML so LoadDockingLayout has something to parse
                    File.WriteAllText(tempPath, "<DockingLayout xmlns=\"http://schemas.syncfusion.com/docking\" />");
                }

                ReplaceDockingLayoutFile(tempPath, layoutPath);
            }
            catch (ArgumentException argEx)
            {
                // Known failure mode in tests - delete corrupt layout and continue
                _logger.LogWarning(argEx, "Failed to save docking layout due to argument exception; clearing layout file");
                TryDeleteLayoutFiles(layoutPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save docking layout");
            }
        }
        finally
        {
            lock (_dockingSaveLock) { _isSavingLayout = false; }
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        // Log visibility changes
        _logger.LogDebug("Dock visibility changed");

        // Ensure central panels remain visible after visibility changes
        EnsureCentralPanelVisibility();
    }

    /// <summary>
    /// Ensures proper visibility and z-order of central panels (Phase 1: always docked)
    /// </summary>
    private void EnsureCentralPanelVisibility()
    {
        // Phase 1 Simplification: Docking always enabled
        try
        {
            EnsureCentralPanelVisible();
            EnsureMdiClientVisible();
            EnsureSidePanelsZOrder();
            RefreshFormLayout();

            _logger.LogDebug("Central panel visibility ensured for docked layout");
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
    /// Ensure MDI client is visible (Phase 1: MDI always enabled)
    /// </summary>
    private void EnsureMdiClientVisible()
    {
        // Phase 1 Simplification: MDI always enabled
        var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
        if (mdiClient == null) return;

        try
        {
            mdiClient.Visible = true;
            mdiClient.SendToBack();
            mdiClient.Invalidate();

            // NOTE: RefreshTabbedMdiManager() removed - TabbedMDI permanently disabled
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set MDI client visibility");
            // MDI client visibility failure is non-critical
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

        // Phase 1 Simplification: MDI always enabled
        if (_centralDocumentPanel != null)
        {
            try
            {
                _dockingManager.SetAsMDIChild(_centralDocumentPanel, true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to reassert MDI child status for central panel");
                // MDI child status reassertion failure is non-critical
            }
        }

        this.Invalidate();
    }

    // Phase 1 Simplification: EnsureNonDockingVisibility removed - docking always enabled

    #endregion

    #region Dynamic Panel Management

    /// <summary>
    /// Add a custom panel to the docking manager at runtime
    /// Enables plugin architecture and dynamic content areas
    /// </summary>
    /// <param name="panelName">Unique identifier for the panel</param>
    /// <param name="displayLabel">User-facing label for the dock tab</param>
    /// <param name="content">Control to host in the panel</param>
    /// <param name="dockStyle">Docking position (Left, Right, Top, Bottom)</param>
    /// <param name="width">Panel width (for Left/Right docking)</param>
    /// <param name="height">Panel height (for Top/Bottom docking)</param>
    /// <returns>True if panel was added successfully, false if panel already exists or docking is disabled</returns>
    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        // Phase 1 Simplification: Docking always enabled
        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot add dynamic dock panel - DockingManager not initialized");
            return false;
        }

        if (string.IsNullOrWhiteSpace(panelName))
        {
            throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        }

        if (_dynamicDockPanels != null && _dynamicDockPanels.ContainsKey(panelName))
        {
            _logger.LogWarning("Dynamic dock panel '{PanelName}' already exists", panelName);
            return false;
        }

        Panel? panel = null;
        try
        {
            panel = new Panel
            {
                Name = panelName,
                Padding = new Padding(5)
            };

            // Add content to panel
            if (content != null)
            {
                content.Dock = DockStyle.Fill;
                panel.Controls.Add(content);
            }

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
            TrySetFloatingMode(panel, true);

            // Apply theme
            ApplyPanelTheme(panel);

            // Track panel
            _dynamicDockPanels ??= new Dictionary<string, Panel>();
            _dynamicDockPanels[panelName] = panel;
            panel = null; // ownership transferred to DockingManager/dictionary

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
    /// Remove a dynamically added panel from the docking manager
    /// </summary>
    /// <param name="panelName">Name of the panel to remove</param>
    /// <returns>True if panel was removed, false if panel doesn't exist</returns>
    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (_dynamicDockPanels == null || !_dynamicDockPanels.ContainsKey(panelName))
        {
            _logger.LogWarning("Cannot remove dynamic dock panel '{PanelName}' - not found", panelName);
            return false;
        }

        try
        {
            var panel = _dynamicDockPanels[panelName];

            // Undock and dispose
            if (_dockingManager != null)
            {
                _dockingManager.SetEnableDocking(panel, false);
            }

            panel.Dispose();
            _dynamicDockPanels.Remove(panelName);

            _logger.LogInformation("Removed dynamic dock panel '{PanelName}'", panelName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
    }

    /// <summary>
    /// Get a dynamically added panel by name
    /// </summary>
    /// <param name="panelName">Name of the panel to retrieve</param>
    /// <returns>Panel if found, null otherwise</returns>
    public Panel? GetDynamicDockPanel(string panelName)
    {
        if (_dynamicDockPanels == null || !_dynamicDockPanels.ContainsKey(panelName))
        {
            return null;
        }

        return _dynamicDockPanels[panelName];
    }

    /// <summary>
    /// Get all dynamically added panel names
    /// </summary>
    /// <returns>Collection of panel names</returns>
    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return _dynamicDockPanels?.Keys.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
    }

    #endregion

    /// <summary>
    /// Helper method to set floating mode with error handling
    /// </summary>
    private void TrySetFloatingMode(Panel panel, bool allowFloating)
    {
        try
        {
            _dockingManager?.SetAllowFloating(panel, allowFloating);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set floating mode for panel '{PanelName}'", panel.Name);
        }
    }




    /// <summary>
    /// Dispose resources owned by the docking implementation
    /// Extracted from the original Dispose override to avoid duplicate overrides
    /// and be callable from the single Dispose override in the main partial.
    ///
    /// NOTE: To avoid races with concurrent Save operations and to ensure
    /// fast, predictable teardown (important for unit tests), this method
    /// clears the `_dockingManager` field immediately and schedules the
    /// actual disposal work asynchronously (UI-thread or threadpool).
    /// This prevents double-dispose from a container and avoids Syncfusion
    /// NullReferenceExceptions during concurrent teardown.
    /// </summary>
    private void DisposeSyncfusionDockingResources()
    {
        _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - _dockingManager present: {HasDockingManager}, _isSavingLayout={IsSaving}", _dockingManager != null, _isSavingLayout);
        _logger?.LogDebug("Dispose invoked - hasManager={HasDockingManager}, isSavingLayout={IsSaving}", _dockingManager != null, _isSavingLayout);

        // Unsubscribe from theme changes to prevent memory leaks
        try
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to unsubscribe from ThemeChanged event");
        }

        // Best-effort: schedule a non-blocking save to persist layout without blocking disposal
        try
        {
            if (this.IsHandleCreated)
            {
                    try
                    {
                        _ = SafeInvokeAsync(() =>
                        {
                            try { SaveDockingLayout(); }
                            catch (Exception ex2) { _logger?.LogDebug(ex2, "Failed to SaveDockingLayout in scheduled invoke"); }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "BeginInvoke scheduling SaveDockingLayout failed - invoking directly as fallback");
                        try { SaveDockingLayout(); } catch (Exception ex2) { _logger?.LogDebug(ex2, "Direct SaveDockingLayout fallback failed"); }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Unexpected error while attempting to schedule SaveDockingLayout");
            }
            var mgr = _dockingManager;
            _dockingManager = null;

            if (mgr == null)
            {
                _logger?.LogDebug("DockingManager was already null during dispose");
                return;
            }

            try
            {
                // Attempt quick detach operations synchronously so owner won't double-dispose
                try { mgr.PersistState = false; } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to set PersistState=false on DockingManager during dispose"); }
                try { mgr.HostControl = null; } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to clear HostControl on DockingManager during dispose"); }

                try
                {
                    var owner = mgr.Site?.Container;
                    if (owner != null)
                    {
                        try { owner.Remove(mgr); } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to remove DockingManager from container owner during dispose"); }
                    }
                }
                catch (Exception ex) { _logger!.LogDebug(ex, "Failed during owner detach attempt for docking manager"); }
            }
            catch { }

            // Schedule the potentially heavy disposal asynchronously on the UI thread (best-effort)
            try
            {
                if (this.IsHandleCreated)
                {
                    try
                    {
                        this.BeginInvoke(new System.Action(() =>
                        {
                            try
                            {
                                try { mgr.DockStateChanged -= DockingManager_DockStateChanged; } catch { }
                                try { mgr.DockControlActivated -= DockingManager_DockControlActivated; } catch { }
                                try { mgr.DockVisibilityChanged -= DockingManager_DockVisibilityChanged; } catch { }

                                try { mgr.Dispose(); } catch (Exception ex) { _logger!.LogWarning(ex, "Exception while disposing DockingManager (async)"); }
                            }
                            catch { }
                        }));
                    }
                    catch
                    {
                        // BeginInvoke may throw; fall back to threadpool
                        Task.Run(() =>
                        {
                            try
                            {
                                try { mgr.DockStateChanged -= DockingManager_DockStateChanged; } catch { }
                                try { mgr.DockControlActivated -= DockingManager_DockControlActivated; } catch { }
                                try { mgr.DockVisibilityChanged -= DockingManager_DockVisibilityChanged; } catch { }

                                try { mgr.Dispose(); } catch (Exception ex) { _logger!.LogWarning(ex, "Exception while disposing DockingManager (fallback)"); }
                            }
                            catch { }
                        });
                    }
                }
                else
                {
                    // No handle - run dispose on threadpool
                    Task.Run(() =>
                    {
                        try
                        {
                            try { mgr.DockStateChanged -= DockingManager_DockStateChanged; } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to detach DockStateChanged during threadpool dispose"); }
                            try { mgr.DockControlActivated -= DockingManager_DockControlActivated; } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to detach DockControlActivated during threadpool dispose"); }
                            try { mgr.DockVisibilityChanged -= DockingManager_DockVisibilityChanged; } catch (Exception ex) { _logger!.LogDebug(ex, "Failed to detach DockVisibilityChanged during threadpool dispose"); }

                            try { mgr.Dispose(); } catch (Exception ex) { _logger!.LogWarning(ex, "Exception while disposing DockingManager (threadpool)"); }
                        }
                        catch (Exception ex) { _logger!.LogDebug(ex, "Unexpected error during threadpool docking disposal (outer)"); }
                    });
                }
            }
            catch { }

            _logger?.LogDebug("_dockingManager cleared and disposal scheduled");

        // Dispose debounce timer
        if (_dockingLayoutSaveTimer != null)
        {
            try
            {
                _dockingLayoutSaveTimer.Stop();
                _dockingLayoutSaveTimer.Tick -= OnSaveTimerTick;
                _dockingLayoutSaveTimer.Dispose();
                _dockingLayoutSaveTimer = null;
            }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to dispose docking layout save timer"); }
        }

        // Dispose dynamic panels
        if (_dynamicDockPanels != null)
        {
            foreach (var panel in _dynamicDockPanels.Values)
            {
                try
                {
                    panel.Dispose();
                }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to dispose dynamic dock panel"); }
            }
            _dynamicDockPanels.Clear();
            _dynamicDockPanels = null;
        }

        _leftDockPanel?.Dispose();
        _leftDockPanel = null;

        _rightDockPanel?.Dispose();
        _rightDockPanel = null;

        _centralDocumentPanel?.Dispose();
        _centralDocumentPanel = null;

        // Dispose fonts used by DockingManager
        try
        {
            _dockAutoHideTabFont?.Dispose();
            _dockAutoHideTabFont = null;
        }
        catch { }

        try
        {
            _dockTabFont?.Dispose();
            _dockTabFont = null;
        }
        catch { }
    }



    // Helper methods for dashboard cards (referenced by CreateDashboardCardsPanel)
    private (Panel Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
            // REMOVED: ForeColor - SfSkinManager theme cascade handles label colors
            Height = 28
        };

        var descriptionLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            Font = new Font(SegoeUiFontName, 10, FontStyle.Regular)
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);

        // Apply professional shadow effect
        ProfessionalUI.ApplyCardShadow(panel);

        return (panel, descriptionLabel);
    }



    private void SetupCardClickHandler(Control card, System.Action onClick)
    {
        void Wire(Control control)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => onClick();
            foreach (Control child in control.Controls)
            {
                Wire(child);
            }
        }

        Wire(card);
    }

    private void ShowChildForm<TForm, TViewModel>(bool allowMultiple = false)
        where TForm : Form
        where TViewModel : class
    {
        try
        {
            // Marshal to UI thread if invoked from background threads
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowChildForm<TForm, TViewModel>(allowMultiple)));
                return;
            }

            if (_uiConfig.UseMdiMode)
            {
                ShowChildFormMdi<TForm, TViewModel>(allowMultiple);
            }
            else
            {
                ShowChildFormNonMdi<TForm, TViewModel>(allowMultiple);
            }
            UpdateDockingStateText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open child form {Form}", typeof(TForm).Name);
            if (_statusTextPanel != null)
            {
                _statusTextPanel.Text = $"Unable to open {typeof(TForm).Name}";
            }
        }
    }

    private void UpdateDockingStateText()
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(UpdateDockingStateText));
                return;
            }

            if (_statePanel == null)
                return;

            var stateInfo = new System.Text.StringBuilder();

            if (_uiConfig.UseMdiMode)
            {
                var childCount = MdiChildren?.Length ?? 0;
                stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"MDI: {childCount} window{(childCount != 1 ? "s" : "")}");
            }
            else
            {
                var childCount = _activeMdiChildren.Count;
                stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Windows: {childCount} window{(childCount != 1 ? "s" : "")}");
            }

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    #endregion

    #region MDI

    // Phase 1 Simplification: ShowNonMdiChildForm removed - MDI mode permanently enabled

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool UseMdiMode => _uiConfig.UseMdiMode;

    private void InitializeMdiSupport()
    {
        try
        {
            if (_uiConfig.UseMdiMode)
            {
                _logger.LogInformation("Initializing standard MDI container mode");
                ApplyMdiMode();
            }
            else
            {
                _logger.LogInformation("MDI mode disabled in configuration - skipping MDI initialization");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize MDI support. Note: _useMdiMode and _useTabbedMdi are const and cannot be changed.");

            // NOTE: Cannot actually fall back to modal dialogs because _useMdiMode is const true.
            // User-friendly error: Show message and let the error propagate
            try
            {
                MessageBox.Show(
                    "MDI initialization failed. Please check the error log for details.",
                    "MDI Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception msgEx)
            {
                _logger.LogError(msgEx, "Failed to show MDI error message");
            }

            // Re-throw since we can't actually fall back
            throw;
        }
    }

    private void ApplyMdiMode()
    {
        // Phase 1 Simplification: Standard MDI only
        if (!IsMdiContainer)
        {
            _logger.LogWarning("ApplyMdiMode called but IsMdiContainer is false. MDI mode may not be properly configured.");
            return;
        }

        // Customize the MDI client area background color (theme-aware)
        SetMdiClientBackColor(SystemColors.Control);  // Standard system background

        // Add Window menu for MDI management
        AddMdiWindowMenu();

        _logger.LogInformation("Standard MDI container mode enabled");
    }

    private void SetMdiClientBackColor(Color color)
    {
        // Note: MDI client BackColor is now managed by SfSkinManager theme cascade
        // This method retained for compatibility but performs no operation
        _logger.LogDebug("SetMdiClientBackColor called - theme managed by SfSkinManager");
    }

    private void AddMdiWindowMenu()
    {
        // Find the main MenuStrip
        var menuStrip = Controls.OfType<MenuStrip>().FirstOrDefault();
        if (menuStrip == null)
        {
            // try the MainForm field
            menuStrip = _menuStrip;
        }
        if (menuStrip == null)
        {
            // create a hidden MenuStrip for MDI window list
            try
            {
                menuStrip = new MenuStrip { Name = "MainMenuStrip", Dock = DockStyle.Top, Visible = false, AllowMerge = true };
                Controls.Add(menuStrip);
                this.MainMenuStrip = menuStrip;
                _logger.LogInformation("Created hidden MenuStrip for MDI support");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MenuStrip not found and creation failed; cannot add Window menu");
                return;
            }
        }

        // Check if Window menu already exists
        var existingWindowMenu = menuStrip.Items.OfType<ToolStripMenuItem>()
            .FirstOrDefault(item => item.Text == "&Window");

        if (existingWindowMenu != null)
        {
            _logger.LogDebug("Window menu already exists, updating it");
            menuStrip.Items.Remove(existingWindowMenu);
        }

        // Create Window menu
        var windowMenu = new ToolStripMenuItem("&Window");

        // Add window arrangement commands
        var cascadeMenuItem = new ToolStripMenuItem("&Cascade", null, (s, e) => LayoutMdi(MdiLayout.Cascade))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.C,
            ToolTipText = "Arrange windows in a cascade"
        };

        var tileHorizontalMenuItem = new ToolStripMenuItem("Tile &Horizontal", null, (s, e) => LayoutMdi(MdiLayout.TileHorizontal))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.H,
            ToolTipText = "Arrange windows in horizontal tiles"
        };

        var tileVerticalMenuItem = new ToolStripMenuItem("Tile &Vertical", null, (s, e) => LayoutMdi(MdiLayout.TileVertical))
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.V,
            ToolTipText = "Arrange windows in vertical tiles"
        };

        var arrangeIconsMenuItem = new ToolStripMenuItem("Arrange &Icons", null, (s, e) => LayoutMdi(MdiLayout.ArrangeIcons))
        {
            ToolTipText = "Arrange minimized window icons"
        };

        var closeAllMenuItem = new ToolStripMenuItem("Close &All", null, (s, e) => CloseAllMdiChildren())
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.W,
            ToolTipText = "Close all open windows"
        };

        // Add separator
        var separator = new ToolStripSeparator();

        // Add items to Window menu
        windowMenu.DropDownItems.Add(cascadeMenuItem);
        windowMenu.DropDownItems.Add(tileHorizontalMenuItem);
        windowMenu.DropDownItems.Add(tileVerticalMenuItem);
        windowMenu.DropDownItems.Add(arrangeIconsMenuItem);
        windowMenu.DropDownItems.Add(separator);
        windowMenu.DropDownItems.Add(closeAllMenuItem);
        windowMenu.DropDownItems.Add(new ToolStripSeparator());

        // Configure automatic MDI window list
        // The MenuStrip will automatically add a list of open MDI child windows
        menuStrip.MdiWindowListItem = windowMenu;

        // Insert Window menu before Help menu (if exists) or at the end
        var helpMenuIndex = -1;
        for (int i = 0; i < menuStrip.Items.Count; i++)
        {
            var menuItem = menuStrip.Items[i] as ToolStripMenuItem;
            if (menuItem?.Text != null && menuItem.Text.Contains("Help", StringComparison.OrdinalIgnoreCase))
            {
                helpMenuIndex = i;
                break;
            }
        }

        if (helpMenuIndex >= 0)
        {
            menuStrip.Items.Insert(helpMenuIndex, windowMenu);
        }
        else
        {
            menuStrip.Items.Add(windowMenu);
        }

        _logger.LogInformation("Added Window menu with MDI management commands");
    }

    private void CloseAllMdiChildren()
    {
        try
        {
            _logger.LogInformation("Closing all MDI child forms");

            // Create a copy of the array since closing forms modifies the collection
            var children = MdiChildren.ToArray();

            foreach (var child in children)
            {
                try
                {
                    child.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to close MDI child form {FormType}", child.GetType().Name);
                }
            }

            // Clear tracking dictionary
            _activeMdiChildren.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close all MDI children");
        }
    }

    private void ShowChildFormMdi<TForm, TViewModel>(bool allowMultiple = false)
        where TForm : Form
        where TViewModel : class
    {
        try
        {
            _logger.LogInformation("Showing child form {FormType} (MDI mode: {MdiMode}, AllowMultiple: {AllowMultiple})",
                typeof(TForm).Name, _uiConfig.UseMdiMode, allowMultiple);

            if (!IsMdiContainer)
            {
                IsMdiContainer = true;
            }

            // In MDI mode, check if we should reuse an existing window
            if (!allowMultiple && _activeMdiChildren.TryGetValue(typeof(TForm), out var existingForm))
            {
                if (existingForm != null && !existingForm.IsDisposed)
                {
                    try
                    {
                        existingForm.BringToFront();
                        existingForm.Activate();
                    }
                    catch { }
                    _logger.LogDebug("Activated existing MDI child {FormType}", typeof(TForm).Name);
                    return;
                }

                _activeMdiChildren.Remove(typeof(TForm));
            }

            // Create a new scope to get fresh DbContext + ViewModels for each child window
            var scope = _serviceProvider.CreateScope();
            var form = CreateFormInstance<TForm, TViewModel>(scope);

            // Apply current theme to child form
            try
            {
                AppThemeColors.ApplyTheme(form);
                _logger.LogDebug("Theme applied to child form {FormType}", typeof(TForm).Name);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply theme to child form {FormType}", typeof(TForm).Name);
            }

            // Ensure DockingManager is configured for standard MDI (Phase 1 Simplification)
            RegisterMdiChildWithDocking(form);

            try
            {
                // Phase 1 Simplification: Standard MDI pattern (no TabbedMDI)
                // Use form.MdiParent = this; form.Show() for standard MDI containers

                if (!form.IsMdiChild)
                {
                    form.MdiParent = this;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set MdiParent for {FormType}, attempting recovery", typeof(TForm).Name);
                // Recovery: ensure form is at least shown as owned window
                try
                {
                    form.Owner = this;
                }
                catch (Exception recoverEx)
                {
                    _logger.LogError(recoverEx, "Failed to recover from MdiParent assignment failure for {FormType}", typeof(TForm).Name);
                }
            }

            // Handle form closing to clean up scope and tracking
            form.FormClosed += (s, e) =>
            {
                try
                {
                    _activeMdiChildren.Remove(typeof(TForm));
                    scope.Dispose();
                    _logger.LogDebug("MDI child {FormType} closed and cleaned up", typeof(TForm).Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up MDI child {FormType}", typeof(TForm).Name);
                }
            };

            // Track the form
            if (!allowMultiple)
            {
                _activeMdiChildren[typeof(TForm)] = form;
            }

            // Show the form (non-modal, as MDI child)
            form.Show();

            _logger.LogInformation("MDI child form {FormType} shown", typeof(TForm).Name);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogDebug(oce, "Showing child form {FormType} was canceled", typeof(TForm).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show child form {FormType}", typeof(TForm).Name);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
#endif
            throw;
        }
    }

    private void ShowChildFormNonMdi<TForm, TViewModel>(bool allowMultiple = false)
        where TForm : Form
        where TViewModel : class
    {
        try
        {
            _logger.LogInformation("Showing child form {FormType} (non-MDI mode, AllowMultiple: {AllowMultiple})",
                typeof(TForm).Name, allowMultiple);

            // In non-MDI mode, check if we should reuse an existing window
            if (!allowMultiple && _activeMdiChildren.TryGetValue(typeof(TForm), out var existingForm))
            {
                if (existingForm != null && !existingForm.IsDisposed)
                {
                    try
                    {
                        existingForm.BringToFront();
                        existingForm.Activate();
                    }
                    catch { }
                    _logger.LogDebug("Activated existing non-MDI child {FormType}", typeof(TForm).Name);
                    return;
                }

                _activeMdiChildren.Remove(typeof(TForm));
            }

            // Create a new scope to get fresh DbContext + ViewModels for each child window
            var scope = _serviceProvider.CreateScope();
            var form = CreateFormInstance<TForm, TViewModel>(scope);

            // Apply current theme to child form
            try
            {
                AppThemeColors.ApplyTheme(form);
                _logger.LogDebug("Theme applied to child form {FormType}", typeof(TForm).Name);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply theme to child form {FormType}", typeof(TForm).Name);
            }

            // Handle form closing to clean up scope and tracking
            form.FormClosed += (s, e) =>
            {
                try
                {
                    _activeMdiChildren.Remove(typeof(TForm));
                    scope.Dispose();
                    _logger.LogDebug("Non-MDI child {FormType} closed and cleaned up", typeof(TForm).Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up non-MDI child {FormType}", typeof(TForm).Name);
                }
            };

            // Track the form
            if (!allowMultiple)
            {
                _activeMdiChildren[typeof(TForm)] = form;
            }

            // Show the form as a separate window (non-modal)
            form.Show();

            _logger.LogInformation("Non-MDI child form {FormType} shown", typeof(TForm).Name);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogDebug(oce, "Showing child form {FormType} was canceled", typeof(TForm).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show child form {FormType}", typeof(TForm).Name);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }
#endif
            throw;
        }
    }

    internal void RegisterMdiChildWithDocking(Form child)
    {
        if (child == null)
        {
            return;
        }

        if (_dockingManager == null || !_uiConfig.UseSyncfusionDocking)
        {
            return;
        }

        // Phase 1 Simplification: Standard MDI with docking (no TabbedMDI)
        // DockingManager handles dockable panels; Forms use standard MDI pattern
        if (_uiConfig.UseTabbedMdi) { _dockingManager.EnableDocumentMode = false; }

        // Ensure MDI child Forms are not treated as dockable windows.
        try
        {
            // _dockingManager is guaranteed non-null here (guarded above); call directly to avoid dead code.
            _dockingManager.SetEnableDocking(child, false);
        }
        catch
        {
            // Some Syncfusion builds may throw when calling SetEnableDocking on top-level Forms.
        }
    }

    public void RegisterAsDockingMDIChild(Form child, bool enabled)
    {
        // Delegate to the actual implementation (enabled parameter is ignored)
        RegisterMdiChildWithDocking(child);
    }

    public void CloseSettingsPanel()
    {
        // Find and close any SettingsForm child windows
        foreach (Form childForm in this.MdiChildren)
        {
            if (childForm is SettingsForm settingsForm)
            {
                settingsForm.Close();
                return;
            }
        }
    }

    public void ClosePanel(string panelName)
    {
        // Find and close child form or panel by name using LINQ
        var matchingForm = this.MdiChildren.FirstOrDefault(f =>
            f.Text.Contains(panelName, StringComparison.OrdinalIgnoreCase));

        matchingForm?.Close();
    }

    private TForm CreateFormInstance<TForm, TViewModel>(IServiceScope scope)
        where TForm : Form
        where TViewModel : class
    {
        if (typeof(TForm) == typeof(ChartForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ChartViewModel>(scope.ServiceProvider);
            var chartForm = ActivatorUtilities.CreateInstance<ChartForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)chartForm;
        }

        if (typeof(TForm) == typeof(AccountsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(scope.ServiceProvider);
            var accountsForm = ActivatorUtilities.CreateInstance<AccountsForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)accountsForm;
        }

        if (typeof(TForm) == typeof(BudgetOverviewForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<BudgetOverviewViewModel>(scope.ServiceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<BudgetOverviewForm>>(scope.ServiceProvider);
            var budgetOverviewForm = ActivatorUtilities.CreateInstance<BudgetOverviewForm>(scope.ServiceProvider, vm, logger, this);
            return (TForm)(Form)budgetOverviewForm;
        }

        if (typeof(TForm) == typeof(DashboardForm))
        {
            // Prefer a DI-provided instance when available (tests can register a lightweight stub)
            try
            {
                var provided = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DashboardForm>(scope.ServiceProvider);
                if (provided != null)
                {
                    return (TForm)(Form)provided;
                }
            }
            catch { }

            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DashboardViewModel>(scope.ServiceProvider);
            var dashboardForm = ActivatorUtilities.CreateInstance<DashboardForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)dashboardForm;
        }

        if (typeof(TForm) == typeof(ReportsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ReportsViewModel>(scope.ServiceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ReportsForm>>(scope.ServiceProvider);
            var reportsForm = ActivatorUtilities.CreateInstance<ReportsForm>(scope.ServiceProvider, vm, logger, this);
            return (TForm)(Form)reportsForm;
        }

        if (typeof(TForm) == typeof(SettingsForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(scope.ServiceProvider);
            var settingsForm = ActivatorUtilities.CreateInstance<SettingsForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)settingsForm;
        }

        if (typeof(TForm) == typeof(ChatWindow))
        {
            var chatWindow = ActivatorUtilities.CreateInstance<ChatWindow>(scope.ServiceProvider, this);
            return (TForm)(Form)chatWindow;
        }

        if (typeof(TForm) == typeof(QuickBooksForm))
        {
            var quickBooksForm = ActivatorUtilities.CreateInstance<QuickBooksForm>(scope.ServiceProvider, this);
            return (TForm)(Form)quickBooksForm;
        }

        if (typeof(TForm) == typeof(CustomersForm))
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CustomersViewModel>(scope.ServiceProvider);
            var customersForm = ActivatorUtilities.CreateInstance<CustomersForm>(scope.ServiceProvider, vm, this);
            return (TForm)(Form)customersForm;
        }

        return ActivatorUtilities.CreateInstance<TForm>(scope.ServiceProvider);
    }

    private IEnumerable<TForm> GetMdiChildrenOfType<TForm>() where TForm : Form
    {
        return MdiChildren.OfType<TForm>();
    }

    private bool ActivateMdiChildOfType<TForm>() where TForm : Form
    {
        var existingForm = GetMdiChildrenOfType<TForm>().FirstOrDefault();
        if (existingForm != null && !existingForm.IsDisposed)
        {
            existingForm.Activate();
            return true;
        }
        return false;
    }

    private void DisposeMdiResources()
    {
        try
        {
        // Phase 1 Simplification: TabbedMDIManager permanently removed - no disposal needed

        // Close all MDI children before disposal
            if (_uiConfig.UseMdiMode && MdiChildren.Length > 0)
            {
                CloseAllMdiChildren();
            }

            _activeMdiChildren.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing MDI resources");
        }
    }

    private void HandleMdiKeyboardShortcuts(KeyEventArgs e)
    {
        if (!_uiConfig.UseMdiMode) return;

        if (e.Control && e.KeyCode == Keys.Tab && !e.Shift)
        {
            ActivateNextMdiChild();
            e.Handled = true;
        }
        else if (e.Control && e.Shift && e.KeyCode == Keys.Tab)
        {
            ActivatePreviousMdiChild();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.F4)
        {
            ActiveMdiChild?.Close();
            e.Handled = true;
        }
    }

    private void ActivateNextMdiChild()
    {
        var children = MdiChildren;
        if (children.Length <= 1) return;

        var activeChild = ActiveMdiChild;
        if (activeChild == null)
        {
            children[0].Activate();
            return;
        }

        var currentIndex = Array.IndexOf(children, activeChild);
        var nextIndex = (currentIndex + 1) % children.Length;
        children[nextIndex].Activate();
    }

    private void ActivatePreviousMdiChild()
    {
        var children = MdiChildren;
        if (children.Length <= 1) return;

        var activeChild = ActiveMdiChild;
        if (activeChild == null)
        {
            children[children.Length - 1].Activate();
            return;
        }

        var currentIndex = Array.IndexOf(children, activeChild);
        var previousIndex = currentIndex == 0 ? children.Length - 1 : currentIndex - 1;
        children[previousIndex].Activate();
    }

    public void ConfigureChildMenuMerging(MenuStrip childMenuStrip)
    {
        if (childMenuStrip == null) return;

        childMenuStrip.AllowMerge = true;

        var parentMenuStrip = Controls.OfType<MenuStrip>().FirstOrDefault();
        if (parentMenuStrip != null)
        {
            parentMenuStrip.AllowMerge = true;
            _logger.LogDebug("Configured menu merging for child form");
        }
    }

    #endregion
}
