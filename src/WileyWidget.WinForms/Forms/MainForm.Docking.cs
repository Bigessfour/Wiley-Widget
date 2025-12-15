using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using System.Drawing;
using System.Reflection;
using System.Linq;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Runtime.Serialization;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Windows.Forms;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using Syncfusion.WinForms.Themes;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// MainForm partial class for Syncfusion DockingManager (Phase 2)
/// Provides advanced docking features: floating windows, tabbed documents,
/// persistent layouts, and AI-first architecture with collapsible side panels.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class MainForm
{
    private DockingManager? _dockingManager;
    private Panel? _leftDockPanel;
    private Panel? _rightDockPanel;
    private Panel? _centralDocumentPanel;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _activityGrid;
    private System.Windows.Forms.Timer? _activityRefreshTimer;
    private bool _useSyncfusionDocking = false;  // Feature flag - set true to enable
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";
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
    // Dashboard description labels (populated by CreateDashboardCard)
    private Label? _accountsDescLabel;
    private Label? _chartsDescLabel;
    private Label? _settingsDescLabel;
    private Label? _reportsDescLabel;
    private Label? _infoDescLabel;

    /// <summary>
    /// Initialize Syncfusion DockingManager with AI-first layout
    /// Call this from constructor after InitializeComponent() to enable Phase 2 docking
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        if (!_useSyncfusionDocking)
        {
            _logger.LogDebug("Syncfusion docking disabled via feature flag");
            return;
        }

        try
        {
            // Instantiate DockingManager per official Syncfusion API
            components ??= new System.ComponentModel.Container();
            _dockingManager = new DockingManager(components);
            _dockingManager.HostControl = this;

            // When using TabbedMDI, DockingManager's DocumentMode must be disabled to avoid
            // casting MDI child Forms to Syncfusion internal DockingWrapperForm.
            // Document mode should only be enabled when Docking is in use, MDI mode is enabled,
            // and TabbedMDI is NOT being used.
            var shouldEnableDocumentMode = _useSyncfusionDocking && _useMdiMode && !_useTabbedMdi;
            _logger.LogInformation("Setting EnableDocumentMode: UseDocking={UseDocking}, _useMdiMode={MdiMode}, _useTabbedMdi={TabbedMdi}, shouldEnableDocumentMode={ShouldEnable}",
                _useSyncfusionDocking, _useMdiMode, _useTabbedMdi, shouldEnableDocumentMode);

            // Apply computed document-mode once at initialization. Never toggle per-child.
            _dockingManager.EnableDocumentMode = shouldEnableDocumentMode;

            if (_dockingManager.EnableDocumentMode)
            {
                _logger.LogInformation("DockingManager document mode enabled (DocumentContainer will host documents)");
            }
            else
            {
                _logger.LogInformation("DockingManager document mode disabled (using DockingManager for panels only)");
            }

            // Enable automatic persistence - Syncfusion handles save/load internally
            _dockingManager.PersistState = true;
            _dockingManager.AnimateAutoHiddenWindow = true;
            _dockingManager.AutoHideTabFont = _dockAutoHideTabFont = new Font("Segoe UI", 9f);
            _dockingManager.DockTabFont = _dockTabFont = new Font("Segoe UI", 9f);
            _dockingManager.ShowCaption = true;
            try
            {
                SfSkinManager.SetVisualStyle(_dockingManager, ThemeColors.DefaultTheme);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply theme to DockingManager");
            }


            CreateLeftDockPanel();
            CreateCentralDocumentPanel();
            CreateRightDockPanel();

            // Hide standard panels when using docking
            HideStandardPanelsForDocking();



            // Apply theme to docked panels
            ApplyThemeToDockingPanels();



            _logger.LogInformation("DockingManager initialized successfully");
        }
        catch (Syncfusion.Windows.Forms.Tools.DockingManagerException dockEx)
        {
            _logger.LogError(dockEx, "DockingManagerException during initialization: {Message}. InnerException: {InnerException}",
                dockEx.Message, dockEx.InnerException?.Message);
            System.Diagnostics.Debug.WriteLine($"[DOCKING ERROR] {dockEx.GetType().Name}: {dockEx.Message}");
            System.Diagnostics.Debug.WriteLine($"  InnerException: {dockEx.InnerException}");
            System.Diagnostics.Debug.WriteLine($"  StackTrace: {dockEx.StackTrace}");
            // Fall back to standard docking
            _useSyncfusionDocking = false;
            try
            {
                ShowStandardPanelsAfterDockingFailure();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to show standard panels after DockingManagerException");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Syncfusion DockingManager: {Type} - {Message}",
                ex.GetType().Name, ex.Message);
            System.Diagnostics.Debug.WriteLine($"[DOCKING ERROR] {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"  InnerException: {ex.InnerException.Message}");
            }
            // Fall back to standard docking
            _useSyncfusionDocking = false;
            try
            {
                ShowStandardPanelsAfterDockingFailure();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to show standard panels after general exception");
            }

            // User-friendly fallback: Show message and continue with standard panels
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
            }
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
            BackColor = ThemeColors.Background,
            AutoScroll = true
        };

        // Move dashboard cards to left dock (reuse existing dashboard panel logic)
        var dashboardContent = CreateDashboardCardsPanel();
        _leftDockPanel.Controls.Add(dashboardContent);

        // Configure docking behavior
        _dockingManager.SetEnableDocking(_leftDockPanel, true);
        _dockingManager.DockControl(_leftDockPanel, this, DockingStyle.Left, 250);
        _dockingManager.SetAutoHideMode(_leftDockPanel, true);  // Collapsible
        _dockingManager.SetDockLabel(_leftDockPanel, "Dashboard");
        _dockingManager.SetAllowFloating(_leftDockPanel, true);  // Enable floating windows

        // Set as MDI child to integrate with TabbedMDIManager
        if (_useMdiMode && IsMdiContainer)
        {
            _dockingManager.SetAsMDIChild(_leftDockPanel, true);
        }

        _logger.LogDebug("Left dock panel created with dashboard cards");
    }

    /// <summary>
    /// Create central document panel with AI chat as primary tab.
    /// Supports both standard panel mode and MDI container mode for multiple documents.
    /// </summary>
    private void CreateCentralDocumentPanel()
    {
        if (_dockingManager == null || _aiChatControl == null) return;

        _centralDocumentPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            AccessibleName = "CentralDocumentPanel",
            Dock = DockStyle.Fill,
            BackColor = ThemeColors.Background,
            Visible = true
        };

        // Add AI Chat as primary document in the central panel
        // Note: When MDI mode is enabled, this panel can be replaced with MdiClient
        // to support multiple document windows
        _centralDocumentPanel.Controls.Add(_aiChatControl);
        _aiChatControl.Dock = DockStyle.Fill;
        try { _aiChatControl.Visible = true; } catch { }

        // IMPORTANT: When EnableDocumentMode = true and HostControl is set,
        // the central fill area should NOT be docked via DockControl() with DockingStyle.Fill.
        // Syncfusion DockingManager explicitly prohibits docking with Fill style to the host control.
        // Instead, add the panel directly to the form's Controls collection with standard WinForms docking.
        // Side panels (Left, Right, Top, Bottom) use DockControl(), the center uses standard Fill docking.
        if (_useMdiMode && IsMdiContainer)
        {
            // Replace central panel with the form's MdiClient so child windows render inside central docked area
            var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
            if (mdiClient != null)
            {
                try
                {
                    // Ensure z-order and docking
                    mdiClient.Dock = DockStyle.Fill;
                    mdiClient.SendToBack();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to configure MdiClient as central document area - using central panel fallback");
                    Controls.Add(_centralDocumentPanel);
                    _centralDocumentPanel.BringToFront();
                }
            }
            else
            {
                Controls.Add(_centralDocumentPanel);
                try { SfSkinManager.SetVisualStyle(_centralDocumentPanel, ThemeColors.DefaultTheme); } catch { }
                try { _centralDocumentPanel.BringToFront(); } catch { }
                _centralDocumentPanel.BringToFront();
            }
        }
        else
        {
            Controls.Add(_centralDocumentPanel);
            try { SfSkinManager.SetVisualStyle(_centralDocumentPanel, ThemeColors.DefaultTheme); } catch { }
            try { _centralDocumentPanel.BringToFront(); } catch { }
            _centralDocumentPanel.BringToFront();
        }

        // MDI Support: If MDI mode is active, this central panel coexists with MdiClient
        // The MdiClient will be set as the form's MdiClient property and will handle
        // child window management separately from the docking framework

        // Ensure the central panel is visible immediately after creation
        EnsureCentralPanelVisibility();

        _logger.LogDebug("Central document panel created with AI chat (standard Fill docking, MDI-compatible)");
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
            BackColor = ThemeColors.Background,
            Padding = new Padding(10)
        };

        // Move activity grid to right dock (reuse existing activity panel logic)
        var activityContent = CreateActivityGridPanel();
        _rightDockPanel.Controls.Add(activityContent);

        // Configure docking behavior
        _dockingManager.SetEnableDocking(_rightDockPanel, true);
        _dockingManager.DockControl(_rightDockPanel, this, DockingStyle.Right, 200);
        _dockingManager.SetAutoHideMode(_rightDockPanel, true);  // Collapsible
        _dockingManager.SetDockLabel(_rightDockPanel, "Activity");
        _dockingManager.SetAllowFloating(_rightDockPanel, true);  // Enable floating windows

        // Set as MDI child to integrate with TabbedMDIManager
        if (_useMdiMode && IsMdiContainer)
        {
            _dockingManager.SetAsMDIChild(_rightDockPanel, true);
        }

        _logger.LogDebug("Right dock panel created with activity grid");
    }

    /// <summary>
    /// Create dashboard cards panel (extracted for reuse in docking)
    /// </summary>
    private Panel CreateDashboardCardsPanel()
    {
        var dashboardPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,  // Single column for left dock
            RowCount = 5,
            Padding = new Padding(10),
            BackColor = ThemeColors.Background
        };

        // Add row styles
        for (int i = 0; i < 5; i++)
        {
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }

        // Create cards (reuse existing logic from InitializeComponent)
        var accountsCard = CreateDashboardCard("Accounts", MainFormResources.LoadingText, ThemeColors.PrimaryAccent, out _accountsDescLabel);
        SetupCardClickHandler(accountsCard, () => ShowChildForm<AccountsForm, AccountsViewModel>());

        var chartsCard = CreateDashboardCard("Charts", MainFormResources.LoadingText, ThemeColors.Success, out _chartsDescLabel);
        SetupCardClickHandler(chartsCard, () => ShowChildForm<ChartForm, ChartViewModel>());

        var settingsCard = CreateDashboardCard("Settings", MainFormResources.LoadingText, ThemeColors.Warning, out _settingsDescLabel);
        SetupCardClickHandler(settingsCard, () => ShowChildForm<SettingsForm, SettingsViewModel>());

        var reportsCard = CreateDashboardCard("Reports", MainFormResources.LoadingText, ThemeColors.PrimaryAccent, out _reportsDescLabel);
        SetupCardClickHandler(reportsCard, () => ShowChildForm<ReportsForm, ReportsViewModel>());

        var infoCard = CreateDashboardCard("Budget Status", MainFormResources.LoadingText, ThemeColors.Error, out _infoDescLabel);

        dashboardPanel.Controls.Add(accountsCard, 0, 0);
        dashboardPanel.Controls.Add(chartsCard, 0, 1);
        dashboardPanel.Controls.Add(settingsCard, 0, 2);
        dashboardPanel.Controls.Add(reportsCard, 0, 3);
        dashboardPanel.Controls.Add(infoCard, 0, 4);

        return dashboardPanel;
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
            BackColor = ThemeColors.Background,
            Padding = new Padding(10)
        };

        var activityHeader = new Label
        {
            Text = "Recent Activity",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = ThemeManager.Colors.TextPrimary,
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
        SfSkinManager.SetVisualStyle(_activityGrid, ThemeColors.DefaultTheme);

        // Map to ActivityLog properties
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridDateTimeColumn { MappingName = "Timestamp", HeaderText = "Time", Format = "HH:mm", Width = 80 });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Action", Width = 150 });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details", Width = 200 });
        _activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "User", HeaderText = "User", Width = 100 });

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
            var activityLogRepository = ServiceProviderExtensions.GetService<IActivityLogRepository>(scope.ServiceProvider);
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
        // Hide standard split container and AI panel
        foreach (Control control in Controls)
        {
            if (control is SplitContainer || (control is Panel panel && panel == _aiChatPanel))
            {
                try
                {
                    control.Visible = false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
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
            if (control is SplitContainer || (control is Panel panel && panel == _aiChatPanel))
            {
                try
                {
                    control.Visible = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to show standard panel {ControlName} after docking failure", control.Name);
                }
            }
        }
        _logger.LogDebug("Standard panels restored after docking failure");
    }

    /// <summary>
    /// Load saved docking layout from AppData
    /// Implements state persistence using AppStateSerializer with enhanced error handling
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/layouts
    /// </summary>
    private void LoadDockingLayout()
    {
        if (_dockingManager == null) return;

        // Syncfusion guidance: skip persistence calls until host handle and message loop are ready
        if (!IsHandleCreated || !Application.MessageLoop)
        {
            _logger.LogDebug("Skipping LoadDockingLayout: handle not created or message loop not running");
            return;
        }

        if (this.IsDisposed || this.Disposing)
        {
            _logger.LogDebug("Skipping LoadDockingLayout: form disposing/disposed");
            return;
        }

        if (this.InvokeRequired)
        {
            try { this.Invoke(new System.Action(LoadDockingLayout)); } catch { }
            return;
        }

        // Instrumentation: log preconditions and thread info to aid debugging of first-chance NREs
        try
        {
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _logger.LogDebug("LoadDockingLayout START - ThreadId={ThreadId}, InvokeRequired={InvokeRequired}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime}",
                threadId, this.InvokeRequired, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
        }
        catch { }

        try
        {
            var layoutPath = GetDockingLayoutPath();
            if (!File.Exists(layoutPath))
            {
                _logger.LogInformation("No saved docking layout found at {Path} - using default layout", layoutPath);
                return;
            }

            var layoutFileInfo = new FileInfo(layoutPath);
            if (layoutFileInfo.Length == 0)
            {
                _logger.LogInformation("Docking layout file {Path} is empty - resetting to default layout", layoutPath);
                try
                {
                    File.Delete(layoutPath);
                    _dockingManager.LoadDesignerDockState();
                    ApplyThemeToDockingPanels();
                    _logger.LogInformation("Docking layout reset to default successfully");
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete empty docking layout file {Path}", layoutPath);
                }
                return;
            }

            // Load dynamic panel metadata before loading dock state
            var dynamicPanelInfos = LoadDynamicPanelMetadata(layoutPath);

            // Recreate dynamic panels
            foreach (var panelInfo in dynamicPanelInfos)
            {
                try
                {
                    RecreateDynamicPanel(panelInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to recreate dynamic panel '{PanelName}'", panelInfo.Name);
                }
            }

            // Validate XML structure before loading to catch corruption early
            try
            {
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.Load(layoutPath);
                _logger.LogDebug("Layout XML validated successfully");
            }
            catch (System.Xml.XmlException xmlEx)
            {
                _logger.LogInformation(xmlEx, "Corrupt XML layout file detected at {Path} - resetting to default layout", layoutPath);
                try
                {
                    File.Delete(layoutPath);
                    _logger.LogInformation("Deleted corrupt layout file");
                    _dockingManager.LoadDesignerDockState();
                    ApplyThemeToDockingPanels();
                    _logger.LogInformation("Docking layout reset to default successfully");
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete corrupt layout file - will be overwritten on save");
                }
                return;
            }

            // Use Syncfusion's AppStateSerializer for proper state loading
            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, layoutPath);
            try
            {
                // Instrumentation: log just before entering Syncfusion deserialization (info for log capture)
                _logger.LogInformation("Calling _dockingManager.LoadDockState - ThreadId={ThreadId}, layoutPath={Path}, InvokeRequired={InvokeRequired}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
                    System.Threading.Thread.CurrentThread.ManagedThreadId, layoutPath, this.InvokeRequired, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
                _dockingManager.LoadDockState(serializer);
                _logger.LogInformation("Docking layout loaded from {Path}", layoutPath);
            }
            catch (NullReferenceException nre)
            {
                _logger.LogWarning(nre, "NullReferenceException while loading docking layout - layout may be corrupt. Resetting to default.");
                try { File.Delete(layoutPath); } catch { }
                try { _dockingManager.LoadDesignerDockState(); ApplyThemeToDockingPanels(); } catch (Exception fallbackEx) { _logger.LogWarning(fallbackEx, "Failed to reset docking layout after NRE"); }
                return;
            }
            catch (Exception loadEx)
            {
                // If loading fails (can happen when controls have changed or state is corrupt),
                // remove the layout file and fall back to the designer layout to avoid
                // Syncfusion internal NullReferenceExceptions from its serialization wrapper.
                _logger.LogWarning(loadEx, "Failed to load docking layout from {Path} - resetting to default layout", layoutPath);
                try
                {
                    File.Delete(layoutPath);
                    _logger.LogInformation("Deleted corrupt docking layout file {Path}", layoutPath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete corrupt docking layout file {Path}", layoutPath);
                }

                try
                {
                    _dockingManager.LoadDesignerDockState();
                    ApplyThemeToDockingPanels();
                    _logger.LogInformation("Docking layout reset to default successfully after failed load");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Failed to reset docking layout after failed load");
                }

                return;
            }
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
    /// Save current docking layout to AppData with fallback to temp directory
    /// Implements state persistence using Syncfusion DockingManager serialization
    /// Captures: panel positions, sizes, docking states, floating window states, tab order
    /// Includes custom persistence for dynamic panels to ensure they are recreated on load
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/layouts
    /// </summary>
    private void SaveDockingLayout()
    {
        if (_dockingManager == null)
        {
            return;
        }

        if (!IsHandleCreated || !Application.MessageLoop)
        {
            _logger.LogDebug("Skipping SaveDockingLayout: handle not created or message loop not running");
            return;
        }

        if (this.IsDisposed || this.Disposing)
        {
            _logger.LogDebug("Skipping SaveDockingLayout: form disposing/disposed");
            return;
        }

        if (this.InvokeRequired)
        {
            try { this.Invoke(new System.Action(SaveDockingLayout)); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to marshal SaveDockingLayout to UI thread"); }
            return;
        }

        lock (_dockingSaveLock)
        {
            if (_isSavingLayout)
            {
                _logger.LogDebug("Skipping manual save - another save is in progress");
                return;
            }

            _isSavingLayout = true;
        }

        try
        {
            try
            {
                var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                _logger.LogDebug("SaveDockingLayout START - ThreadId={ThreadId}, InvokeRequired={InvokeRequired}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
                    threadId, this.InvokeRequired, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
            }
            catch { }

            // Avoid invoking Syncfusion's internal serialization wrapper when no dock panels exist
            try
            {
                var hasDockControls = false;
                if (_leftDockPanel != null && !_leftDockPanel.IsDisposed) hasDockControls = true;
                if (_rightDockPanel != null && !_rightDockPanel.IsDisposed) hasDockControls = true;
                if (_dynamicDockPanels != null && _dynamicDockPanels.Count > 0) hasDockControls = true;

                if (!hasDockControls)
                {
                    var now = DateTime.Now;
                    var elapsedMs = _lastSaveTime == DateTime.MinValue ? double.NaN : (now - _lastSaveTime).TotalMilliseconds;
                    _logger.LogDebug("Skipping SaveDockingLayout - hasDockControls={HasDockControls}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, TimeSinceLastSaveMs={ElapsedMs}",
                        hasDockControls, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, elapsedMs);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error inspecting dock panel state - skipping docking layout save to avoid potential Syncfusion NRE");
                return;
            }

            string? layoutPath = GetDockingLayoutPath();
            var layoutDir = Path.GetDirectoryName(layoutPath);

            if (!string.IsNullOrEmpty(layoutDir))
            {
                if (!Directory.Exists(layoutDir))
                {
                    try
                    {
                        Directory.CreateDirectory(layoutDir);
                        _logger.LogDebug("Created docking layout directory at {Dir}", layoutDir);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        layoutPath = Path.Combine(Path.GetTempPath(), DockingLayoutFileName);
                        _logger.LogWarning("AppData directory creation failed - using temp directory: {Path}", layoutPath);
                    }
                }
                else
                {
                    try
                    {
                        using var testStream = File.Open(layoutPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                        testStream.Close();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        layoutPath = Path.Combine(Path.GetTempPath(), DockingLayoutFileName);
                        _logger.LogWarning("No write permission to AppData - using temp directory: {Path}", layoutPath);
                    }
                }
            }

            var tempLayoutPath = layoutPath + ".tmp";
            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, tempLayoutPath);

            try
            {
                var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                _logger.LogInformation("Calling _dockingManager.SaveDockState - ThreadId={ThreadId}, tempLayoutPath={TempPath}, finalLayoutPath={FinalPath}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}, MessageLoop={MessageLoop}, _isSavingLayout={IsSavingLayout}, _lastSaveTime={LastSaveTime:o}",
                    threadId, tempLayoutPath, layoutPath, this.IsDisposed, this.IsHandleCreated, Application.MessageLoop, _isSavingLayout, _lastSaveTime);
                _dockingManager.SaveDockState(serializer);
            }
            catch (NullReferenceException nre)
            {
                _logger.LogWarning(nre, "Syncfusion.SaveDockState raised NullReferenceException - deleting layout to prevent corruption.");
                TryDeleteLayoutFiles(layoutPath);
                TryCleanupTempFile(tempLayoutPath);
                return;
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Syncfusion.SaveDockState raised ArgumentException - deleting layout to prevent corruption.");
                TryDeleteLayoutFiles(layoutPath);
                TryCleanupTempFile(tempLayoutPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Syncfusion.SaveDockState failed - aborting save.");
                TryCleanupTempFile(tempLayoutPath);
                return;
            }

            SaveDynamicPanelMetadata(tempLayoutPath);
            ReplaceDockingLayoutFile(tempLayoutPath, layoutPath);

            _lastSaveTime = DateTime.Now;
            _logger.LogInformation("Docking layout saved to {Path}", layoutPath);
        }
        catch (UnauthorizedAccessException authEx)
        {
            _logger.LogError(authEx, "Permission denied saving docking layout");
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "I/O error saving docking layout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save docking layout");
        }
        finally
        {
            lock (_dockingSaveLock)
            {
                _isSavingLayout = false;
            }
        }
    }

    /// <summary>
    /// Save metadata about dynamic panels to the layout XML file
    /// This ensures dynamic panels can be recreated when the layout is loaded
    /// </summary>
    /// <param name="layoutPath">Path to the layout XML file</param>
    private void SaveDynamicPanelMetadata(string layoutPath)
    {
        if (_dynamicDockPanels == null || _dynamicDockPanels.Count == 0)
            return;

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(layoutPath);

            // Create or get the custom WileyWidget metadata node
            var metadataNode = xmlDoc.SelectSingleNode("//WileyWidgetDynamicPanels");
            if (metadataNode == null)
            {
                metadataNode = xmlDoc.CreateElement("WileyWidgetDynamicPanels");
                xmlDoc.DocumentElement?.AppendChild(metadataNode);
            }
            else
            {
                metadataNode.RemoveAll(); // Clear existing data
            }

            // Add each dynamic panel's metadata
            foreach (var kvp in _dynamicDockPanels)
            {
                var panelName = kvp.Key;
                var panel = kvp.Value;

                var panelNode = xmlDoc.CreateElement("DynamicPanel");
                panelNode.SetAttribute("Name", panelName);
                panelNode.SetAttribute("Type", panel.GetType().FullName ?? "System.Windows.Forms.Panel");

                // Get docking information from DockingManager
                if (_dockingManager != null)
                {
                    try
                    {
                        var dockLabel = _dockingManager.GetDockLabel(panel);
                        if (!string.IsNullOrEmpty(dockLabel))
                        {
                            panelNode.SetAttribute("DockLabel", dockLabel);
                        }

                        var isAutoHide = _dockingManager.GetAutoHideMode(panel);
                        panelNode.SetAttribute("IsAutoHide", isAutoHide.ToString());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get docking info for dynamic panel '{PanelName}'", panelName);
                    }
                }

                metadataNode.AppendChild(panelNode);
            }

            xmlDoc.Save(layoutPath);
            _logger.LogDebug("Saved metadata for {Count} dynamic panels", _dynamicDockPanels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save dynamic panel metadata to layout file");
        }
    }

    /// <summary>
    /// Load metadata about dynamic panels from the layout XML file
    /// </summary>
    /// <param name="layoutPath">Path to the layout XML file</param>
    /// <returns>List of dynamic panel information</returns>
    private List<DynamicPanelInfo> LoadDynamicPanelMetadata(string layoutPath)
    {
        var panelInfos = new List<DynamicPanelInfo>();

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(layoutPath);

            var metadataNode = xmlDoc.SelectSingleNode("//WileyWidgetDynamicPanels");
            if (metadataNode == null)
                return panelInfos;

            foreach (XmlNode panelNode in metadataNode.ChildNodes)
            {
                if (panelNode.Name != "DynamicPanel")
                    continue;

                var name = panelNode.Attributes?["Name"]?.Value;
                var type = panelNode.Attributes?["Type"]?.Value;
                var dockLabel = panelNode.Attributes?["DockLabel"]?.Value;
                var isAutoHide = panelNode.Attributes?["IsAutoHide"]?.Value;

                if (!string.IsNullOrEmpty(name))
                {
                    panelInfos.Add(new DynamicPanelInfo
                    {
                        Name = name,
                        Type = type ?? "System.Windows.Forms.Panel",
                        IsAutoHide = isAutoHide == "True"
                    });
                }
            }

            _logger.LogDebug("Loaded metadata for {Count} dynamic panels", panelInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dynamic panel metadata from layout file");
        }

        return panelInfos;
    }

    /// <summary>
    /// Recreate a dynamic panel based on saved metadata
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
                Name = panelInfo.Name,
                BackColor = ThemeColors.Background,
                ForeColor = Color.Black
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
    private class DynamicPanelInfo
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
        catch { }

        TryCleanupTempFile(layoutPath + ".tmp");
    }

    private static void ReplaceDockingLayoutFile(string tempPath, string finalPath)
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
        catch { }
    }
    /// Get docking layout file path in AppData
    /// </summary>
    private static string GetDockingLayoutPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var wileyWidgetPath = Path.Combine(appDataPath, "WileyWidget");
        return Path.Combine(wileyWidgetPath, DockingLayoutFileName);
    }

    /// <summary>
    /// Toggle between standard and Syncfusion docking modes
    /// </summary>
    private void ToggleDockingMode()
    {
        _useSyncfusionDocking = !_useSyncfusionDocking;

        if (_useSyncfusionDocking)
        {
            InitializeSyncfusionDocking();
            _logger.LogInformation("Switched to Syncfusion docking mode");
        }
        else
        {
            DisposeSyncfusionDocking();
            ShowStandardPanelsAfterDockingFailure();
            _logger.LogInformation("Switched to standard docking mode");
        }
    }

    /// <summary>
    /// Dispose Syncfusion docking manager and restore standard layout
    /// </summary>
    private void DisposeSyncfusionDocking()
    {
        if (_dockingManager != null)
        {
            SaveDockingLayout();

            _dockingManager.DockStateChanged -= DockingManager_DockStateChanged;
            _dockingManager.DockControlActivated -= DockingManager_DockControlActivated;
            _dockingManager.DockVisibilityChanged -= DockingManager_DockVisibilityChanged;

            _dockingManager.Dispose();
            _dockingManager = null;
        }

        // Dispose debounce timer
        if (_dockingLayoutSaveTimer != null)
        {
            _dockingLayoutSaveTimer.Stop();
            _dockingLayoutSaveTimer.Tick -= OnSaveTimerTick;
            _dockingLayoutSaveTimer.Dispose();
            _dockingLayoutSaveTimer = null;
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing dynamic dock panel '{PanelName}'", panel.Name);
                }
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

    #region Theme Integration

    /// <summary>
    /// Apply Syncfusion theme to docked panels using SkinManager (single authority).
    /// </summary>
    private void ApplyThemeToDockingPanels()
    {
        try
        {
            // Ensure the DockingManager and its children inherit the global theme
            if (_dockingManager != null)
            {
                try
                {
                    SfSkinManager.SetVisualStyle(_dockingManager, ThemeColors.DefaultTheme);

                    // No per-control ThemeName assignments - rely on SfSkinManager (global SkinManager) for visual styles
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DockingManager theme apply fallback");
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
        }
    }

    private static void ApplyPanelTheme(Control? panel)
    {
        if (panel == null) return;

        try
        {
            panel.BackColor = ThemeColors.Background;
            panel.ForeColor = Color.Black;
            SfSkinManager.SetVisualStyle(panel, ThemeColors.DefaultTheme);
        }
        catch
        {
            // Non-blocking; fallback colors already set
        }
    }

    #endregion

    #region Docking Event Handlers

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        // Log docking state changes
        _logger.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Ensure central panels remain visible after state changes
        EnsureCentralPanelVisibility();

        // Auto-save layout on state changes with debouncing to prevent I/O spam
        if (_useSyncfusionDocking)
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
        catch { }

        // Skip if already saving to prevent concurrent I/O operations
        if (_isSavingLayout)
        {
            _logger.LogDebug("Skipping debounced save - save already in progress");
            return;
        }

        // Enforce minimum interval between saves
        var timeSinceLastSave = DateTime.Now - _lastSaveTime;
        if (timeSinceLastSave < MinimumSaveInterval)
        {
            _logger.LogDebug("Skipping debounced save - too soon since last save ({Elapsed}ms ago)",
                timeSinceLastSave.TotalMilliseconds);
            return;
        }

        // Stop existing timer if running
        _dockingLayoutSaveTimer?.Stop();

        // Initialize timer on first use with increased interval
        if (_dockingLayoutSaveTimer == null)
        {
            _dockingLayoutSaveTimer = new System.Windows.Forms.Timer { Interval = 1500 }; // Increased from 500ms
            _dockingLayoutSaveTimer.Tick += OnSaveTimerTick;
        }

        // Restart timer - will fire after 1500ms of no state changes
        _dockingLayoutSaveTimer.Start();
    }

    /// <summary>
    /// Timer tick handler - performs actual save after debounce period
    /// </summary>
    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _dockingLayoutSaveTimer?.Stop();

        // Prevent concurrent saves
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
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger.LogDebug("Dock control activated: {Control}", e.Control.Name);

        // Auto-focus input when AI chat is activated
        if (e.Control == _aiChatControl && _aiChatControl != null)
        {
            _aiChatControl.Focus();
        }
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        // Log visibility changes
        _logger.LogDebug("Dock visibility changed");

        // Ensure central panels remain visible after visibility changes
        EnsureCentralPanelVisibility();
    }

    /// <summary>
    /// Ensures proper visibility and z-order of central panels in docked layouts.
    /// Handles complex scenarios where docking, MDI, and TabbedMDIManager interact.
    /// </summary>
    private void EnsureCentralPanelVisibility()
    {
        if (!_useSyncfusionDocking)
        {
            // Fallback: Handle non-docking scenario - visibility only (z-order handled in OnLoad)
            EnsureNonDockingVisibility();
            return;
        }

        try
        {
            // Handle central document panel visibility and z-order
            if (_centralDocumentPanel != null)
            {
                // Ensure the central panel is visible and on top (critical for overlaps)
                try
                {
                    _centralDocumentPanel.Visible = true;
                    _centralDocumentPanel.BringToFront();  // Enforce z-order
                    _centralDocumentPanel.Invalidate();     // Force repaint
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to set central document panel visibility"); }

                // Ensure AI chat control within central panel is visible and on top
                if (_aiChatControl != null)
                {
                    try
                    {
                        _aiChatControl.Visible = true;
                        _aiChatControl.BringToFront();
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to set AI chat control visibility"); }
                }
            }

            // Handle MDI client when both MDI and docking are enabled
            if (_useMdiMode && IsMdiContainer)
            {
                var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
                if (mdiClient != null)
                {
                    // MDI client must be behind central panel to prevent occlusion
                    try
                    {
                        mdiClient.Visible = true;
                        mdiClient.SendToBack();  // Behind central panel
                        mdiClient.Invalidate();   // Force repaint
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to set MDI client visibility"); }

                    // If TabbedMDIManager is active, ensure it works with docking
                    if (_tabbedMdiManager != null)
                    {
                        // The TabbedMDIManager handles MDI child window tabbing
                        // Ensure it doesn't interfere with the central document panel
                        try
                        {
                            // Force refresh of TabbedMDIManager layout
                            var refreshMethod = _tabbedMdiManager.GetType().GetMethod("Refresh");
                            refreshMethod?.Invoke(_tabbedMdiManager, null);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to refresh TabbedMDIManager during visibility adjustment");
                        }
                    }
                }
            }

            // Ensure side panels are behind central content
            if (_leftDockPanel != null)
            {
                try { _leftDockPanel.SendToBack(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to set left dock panel z-order"); }
            }
            if (_rightDockPanel != null)
            {
                try { _rightDockPanel.SendToBack(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to set right dock panel z-order"); }
            }

            // Ensure docked side panels don't obscure central content
            if (_dockingManager != null)
            {
                // Force layout refresh to ensure proper z-order
                this.Refresh();

                // If central panel was set as MDI child, ensure it's properly positioned
                if (_centralDocumentPanel != null && _useMdiMode)
                {
                    try
                    {
                        _dockingManager.SetAsMDIChild(_centralDocumentPanel, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to reassert MDI child status for central panel");
                    }
                }
            }

            // Force form layout update
            this.Refresh();
            this.Invalidate();

            _logger.LogDebug("Central panel visibility ensured for docked layout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure central panel visibility in docked layout");
        }
    }

    /// <summary>
    /// Ensures visibility when docking is disabled (fallback mode).
    /// </summary>
    /// <summary>
    /// Ensure proper z-order for Syncfusion docking mode
    /// Called from OnLoad to guarantee docked panels don't overlap ribbon/status bar
    /// </summary>
    private void EnsureDockingZOrder()
    {
        try
        {
            // Step 1: Ensure central document panel is properly positioned
            if (_centralDocumentPanel != null)
            {
                try { _centralDocumentPanel.Visible = true; } catch (Exception ex) { _logger.LogWarning(ex, "Failed to set central document panel visibility in z-order"); }
                _centralDocumentPanel.BringToFront();

                // Ensure AI chat control within central panel is visible
                if (_aiChatControl != null)
                {
                    try { _aiChatControl.Visible = true; } catch (Exception ex) { _logger.LogWarning(ex, "Failed to set AI chat control visibility in z-order"); }
                    _aiChatControl.BringToFront();
                }
            }

            // Step 2: Handle MDI integration with docking
            if (_useMdiMode && IsMdiContainer && _dockingManager != null)
            {
                var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
                if (mdiClient != null)
                {
                    // MDI client should be behind central content but above docked side panels
                    mdiClient.SendToBack();

                    // Ensure central panel is treated as MDI child if needed
                    if (_centralDocumentPanel != null)
                    {
                        try
                        {
                            _dockingManager.SetAsMDIChild(_centralDocumentPanel, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to set central panel as MDI child during z-order setup");
                        }
                    }
                }
            }

            // Step 3: Ensure docked side panels don't interfere with chrome
            // The DockingManager handles side panel z-order automatically, but we ensure
            // they don't obscure the ribbon/status bar by keeping chrome on top

            _logger.LogDebug("Docking z-order ensured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure docking z-order");
        }
    }

    /// <summary>
    /// Ensure proper visibility for non-docking mode
    /// Called when Syncfusion docking is disabled - handles visibility only
    /// </summary>
    private void EnsureNonDockingVisibility()
    {
        try
        {
            // Handle MDI client if MDI is enabled (z-order handled in OnLoad)
            if (_useMdiMode && IsMdiContainer)
            {
                var mdiClient = this.Controls.OfType<MdiClient>().FirstOrDefault();
                if (mdiClient != null)
                {
                    try { mdiClient.Visible = true; } catch (Exception ex) { _logger.LogWarning(ex, "Failed to set MDI client visibility in non-docking mode"); }
                }
            }

            // Show legacy AI chat panel if docking is disabled
            if (_aiChatPanel != null && !_aiChatPanel.Visible)
            {
                try { _aiChatPanel.Visible = true; } catch (Exception ex) { _logger.LogWarning(ex, "Failed to show AI chat panel in non-docking mode"); }
                _logger.LogDebug("AI chat panel shown (docking disabled)");
            }

            this.Refresh();
            _logger.LogDebug("Non-docking visibility ensured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure non-docking visibility");
        }
    }

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
        if (!_useSyncfusionDocking || _dockingManager == null)
        {
            _logger.LogWarning("Cannot add dynamic dock panel - Syncfusion docking is not enabled");
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
                BackColor = ThemeColors.Background,
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

        // Best-effort: schedule a non-blocking save to persist layout without blocking disposal
        try
        {
            if (this.IsHandleCreated)
            {
                try { this.BeginInvoke(new System.Action(() => { try { SaveDockingLayout(); } catch { } })); }
                catch { }
            }
        }
        catch { }

        if (_dockingManager != null)
        {
            // Capture manager and clear field immediately so callers observe it's gone quickly
            var mgr = _dockingManager;
            _dockingManager = null;

            try
            {
                // Attempt quick detach operations synchronously so owner won't double-dispose
                try { mgr.PersistState = false; } catch { }
                try { mgr.HostControl = null; } catch { }

                try
                {
                    var owner = mgr.Site?.Container;
                    if (owner != null)
                    {
                        try { owner.Remove(mgr); } catch { }
                    }
                }
                catch { }
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

                                try { mgr.Dispose(); } catch (NullReferenceException nre) { _logger?.LogWarning(nre, "NullReferenceException while disposing DockingManager (async); swallowing"); } catch (Exception ex) { _logger?.LogWarning(ex, "Exception while disposing DockingManager (async)"); }
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

                                try { mgr.Dispose(); } catch (NullReferenceException nre) { _logger?.LogWarning(nre, "NullReferenceException while disposing DockingManager (fallback); swallowing"); } catch (Exception ex) { _logger?.LogWarning(ex, "Exception while disposing DockingManager (fallback)"); }
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
                            try { mgr.DockStateChanged -= DockingManager_DockStateChanged; } catch { }
                            try { mgr.DockControlActivated -= DockingManager_DockControlActivated; } catch { }
                            try { mgr.DockVisibilityChanged -= DockingManager_DockVisibilityChanged; } catch { }

                            try { mgr.Dispose(); } catch (NullReferenceException nre) { _logger?.LogWarning(nre, "NullReferenceException while disposing DockingManager (threadpool); swallowing"); } catch (Exception ex) { _logger?.LogWarning(ex, "Exception while disposing DockingManager (threadpool)"); }
                        }
                        catch { }
                    });
                }
            }
            catch { }

            _logger?.LogDebug("_dockingManager cleared and disposal scheduled");
        }

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
            catch { }
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
                catch { }
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



    private void ResetDockingLayout()
    {
        try
        {
            var layoutPath = GetDockingLayoutPath();
            TryDeleteLayoutFiles(layoutPath);

            if (_dockingManager != null)
            {
                _dockingManager.LoadDesignerDockState();
                ApplyThemeToDockingPanels();
            }

            _lastSaveTime = DateTime.MinValue;
            _logger.LogInformation("Docking layout reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset docking layout");
        }
    }

    // Helper methods for dashboard cards (referenced by CreateDashboardCardsPanel)
    private (Panel Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description, Color accent)
    {
        var panel = new Panel
        {
            BackColor = ThemeColors.Background,
            Dock = DockStyle.Top,
            Height = 140,
            Padding = new Padding(12),
            Margin = new Padding(4)
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
            ForeColor = accent,
            Height = 28
        };

        var descriptionLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = ThemeManager.Colors.TextPrimary
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);

        return (panel, descriptionLabel);
    }

    private Panel CreateDashboardCard(string title, string description, Color accent, out Label descriptionLabel)
    {
        var tuple = CreateDashboardCard(title, description, accent);
        descriptionLabel = tuple.DescriptionLabel;
        return tuple.Panel;
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

            if (_useMdiMode)
            {
                ShowChildFormMdi<TForm, TViewModel>(allowMultiple);
                UpdateDockingStateText();
                return;
            }

            // DockingManager remains active for panels; forms are shown via MDI or owned windows.
            ShowNonMdiChildForm<TForm, TViewModel>(allowMultiple);
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

            if (_useMdiMode)
            {
                var childCount = MdiChildren?.Length ?? 0;
                stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"MDI: {childCount} window{(childCount != 1 ? "s" : "")}");

                if (_useTabbedMdi)
                {
                    stateInfo.Append(" (Tabbed)");
                }
            }
            else if (_useSyncfusionDocking)
            {
                stateInfo.Append("Docking Mode");
            }
            else
            {
                stateInfo.Append("Standard Mode");
            }

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    private void ShowNonMdiChildForm<TForm, TViewModel>(bool allowMultiple)
        where TForm : Form
        where TViewModel : class
    {
        try
        {
            if (!allowMultiple && _activeMdiChildren.TryGetValue(typeof(TForm), out var existing))
            {
                if (existing != null && !existing.IsDisposed)
                {
                    try
                    {
                        existing.BringToFront();
                        existing.Activate();
                    }
                    catch { }
                    _logger.LogDebug("Activated existing non-MDI child {FormType}", typeof(TForm).Name);
                    return;
                }

                _activeMdiChildren.Remove(typeof(TForm));
            }

            var scope = _serviceProvider.CreateScope();
            var form = CreateFormInstance<TForm, TViewModel>(scope);

            form.StartPosition = FormStartPosition.CenterParent;
            form.FormClosed += (s, e) =>
            {
                try
                {
                    _activeMdiChildren.Remove(typeof(TForm));
                    scope.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogDebug(disposeEx, "Failed to dispose scope for non-MDI child {FormType}", typeof(TForm).Name);
                }
            };

            if (!allowMultiple)
            {
                _activeMdiChildren[typeof(TForm)] = form;
            }

            form.Show(this);
            _logger.LogInformation("Non-MDI child form {FormType} shown", typeof(TForm).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show non-MDI child form {Form}", typeof(TForm).Name);
        }
    }
}
