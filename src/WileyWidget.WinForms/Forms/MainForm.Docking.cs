using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
using WileyWidget.WinForms.Services;

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
    // Debounce timer for auto-save to prevent I/O spam
    private System.Windows.Forms.Timer? _dockingLayoutSaveTimer;
    // Dictionary to track dynamically added panels
    private Dictionary<string, Panel>? _dynamicDockPanels;

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
            _dockingManager = new DockingManager(components);
            _dockingManager.HostControl = this;
            _dockingManager.EnableDocumentMode = true;
            _dockingManager.PersistState = true;
            _dockingManager.AnimateAutoHiddenWindow = true;
            _dockingManager.AutoHideTabFont = _dockAutoHideTabFont = new Font("Segoe UI", 9f);
            _dockingManager.DockTabFont = _dockTabFont = new Font("Segoe UI", 9f);
            _dockingManager.ShowCaption = true;

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

            // Initialize dynamic panel tracking
            _dynamicDockPanels = new Dictionary<string, Panel>();

            // Apply theme to docked panels
            ApplyThemeToDockingPanels();

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
            BackColor = Color.FromArgb(45, 45, 48),
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
        _dockingManager.SetFloatingMode(_leftDockPanel, true);  // Enable floating windows

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
        _centralDocumentPanel.SendToBack();  // Ensure it's behind docked panels in z-order

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
        _dockingManager.SetFloatingMode(_rightDockPanel, true);  // Enable floating windows

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
            BackColor = Color.FromArgb(45, 45, 48)
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
    /// Implements state persistence using AppStateSerializer with enhanced error handling
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

            // Validate XML structure before loading to catch corruption early
            try
            {
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.Load(layoutPath);
                _logger.LogDebug("Layout XML validated successfully");
            }
            catch (System.Xml.XmlException xmlEx)
            {
                _logger.LogWarning(xmlEx, "Corrupt XML layout file detected at {Path} - deleting and using defaults", layoutPath);
                try
                {
                    File.Delete(layoutPath);
                    _logger.LogInformation("Deleted corrupt layout file");
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
            _dockingManager.LoadDockState(serializer);

            _logger.LogInformation("Docking layout loaded from {Path}", layoutPath);
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
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/layouts
    /// </summary>
    private void SaveDockingLayout()
    {
        if (_dockingManager == null) return;

        string? layoutPath = null;
        try
        {
            layoutPath = GetDockingLayoutPath();
            var layoutDir = Path.GetDirectoryName(layoutPath);

            // Ensure directory exists with permission check
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
                        // Fallback to temp directory if AppData is restricted
                        layoutPath = Path.Combine(Path.GetTempPath(), DockingLayoutFileName);
                        _logger.LogWarning("AppData directory creation failed - using temp directory: {Path}", layoutPath);
                    }
                }
                // Verify write permission by attempting to open file for write
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

            // Use Syncfusion's AppStateSerializer for proper state persistence
            var serializer = new Syncfusion.Runtime.Serialization.AppStateSerializer(
                Syncfusion.Runtime.Serialization.SerializeMode.XMLFile, layoutPath);
            _dockingManager.SaveDockState(serializer);

            _logger.LogInformation("Docking layout saved to {Path}", layoutPath);
        }
        catch (UnauthorizedAccessException authEx)
        {
            _logger.LogError(authEx, "Permission denied saving docking layout to {Path}", layoutPath);
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "I/O error saving docking layout to {Path}", layoutPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save docking layout to {Path}", layoutPath);
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
    /// Apply Syncfusion theme to docked panels using ThemeManagerService
    /// Integrates with SkinManager to ensure visual consistency
    /// </summary>
    private void ApplyThemeToDockingPanels()
    {
        try
        {
            var themeService = _serviceProvider.GetService<IThemeManagerService>();
            if (themeService == null)
            {
                _logger.LogWarning("ThemeManagerService not available - using hardcoded panel colors");
                return;
            }

            var currentTheme = themeService.GetCurrentTheme();
            _logger.LogDebug("Applying theme '{Theme}' to docking panels", currentTheme);

            // Apply semantic colors to left dock panel (dashboard)
            if (_leftDockPanel != null)
            {
                _leftDockPanel.BackColor = themeService.GetSemanticColor(SemanticColorType.CardBackground);
                _leftDockPanel.ForeColor = themeService.GetSemanticColor(SemanticColorType.Foreground);
                _logger.LogDebug("Applied theme to left dock panel");
            }

            // Apply semantic colors to right dock panel (activity)
            if (_rightDockPanel != null)
            {
                _rightDockPanel.BackColor = themeService.GetSemanticColor(SemanticColorType.Background);
                _rightDockPanel.ForeColor = themeService.GetSemanticColor(SemanticColorType.Foreground);
                _logger.LogDebug("Applied theme to right dock panel");
            }

            // Apply semantic colors to central document panel
            if (_centralDocumentPanel != null)
            {
                _centralDocumentPanel.BackColor = themeService.GetSemanticColor(SemanticColorType.Background);
                _centralDocumentPanel.ForeColor = themeService.GetSemanticColor(SemanticColorType.Foreground);
                _logger.LogDebug("Applied theme to central document panel");
            }

            // Apply theme to DockingManager's internal controls if VisualStyle property exists
            if (_dockingManager != null && themeService.IsSkinManagerAvailable())
            {
                try
                {
                    // Attempt to set VisualStyle property (available in some Syncfusion versions)
                    var visualStyleProp = _dockingManager.GetType().GetProperty("VisualStyle");
                    if (visualStyleProp != null && visualStyleProp.CanWrite)
                    {
                        // Map theme name to Syncfusion.Windows.Forms.VisualStyle enum
                        var visualStyle = MapThemeToVisualStyle(currentTheme);
                        visualStyleProp.SetValue(_dockingManager, visualStyle);
                        _logger.LogInformation("Applied VisualStyle '{Style}' to DockingManager", visualStyle);
                    }
                    else
                    {
                        _logger.LogDebug("DockingManager.VisualStyle property not available in this Syncfusion version");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set DockingManager.VisualStyle - not critical");
                }
            }

            // Apply theme to all Syncfusion controls within docked panels
            if (_leftDockPanel != null)
            {
                themeService.ApplyThemeToAllControls(_leftDockPanel, currentTheme);
            }
            if (_rightDockPanel != null)
            {
                themeService.ApplyThemeToAllControls(_rightDockPanel, currentTheme);
            }

            _logger.LogInformation("Successfully applied theme to all docking panels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme to docking panels - using default colors");
        }
    }

    /// <summary>
    /// Map theme name to Syncfusion.Windows.Forms.VisualStyle enum value
    /// </summary>
    private static object MapThemeToVisualStyle(string themeName)
    {
        // Use reflection to get VisualStyle enum values (version-agnostic)
        var visualStyleType = Type.GetType("Syncfusion.Windows.Forms.VisualStyle, Syncfusion.Shared.Base");
        if (visualStyleType == null)
        {
            return 0; // Default to first enum value
        }

        // Map theme names to VisualStyle enum values
        var styleMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Office2019Colorful", "Office2019Colorful" },
            { "Office2019White", "Office2019White" },
            { "Office2019Black", "Office2019Black" },
            { "Office2019DarkGray", "Office2019DarkGray" },
            { "Office2016Colorful", "Office2016Colorful" },
            { "Office2016White", "Office2016White" },
            { "Office2016Black", "Office2016Black" },
            { "Office2016DarkGray", "Office2016DarkGray" },
            { "MaterialLight", "Office2016Colorful" },  // Fallback
            { "MaterialDark", "Office2016Black" },      // Fallback
            { "HighContrastBlack", "Office2016Black" }  // Fallback
        };

        var enumValueName = styleMapping.ContainsKey(themeName) ? styleMapping[themeName] : "Office2019Colorful";

        try
        {
            return Enum.Parse(visualStyleType, enumValueName);
        }
        catch
        {
            return Enum.GetValues(visualStyleType).GetValue(0) ?? 0;
        }
    }

    #endregion

    #region Docking Event Handlers

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        // Log docking state changes
        _logger.LogDebug("Dock state changed: NewState={NewState}, OldState={OldState}",
            e.NewState, e.OldState);

        // Auto-save layout on state changes with debouncing to prevent I/O spam
        if (_useSyncfusionDocking)
        {
            DebouncedSaveDockingLayout();
        }
    }

    /// <summary>
    /// Debounced save mechanism - waits 500ms after last state change before saving
    /// Prevents I/O spam during rapid docking operations (e.g., dragging, resizing)
    /// </summary>
    private void DebouncedSaveDockingLayout()
    {
        // Stop existing timer if running
        _dockingLayoutSaveTimer?.Stop();

        // Initialize timer on first use
        if (_dockingLayoutSaveTimer == null)
        {
            _dockingLayoutSaveTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _dockingLayoutSaveTimer.Tick += OnSaveTimerTick;
        }

        // Restart timer - will fire after 500ms of no state changes
        _dockingLayoutSaveTimer.Start();
    }

    /// <summary>
    /// Timer tick handler - performs actual save after debounce period
    /// </summary>
    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _dockingLayoutSaveTimer?.Stop();

        try
        {
            SaveDockingLayout();
            _logger.LogDebug("Debounced auto-save completed");
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

        try
        {
            var panel = new Panel
            {
                Name = panelName,
                BackColor = Color.White,
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
            _dockingManager.SetFloatingMode(panel, true);

            // Apply theme
            var themeService = _serviceProvider.GetService<IThemeManagerService>();
            if (themeService != null)
            {
                panel.BackColor = themeService.GetSemanticColor(SemanticColorType.Background);
                panel.ForeColor = themeService.GetSemanticColor(SemanticColorType.Foreground);
                themeService.ApplyThemeToAllControls(panel, themeService.GetCurrentTheme());
            }

            // Track panel
            _dynamicDockPanels ??= new Dictionary<string, Panel>();
            _dynamicDockPanels[panelName] = panel;

            _logger.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
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
}
