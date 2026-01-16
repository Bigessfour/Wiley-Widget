using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.Models;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

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
    /// <returns>Tuple of (DockingManager, leftPanel, rightPanel, activityGrid, activityRefreshTimer)</returns>
    /// <remarks>No central panel - pure docking panel architecture per Option A design</remarks>
    public static (
        DockingManager dockingManager,
        GradientPanelExt leftDockPanel,
        GradientPanelExt rightDockPanel,
        Syncfusion.WinForms.DataGrid.SfDataGrid? activityGrid,
        System.Windows.Forms.Timer? activityRefreshTimer
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
    {
        if (mainForm == null)
            throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        logger?.LogInformation("★ CreateDockingHost START - Creating DockingManager and panels");

        if (mainForm.IsDisposed)
        {
            logger?.LogWarning("MainForm is already disposed; skipping docking host creation.");
            return (new DockingManager(), new GradientPanelExt(), new GradientPanelExt(), null, null);
        }

        // Initialize DockingManager
        logger?.LogInformation("→ Creating DockingManager instance...");
        var dockingManager = new DockingManager
        {
            DockToFill = true,
            ThemeName = "Office2019Colorful",  // Rely on SfSkinManager; set theme here if needed
            HostControl = mainForm,  // CRITICAL: Set HostControl to mainForm so docking knows where to anchor panels
            PersistState = true      // Enable persistence support
        };
        logger?.LogInformation("→ DockingManager created successfully with HostControl={HostName}", mainForm.Name);

        // Create left dock panel
        logger?.LogInformation("→ Creating LeftDockPanel...");
        var leftDockPanel = new GradientPanelExt
        {
            // Dock = DockStyle.Left, // REMOVED: Conflics with DockingManager
            Width = 300,
            Name = "LeftDockPanel",
            AccessibleName = "Navigation panel",
            AccessibleDescription = "Left docked panel for navigation content",
            AccessibleRole = AccessibleRole.Pane,
            TabStop = false,
            TabIndex = 10
        };
        if (!TryDockControl(dockingManager, leftDockPanel, mainForm, DockingStyle.Left, 300, logger))
        {
            logger?.LogWarning("Unable to dock left panel; docking host creation aborted.");
            return (dockingManager, leftDockPanel, new GradientPanelExt(), null, null);
        }
        // Force visibility explicitly as requested
        dockingManager.SetDockVisibility(leftDockPanel, true);
        logger?.LogInformation("SetDockVisibility(true) for LeftDockPanel");
        // DO NOT Invalidate/Update here - paint must be deferred until all panels are docked

        // Apply official API: SetDockLabel for visual dock header
        try
        {
            dockingManager.SetDockLabel(leftDockPanel, "Navigation");
            logger?.LogDebug("Set dock label for left panel: 'Navigation'");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set dock label for left panel; continuing");
        }

        // Apply official API: SetAutoHideMode for space-saving capability
        try
        {
            dockingManager.SetAutoHideMode(leftDockPanel, true);
            logger?.LogDebug("Enabled auto-hide mode for left panel");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to enable auto-hide for left panel; continuing");
        }

        // Create right dock panel
        logger?.LogInformation("→ Creating RightDockPanel...");
        var rightDockPanel = new GradientPanelExt
        {
            // Dock = DockStyle.Right, // REMOVED: Conflicts with DockingManager
            Width = 300,
            Name = "RightDockPanel",
            AccessibleName = "Activity panel",
            AccessibleDescription = "Right docked panel hosting activity and insights",
            AccessibleRole = AccessibleRole.Pane,
            TabStop = false,
            TabIndex = 11
        };
        if (!TryDockControl(dockingManager, rightDockPanel, mainForm, DockingStyle.Right, 300, logger))
        {
            logger?.LogWarning("Unable to dock right panel; docking host creation aborted.");
            return (dockingManager, leftDockPanel, rightDockPanel, null, null);
        }
        // Force visibility explicitly as requested
        dockingManager.SetDockVisibility(rightDockPanel, true);
        logger?.LogInformation("SetDockVisibility(true) for RightDockPanel");

        // Apply official API: SetDockLabel for visual dock header
        try
        {
            dockingManager.SetDockLabel(rightDockPanel, "Activity");
            logger?.LogDebug("Set dock label for right panel: 'Activity'");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to set dock label for right panel; continuing");
        }

        // Apply official API: SetAutoHideMode for space-saving capability
        try
        {
            dockingManager.SetAutoHideMode(rightDockPanel, true);
            logger?.LogDebug("Enabled auto-hide mode for right panel");
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to enable auto-hide for right panel; continuing");
        }

        // Create activity grid (bottom dock)
        var activityGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            // Dock = DockStyle.Bottom, // REMOVED: Conflicts with DockingManager
            Height = 200,
            Name = "ActivityGrid",
            AllowEditing = false,
            AllowSorting = true,
            AccessibleName = "Activity grid",
            AccessibleDescription = "Recent activity table with columns Time, Activity, Details, and User",
            AccessibleRole = AccessibleRole.Table,
            TabStop = true,
            TabIndex = 12
        };
        var gridDocked = TryDockControl(dockingManager, activityGrid, mainForm, DockingStyle.Bottom, 200, logger);

        if (!gridDocked)
        {
            logger?.LogWarning("Activity grid docking failed; skipping activity grid and timer initialization.");
            return (dockingManager, leftDockPanel, rightDockPanel, null, null);
        }

        // Explicitly set dock labels and force visibility explicitly as requested
        dockingManager.SetDockVisibility(activityGrid, true);
        logger?.LogInformation("SetDockVisibility(true) for ActivityGrid");

        // Setup activity grid columns
        if (activityGrid.Columns == null)
        {
            logger?.LogWarning("Activity grid columns collection was null; skipping column setup.");
        }
        else
        {
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Timestamp", HeaderText = "Time" });
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Activity" });
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details" });
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "User", HeaderText = "User" });
        }

        // Load initial activity data
        LoadActivityDataAsync(activityGrid, serviceProvider, logger);

        // Setup refresh timer
        var activityRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000  // 30 seconds; adjust as needed
        };
        activityRefreshTimer.Tick += (_, _) => LoadActivityDataAsync(activityGrid, serviceProvider, logger);
        activityRefreshTimer.Start();

        logger?.LogInformation("★ CreateDockingHost COMPLETE - DockingManager initialized with left/right panels and activity grid");
        return (dockingManager, leftDockPanel, rightDockPanel, activityGrid, activityRefreshTimer);
    }

    private static bool TryDockControl(DockingManager dockingManager, Control control, Control host, DockingStyle dockingStyle, int size, ILogger? logger)
    {
        // GUARD 1: Verify dockingManager is not null
        if (dockingManager == null)
        {
            logger?.LogError("CRITICAL: DockingManager is null - cannot dock control {ControlName}", control.Name);
            return false;
        }

        // GUARD 2: Verify control and host are not disposed
        if (control.IsDisposed || host.IsDisposed)
        {
            logger?.LogWarning("TryDockControl: Skipped because control or host is disposed (Control={ControlName}, Host={HostName})",
                control.Name, host.Name);
            return false;
        }

        try
        {
            logger?.LogDebug("TryDockControl: Attempting to dock {ControlName} to {HostName} with style {Style} and size {Size}",
                control.Name, host.Name, dockingStyle, size);

            // Enforce minimum size to prevent malformation
            if (size < 100)
            {
                size = 100;
            }
            dockingManager.DockControl(control, host, dockingStyle, size);
            control.MinimumSize = new Size(Math.Max(100, size), 100);
            logger?.LogDebug("Enforced minimum size {MinSize} for {Control}", control.MinimumSize, control.Name);

            // Force visibility after docking (do not call BringToFront - it triggers paint)
            control.Visible = true;

            logger?.LogInformation("TryDockControl: Successfully docked {ControlName} to {HostName} with style {Style}",
                control.Name, host.Name, dockingStyle);
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger?.LogError(ex, "TryDockControl: ArgumentOutOfRangeException when docking {ControlName} to {HostName} with style {Style} and size {Size}. " +
                "This usually means DockingManager.DockControl() received invalid size parameter (negative or too large).",
                control.Name, host.Name, dockingStyle, size);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "TryDockControl: InvalidOperationException when docking {ControlName} to {HostName}. " +
                "This usually means host is not properly registered or control already has a parent.",
                control.Name, host.Name);
            return false;
        }
    }

    /// <summary>
    /// Asynchronously load activity data into the grid.
    /// </summary>
    private static async void LoadActivityDataAsync(
        Syncfusion.WinForms.DataGrid.SfDataGrid activityGrid,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        try
        {
            if (activityGrid.IsDisposed) return;

            // Try to resolve activity repository from service provider
            var activityRepo = serviceProvider.GetService(typeof(IActivityLogRepository)) as IActivityLogRepository;
            if (activityRepo == null)
            {
                logger?.LogWarning("Activity repository not resolved - loading fallback data");
                LoadFallbackActivityData(activityGrid);
                return;
            }

            var activities = await activityRepo.GetRecentActivitiesAsync(skip: 0, take: 50);
            if (activityGrid.InvokeRequired)
            {
                activityGrid.Invoke(() => activityGrid.DataSource = activities);
            }
            else
            {
                activityGrid.DataSource = activities;
            }
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 547)
        {
            logger?.LogError(ex, "Database constraint violation during activity load");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load activity data from database");
            if (activityGrid.InvokeRequired)
            {
                activityGrid.Invoke(() => LoadFallbackActivityData(activityGrid));
            }
            else
            {
                LoadFallbackActivityData(activityGrid);
            }
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
            new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-5), Activity = "Account Updated", Details = "GL-1001", User = "System" },
            new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-15), Activity = "Report Generated", Details = "Budget Q4", User = "Scheduler" },
            new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-30), Activity = "QuickBooks Sync", Details = "42 records", User = "Integrator" },
            new ActivityItem { Timestamp = DateTime.Now.AddHours(-1), Activity = "User Login", Details = "Admin", User = "Admin" },
            new ActivityItem { Timestamp = DateTime.Now.AddHours(-2), Activity = "Backup Complete", Details = "12.5 MB", User = "System" }
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
