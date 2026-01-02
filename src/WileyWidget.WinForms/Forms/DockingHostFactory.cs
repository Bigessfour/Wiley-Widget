using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring Syncfusion DockingManager host.
/// Extracts docking initialization logic from MainForm for better testability and maintainability.
/// </summary>
public static class DockingHostFactory
{
    private const string SegoeUiFontName = "Segoe UI";

    /// <summary>
    /// Create and configure DockingManager with all docking panels.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="panelNavigator">Panel navigation service</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (DockingManager, leftPanel, centralPanel, rightPanel, activityGrid, activityRefreshTimer)</returns>
    public static (
        DockingManager dockingManager,
        Panel leftDockPanel,
        Panel centralDocumentPanel,
        Panel rightDockPanel,
        Syncfusion.WinForms.DataGrid.SfDataGrid? activityGrid,
        System.Windows.Forms.Timer? activityRefreshTimer
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            logger?.LogInformation(
                "CreateDockingHost start - FormSize={Width}x{Height}, HandleCreated={HandleCreated}",
                mainForm.Width,
                mainForm.Height,
                mainForm.IsHandleCreated);

            // Initialize DockingManager
            var dockingManager = CreateDockingManager(mainForm, logger);

            // Create panels
            var leftPanel = CreateLeftDockPanel(dockingManager, mainForm, panelNavigator, logger);
            var centralPanel = CreateCentralDocumentPanel(mainForm, logger);
            var (rightPanel, activityGrid, activityTimer) = CreateRightDockPanel(dockingManager, mainForm, serviceProvider, logger);

            stopwatch.Stop();
            logger?.LogInformation("DockingManager initialized successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            logger?.LogDebug(
                "Docking host created with panels - Left={LeftName}, Central={CentralName}, Right={RightName}",
                leftPanel.Name,
                centralPanel.Name,
                rightPanel.Name);

            return (dockingManager, leftPanel, centralPanel, rightPanel, activityGrid, activityTimer);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogError(ex, "DockingManager initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Create and configure DockingManager component with proper Office2019 theme integration.
    /// Follows Syncfusion best practices for SfSkinManager theme application.
    /// </summary>
    private static DockingManager CreateDockingManager(MainForm mainForm, ILogger? logger)
    {
        var dockingManager = new DockingManager(mainForm.components ?? new System.ComponentModel.Container())
        {
            HostControl = mainForm,
            EnableDocumentMode = false, // Panel mode only
            PersistState = true,
            AnimateAutoHiddenWindow = true,
            ShowCaption = true,
            EnableAutoAdjustCaption = true, // Auto-size captions based on font
        };

        // CRITICAL: Apply SfSkinManager theme to DockingManager
        // This ensures theme cascade to all docked panels and controls
        // Theme name comes from SfSkinManager.ApplicationVisualTheme (set globally in Program.InitializeTheme)
        var currentTheme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        try
        {
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(dockingManager, currentTheme);
            logger?.LogInformation("DockingManager theme applied via SfSkinManager: {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to apply SfSkinManager theme to DockingManager - using default theme");
        }

        // Configure fonts for docking UI elements (tabs, auto-hide tabs)
        // Theme-aware fonts ensure consistency with Office2019 theme
        dockingManager.DockTabFont = new Font(SegoeUiFontName, 9F, FontStyle.Regular);
        dockingManager.AutoHideTabFont = new Font(SegoeUiFontName, 9F, FontStyle.Regular);

        // Set stable name for tooling/tests
        try
        {
            var nameProp = dockingManager.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(dockingManager, "DockingManager_Main");
            }
        }
        catch { /* Name setting is optional */ }

        logger?.LogInformation("DockingManager initialized with Office2019 theme integration (EnableDocumentMode=false)");
        logger?.LogDebug(
            "DockingManager config: Theme={Theme}, ShowCaption={ShowCaption}, EnableAutoAdjustCaption={AutoAdjust}",
            currentTheme,
            dockingManager.ShowCaption,
            dockingManager.EnableAutoAdjustCaption);

        return dockingManager;
    }

    /// <summary>
    /// Create left dock panel with dashboard cards.
    /// </summary>
    private static Panel CreateLeftDockPanel(
        DockingManager dockingManager,
        MainForm mainForm,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
    {
        // Guard: Ensure required dependencies are provided
        // Navigator can be null at dock creation time (initialized post-docking)
        // Logger fallback to NullLogger for safe operation
        var safeLogger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var leftPanel = new Panel
        {
            Name = "LeftDockPanel",
            AccessibleName = "LeftDockPanel",
            AutoScroll = true,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(8, 8, 8, 8),
            Visible = true // Explicitly set visible
        };

        Console.WriteLine($"[DIAGNOSTIC] CreateLeftDockPanel: Created {leftPanel.Name}, Visible={leftPanel.Visible}");

        // Note: panelNavigator may be null here - dashboard cards will be inert until
        // EnsurePanelNavigatorInitialized() is called after docking setup completes
        var dashboardContent = DashboardFactory.CreateDashboardPanel(panelNavigator, mainForm, safeLogger);
        leftPanel.Controls.Add(dashboardContent);

        ConfigurePanelDocking(dockingManager, leftPanel, mainForm, DockingStyle.Left, 280, "Dashboard", safeLogger);

        safeLogger.LogInformation("Left dock panel created and docked (dashboard)");
        safeLogger.LogDebug("Left dock panel ready (navigator: {NavigatorStatus})",
            panelNavigator == null ? "pending" : "ready");
        return leftPanel;
    }

    /// <summary>
    /// Create central document panel for main content area.
    /// Phase 1: Central panel is NOT added to MainForm.Controls - DockingManager handles panel layout.
    /// </summary>
    private static Panel CreateCentralDocumentPanel(MainForm mainForm, ILogger? logger)
    {
        var centralPanel = new Panel
        {
            Name = "CentralDocumentPanel",
            AccessibleName = "CentralDocumentPanel",
            Dock = DockStyle.None, // Changed from Fill - let DockingManager handle layout
            Visible = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.Transparent
        };

        // CRITICAL FIX: Do NOT add central panel to MainForm.Controls
        // Adding it causes conflicts with DockingManager's panel hosting.
        // The DockingManager uses MainForm as HostControl and manages all docked panels.
        // mainForm.Controls.Add(centralPanel); // REMOVED

        logger?.LogInformation("Central document panel created (managed by DockingManager, not added to MainForm.Controls)");
        logger?.LogDebug("Central document panel created (layout delegated to DockingManager)");

        Console.WriteLine($"[DIAGNOSTIC] CreateCentralDocumentPanel: Created {centralPanel.Name}, NOT added to MainForm.Controls");
        return centralPanel;
    }

    /// <summary>
    /// Create right dock panel with activity grid.
    /// </summary>
    private static (Panel panel, Syncfusion.WinForms.DataGrid.SfDataGrid? grid, System.Windows.Forms.Timer? timer) CreateRightDockPanel(
        DockingManager dockingManager,
        MainForm mainForm,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        var rightPanel = new Panel
        {
            Name = "RightDockPanel",
            AccessibleName = "RightDockPanel",
            Padding = new Padding(8, 8, 8, 8),
            BorderStyle = BorderStyle.None,
            Visible = true // Explicitly set visible
        };

        Console.WriteLine($"[DIAGNOSTIC] CreateRightDockPanel: Created {rightPanel.Name}, Visible={rightPanel.Visible}");

        var (activityContent, activityGrid, activityTimer) = CreateActivityGridPanel(serviceProvider, logger);
        rightPanel.Controls.Add(activityContent);

        ConfigurePanelDocking(dockingManager, rightPanel, mainForm, DockingStyle.Right, 280, "Activity", logger);

        logger?.LogInformation("Right dock panel created with activity grid");
        return (rightPanel, activityGrid, activityTimer);
    }

    /// <summary>
    /// Configure docking behavior for a panel.
    /// </summary>
    private static void ConfigurePanelDocking(
        DockingManager dockingManager,
        Panel panel,
        MainForm mainForm,
        DockingStyle style,
        int size,
        string label,
        ILogger? logger)
    {
        Console.WriteLine($"[DIAGNOSTIC] ConfigurePanelDocking: Configuring {panel.Name} - style={style}, size={size}, label={label}");

        dockingManager.SetEnableDocking(panel, true);
        Console.WriteLine($"[DIAGNOSTIC] ConfigurePanelDocking: EnableDocking set for {panel.Name}");

        dockingManager.DockControl(panel, mainForm, style, size);
        Console.WriteLine($"[DIAGNOSTIC] ConfigurePanelDocking: DockControl called for {panel.Name}");

        dockingManager.SetAutoHideMode(panel, true);
        dockingManager.SetDockLabel(panel, label);
        dockingManager.SetAllowFloating(panel, true);

        // CRITICAL: Make panel visible after docking
        panel.Visible = true;
        Console.WriteLine($"[DIAGNOSTIC] ConfigurePanelDocking: Final state - {panel.Name} Visible={panel.Visible}, Parent={panel.Parent?.Name}");

        logger?.LogDebug(
            "Docked panel {PanelName}: style={Style}, size={Size}, allowFloating=true, autoHide=true, visible=true",
            panel.Name,
            style,
            size);

        try
        {
            dockingManager.SetControlMinimumSize(panel, new Size(200, 0));
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set minimum size for panel {PanelName}", panel.Name);
        }
    }



    /// <summary>
    /// Create activity grid panel with recent activity tracking.
    /// </summary>
    private static (Panel panel, Syncfusion.WinForms.DataGrid.SfDataGrid grid, System.Windows.Forms.Timer timer) CreateActivityGridPanel(
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        var activityPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var activityHeader = new Label
        {
            Text = "Recent Activity",
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(5, 8, 0, 0)
        };

        var activityGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Name = "ActivityDataGrid",
            AccessibleName = "ActivityDataGrid",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            ShowGroupDropArea = false,
            RowHeight = 36,
            AllowSorting = true,
            AllowFiltering = true,
            SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single
        };

        logger?.LogDebug(
            "Activity grid created: RowHeight={RowHeight}, Columns={ColumnCount}",
            activityGrid.RowHeight,
            4);

        // Configure grid styling
        activityGrid.Style.HeaderStyle.Font = new Syncfusion.WinForms.DataGrid.Styles.GridFontInfo(
            new System.Drawing.Font(SegoeUiFontName, 9.5F, System.Drawing.FontStyle.Bold));
        activityGrid.Style.CellStyle.Font = new Syncfusion.WinForms.DataGrid.Styles.GridFontInfo(
            new System.Drawing.Font(SegoeUiFontName, 9F));

        // Configure columns
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridDateTimeColumn
        {
            MappingName = "Timestamp",
            HeaderText = "Time",
            Format = "HH:mm",
            Width = 70,
            MinimumWidth = 60
        });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "Activity",
            HeaderText = "Action",
            Width = 100,
            MinimumWidth = 80,
            AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill
        });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "Details",
            HeaderText = "Details",
            Width = 0,
            AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill
        });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
        {
            MappingName = "User",
            HeaderText = "User",
            Width = 80,
            MinimumWidth = 60
        });

        // Load initial data
        _ = LoadActivityDataAsync(activityGrid, serviceProvider, logger);

        // Setup auto-refresh timer with disposal guard
        var activityTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000 // 30 seconds
        };
        activityTimer.Tick += async (s, e) =>
        {
            // Guard: Skip refresh if grid is disposed (prevents ObjectDisposedException)
            if (activityGrid?.IsDisposed ?? true)
            {
                logger?.LogDebug("Activity refresh skipped - grid is disposed");
                return;
            }

            logger?.LogDebug("Activity refresh starting - Grid disposed: {Disposed}", activityGrid?.IsDisposed);
            await LoadActivityDataAsync(activityGrid, serviceProvider, logger);
        };
        activityTimer.Start();

        logger?.LogInformation("Activity timer started with interval {IntervalMs}ms", activityTimer.Interval);

        activityPanel.Controls.Add(activityGrid);
        activityPanel.Controls.Add(activityHeader);

        return (activityPanel, activityGrid, activityTimer);
    }

    /// <summary>
    /// Load activity data from database asynchronously.
    /// Uses AsNoTracking() for read-only queries to prevent ObjectDisposedException after scope disposal.
    /// Repository method projects to detached ActivityItem DTOs suitable for UI binding.
    /// </summary>
    private static async Task LoadActivityDataAsync(
        Syncfusion.WinForms.DataGrid.SfDataGrid? activityGrid,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        // Early disposal guard: check if grid is already disposed before starting async work
        if (activityGrid == null || activityGrid.IsDisposed)
        {
            logger?.LogDebug("Activity grid is disposed, skipping data load");
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var activityLogRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<IActivityLogRepository>(scope.ServiceProvider);

            if (activityLogRepository == null)
            {
                logger?.LogWarning("ActivityLogRepository not available, using fallback data");
                LoadFallbackActivityData(activityGrid);
                return;
            }

            // Call repository method - it already uses .AsNoTracking() and projects to ActivityItem DTOs
            // ConfigureAwait(false) since we're not awaiting on UI thread
            var activities = await activityLogRepository.GetRecentActivitiesAsync(skip: 0, take: 50).ConfigureAwait(false);

            // Defensive null/disposal check before grid binding
            // Grid may have been disposed while async operation was in flight
#pragma warning disable CA1508 // Dead code analysis - defensive check for async disposal race condition
            if (activityGrid == null || activityGrid.IsDisposed)
#pragma warning restore CA1508
            {
                logger?.LogDebug("Activity grid disposed during data load, discarding {Count} results", activities.Count);
                return;
            }

            // Activities list is fully materialized before scope disposal
            // ActivityItem DTOs are detached from EF context, safe for UI binding
            logger?.LogDebug("Loaded {Count} activities with AsNoTracking for UI binding", activities.Count);

            // Thread-safe grid binding
            if (activityGrid.InvokeRequired)
            {
                activityGrid.Invoke(() =>
                {
                    // Final disposal check inside UI thread invoke
                    if (!activityGrid.IsDisposed)
                    {
                        activityGrid.DataSource = activities;
                    }
                });
            }
            else
            {
                if (!activityGrid.IsDisposed)
                {
                    activityGrid.DataSource = activities;
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Activity grid disposed during load operation - this should not occur with AsNoTracking()");
            // Don't load fallback - grid is disposed anyway
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load activity data from database");
            LoadFallbackActivityData(activityGrid);
        }
    }

    /// <summary>
    /// Load fallback activity data when repository is unavailable.
    /// </summary>
    private static void LoadFallbackActivityData(Syncfusion.WinForms.DataGrid.SfDataGrid activityGrid)
    {
        if (activityGrid == null || activityGrid.IsDisposed)
            return;

        var activities = new[]
        {
            new WileyWidget.Models.ActivityItem
            {
                Timestamp = DateTime.Now.AddMinutes(-5),
                Activity = "Account Updated",
                Details = "GL-1001",
                User = "System"
            },
            new WileyWidget.Models.ActivityItem
            {
                Timestamp = DateTime.Now.AddMinutes(-15),
                Activity = "Report Generated",
                Details = "Budget Q4",
                User = "Scheduler"
            },
            new WileyWidget.Models.ActivityItem
            {
                Timestamp = DateTime.Now.AddMinutes(-30),
                Activity = "QuickBooks Sync",
                Details = "42 records",
                User = "Integrator"
            },
            new WileyWidget.Models.ActivityItem
            {
                Timestamp = DateTime.Now.AddHours(-1),
                Activity = "User Login",
                Details = "Admin",
                User = "Admin"
            },
            new WileyWidget.Models.ActivityItem
            {
                Timestamp = DateTime.Now.AddHours(-2),
                Activity = "Backup Complete",
                Details = "12.5 MB",
                User = "System"
            }
        };

        if (activityGrid.InvokeRequired)
        {
            activityGrid.Invoke(() => activityGrid.DataSource = activities);
        }
        else
        {
            activityGrid.DataSource = activities;
        }
    }
}
