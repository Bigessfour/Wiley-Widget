using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Runtime.Serialization;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;

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
    private bool _useSyncfusionDocking = false;  // Feature flag - set true to enable
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";
    // Fonts used by DockingManager - keep references so we can dispose them
    private Font? _dockAutoHideTabFont;
    private Font? _dockTabFont;

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
            _logger.LogInformation("Initializing Syncfusion DockingManager (Phase 2)");

            // Create IContainer if it doesn't exist
            if (components == null)
            {
                components = new System.ComponentModel.Container();
                _logger.LogDebug("Created IContainer for DockingManager");
            }

            // Instantiate DockingManager per official Syncfusion API
            _dockingManager = new DockingManager(components)
            {
                HostControl = this,
                EnableDocumentMode = true,
                PersistState = true,
                AnimateAutoHiddenWindow = true,
                AutoHideTabFont = _dockAutoHideTabFont = new Font("Segoe UI", 9f),
                DockTabFont = _dockTabFont = new Font("Segoe UI", 9f),
                ShowCaption = true
            };

            // Subscribe to events for state tracking and logging
            _dockingManager.DockStateChanged += DockingManager_DockStateChanged;
            _dockingManager.DockControlActivated += DockingManager_DockControlActivated;
            _dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;

            // Create dock panels
            CreateLeftDockPanel();
            CreateCentralDocumentPanel();
            CreateRightDockPanel();

            // Hide standard panels when using docking
            HideStandardPanelsForDocking();

            // Load saved layout
            LoadDockingLayout();

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
            ShowStandardPanelsAfterDockingFailure();
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
            ShowStandardPanelsAfterDockingFailure();
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
            BackColor = Color.FromArgb(245, 245, 250),
            AutoScroll = true
        };

        // Move dashboard cards to left dock (reuse existing dashboard panel logic)
        var dashboardContent = CreateDashboardCardsPanel();
        _leftDockPanel.Controls.Add(dashboardContent);

        // Configure docking behavior
        _dockingManager.SetEnableDocking(_leftDockPanel, true);
        _dockingManager.DockControl(_leftDockPanel, this, DockingStyle.Left, 250);
        _dockingManager.SetAutoHideMode(_leftDockPanel, true);  // Collapsible
        _dockingManager.SetDockLabel(_leftDockPanel, "📊 Dashboard");

        _logger.LogDebug("Left dock panel created with dashboard cards");
    }

    /// <summary>
    /// Create central document panel with AI chat as primary tab
    /// </summary>
    private void CreateCentralDocumentPanel()
    {
        if (_dockingManager == null || _aiChatControl == null) return;

        _centralDocumentPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // Add AI Chat as primary document
        _centralDocumentPanel.Controls.Add(_aiChatControl);
        _aiChatControl.Dock = DockStyle.Fill;

        // IMPORTANT: When EnableDocumentMode = true and HostControl is set,
        // the central fill area should NOT be docked via DockControl() with DockingStyle.Fill.
        // Syncfusion DockingManager explicitly prohibits docking with Fill style to the host control.
        // Instead, add the panel directly to the form's Controls collection with standard WinForms docking.
        // Side panels (Left, Right, Top, Bottom) use DockControl(), the center uses standard Fill docking.
        Controls.Add(_centralDocumentPanel);
        _centralDocumentPanel.BringToFront();  // Ensure it's behind docked panels visually

        _logger.LogDebug("Central document panel created with AI chat (using standard Fill docking, not DockingManager)");
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
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        // Move activity grid to right dock (reuse existing activity panel logic)
        var activityContent = CreateActivityGridPanel();
        _rightDockPanel.Controls.Add(activityContent);

        // Configure docking behavior
        _dockingManager.SetEnableDocking(_rightDockPanel, true);
        _dockingManager.DockControl(_rightDockPanel, this, DockingStyle.Right, 200);
        _dockingManager.SetAutoHideMode(_rightDockPanel, true);  // Collapsible
        _dockingManager.SetDockLabel(_rightDockPanel, "📋 Activity");

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
            BackColor = Color.FromArgb(245, 245, 250)
        };

        // Add row styles
        for (int i = 0; i < 5; i++)
        {
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }

        // Create cards (reuse existing logic from InitializeComponent)
        var accountsCard = CreateDashboardCard("📊 Accounts", "Loading...", Color.FromArgb(66, 133, 244), out _accountsDescLabel);
        SetupCardClickHandler(accountsCard, () => ShowChildForm<AccountsForm, AccountsViewModel>());

        var chartsCard = CreateDashboardCard("📈 Charts", "Loading...", Color.FromArgb(52, 168, 83), out _chartsDescLabel);
        SetupCardClickHandler(chartsCard, () => ShowChildForm<ChartForm, ChartViewModel>());

        var settingsCard = CreateDashboardCard("⚙️ Settings", "Loading...", Color.FromArgb(251, 188, 4), out _settingsDescLabel);
        SetupCardClickHandler(settingsCard, () => ShowChildForm<SettingsForm, SettingsViewModel>());

        var reportsCard = CreateDashboardCard("📄 Reports", "Loading...", Color.FromArgb(156, 39, 176), out _reportsDescLabel);
        SetupCardClickHandler(reportsCard, () => ShowChildForm<ReportsForm, ReportsViewModel>());

        var infoCard = CreateDashboardCard("ℹ️ Budget Status", "Loading...", Color.FromArgb(234, 67, 53), out _infoDescLabel);

        dashboardPanel.Controls.Add(accountsCard, 0, 0);
        dashboardPanel.Controls.Add(chartsCard, 0, 1);
        dashboardPanel.Controls.Add(settingsCard, 0, 2);
        dashboardPanel.Controls.Add(reportsCard, 0, 3);
        dashboardPanel.Controls.Add(infoCard, 0, 4);

        return dashboardPanel;
    }

    /// <summary>
    /// Create activity grid panel (extracted for reuse in docking)
    /// </summary>
    private Panel CreateActivityGridPanel()
    {
        var activityPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        var activityHeader = new Label
        {
            Text = "📋 Recent Activity",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(5, 8, 0, 0)
        };

        var activityGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            ShowGroupDropArea = false,
            RowHeight = 36,
            AllowSorting = true,
            AllowFiltering = true
        };

        // Map to ActivityItem properties
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridDateTimeColumn { MappingName = "Timestamp", HeaderText = "Time", Format = "HH:mm", Width = 80 });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Action", Width = 150 });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details", Width = 200 });

        // Typed sample ActivityItem fallback
        var activities = new[]
        {
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-5), Activity = "Account Updated", Details = "GL-1001", User = "System" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-15), Activity = "Report Generated", Details = "Budget Q4", User = "Scheduler" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddMinutes(-30), Activity = "QuickBooks Sync", Details = "42 records", User = "Integrator" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddHours(-1), Activity = "User Login", Details = "Admin", User = "Admin" },
            new WileyWidget.Models.ActivityItem { Timestamp = DateTime.Now.AddHours(-2), Activity = "Backup Complete", Details = "12.5 MB", User = "System" }
        };
        activityGrid.DataSource = activities;

        activityPanel.Controls.Add(activityGrid);
        activityPanel.Controls.Add(activityHeader);

        return activityPanel;
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
                control.Visible = false;
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
                control.Visible = true;
            }
        }
        _logger.LogDebug("Standard panels restored after docking failure");
    }

    /// <summary>
    /// Load saved docking layout from AppData
    /// Implements state persistence using AppStateSerializer
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/layouts
    /// </summary>
    private void LoadDockingLayout()
    {
        if (_dockingManager == null) return;

        try
        {
            var layoutPath = GetDockingLayoutPath();
            if (!File.Exists(layoutPath))
            {
                _logger.LogDebug("No saved docking layout found at {Path} - using default layout", layoutPath);
                return;
            }

            // Use Syncfusion's AppStateSerializer for proper state loading
            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, layoutPath);
            _dockingManager.LoadDockState(serializer);

            _logger.LogInformation("Docking layout loaded from {Path}", layoutPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load docking layout - using default layout");
        }
    }

    /// <summary>
    /// Save current docking layout to AppData
    /// Implements state persistence using Syncfusion DockingManager serialization
    /// Captures: panel positions, sizes, docking states, floating window states, tab order
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/layouts
    /// </summary>
    private void SaveDockingLayout()
    {
        if (_dockingManager == null) return;

        try
        {
            var layoutPath = GetDockingLayoutPath();
            var layoutDir = Path.GetDirectoryName(layoutPath);

            // Ensure directory exists
            if (!string.IsNullOrEmpty(layoutDir) && !Directory.Exists(layoutDir))
            {
                Directory.CreateDirectory(layoutDir);
                _logger.LogDebug("Created docking layout directory at {Dir}", layoutDir);
            }

            // Use Syncfusion's AppStateSerializer for proper state persistence
            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, layoutPath);
            _dockingManager.SaveDockState(serializer);

            _logger.LogInformation("Docking layout saved to {Path}", layoutPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save docking layout");
        }
    }    /// <summary>
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

    #region Docking Event Handlers

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        // Log docking state changes
        _logger.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Auto-save layout on state changes (debounced via timer would be better for production)
        if (_useSyncfusionDocking)
        {
            try
            {
                SaveDockingLayout();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-save docking layout after state change");
            }
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
    }

    #endregion

    /// <summary>
    /// Override FormClosing to save docking layout before exit
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_useSyncfusionDocking)
        {
            SaveDockingLayout();
        }
        base.OnFormClosing(e);
    }

    /// <summary>
    /// Dispose resources owned by the docking implementation
    /// Extracted from the original Dispose override to avoid duplicate overrides
    /// and be callable from the single Dispose override in the main partial.
    /// </summary>
    private void DisposeSyncfusionDockingResources()
    {
        if (_dockingManager != null)
        {
            try
            {
                SaveDockingLayout();
            }
            catch
            {
                // Swallow failures during disposal; nothing we can do safely here
            }

            _dockingManager.DockStateChanged -= DockingManager_DockStateChanged;
            _dockingManager.DockControlActivated -= DockingManager_DockControlActivated;
            _dockingManager.DockVisibilityChanged -= DockingManager_DockVisibilityChanged;

            _dockingManager.Dispose();
            _dockingManager = null;
        }

        _leftDockPanel?.Dispose();
        _leftDockPanel = null;

        _rightDockPanel?.Dispose();
        _rightDockPanel = null;

        _centralDocumentPanel?.Dispose();
        _centralDocumentPanel = null;
    }
}
