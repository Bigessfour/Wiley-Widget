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

        // Initialize DockingManager
        var dockingManager = new DockingManager
        {
            DockToFill = true,
            ThemeName = "Office2019Colorful"  // Rely on SfSkinManager; set theme here if needed
        };

        // Create left dock panel
        var leftDockPanel = new GradientPanelExt
        {
            Dock = DockStyle.Left,
            Width = 300,
            Name = "LeftDockPanel",
            AccessibleName = "Navigation panel",
            AccessibleDescription = "Left docked panel for navigation content",
            AccessibleRole = AccessibleRole.Pane,
            TabStop = false,
            TabIndex = 10
        };
        dockingManager.DockControl(leftDockPanel, mainForm, DockingStyle.Left, 300);

        // Create right dock panel
        var rightDockPanel = new GradientPanelExt
        {
            Dock = DockStyle.Right,
            Width = 300,
            Name = "RightDockPanel",
            AccessibleName = "Activity panel",
            AccessibleDescription = "Right docked panel hosting activity and insights",
            AccessibleRole = AccessibleRole.Pane,
            TabStop = false,
            TabIndex = 11
        };
        dockingManager.DockControl(rightDockPanel, mainForm, DockingStyle.Right, 300);

        // Create activity grid (bottom dock)
        var activityGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Dock = DockStyle.Bottom,
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
        dockingManager.DockControl(activityGrid, mainForm, DockingStyle.Bottom, 200);

        // Setup activity grid columns
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Timestamp", HeaderText = "Time" });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Activity" });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details" });
        activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "User", HeaderText = "User" });

        // Load initial activity data
        LoadActivityDataAsync(activityGrid, serviceProvider, logger);

        // Setup refresh timer
        var activityRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000  // 30 seconds; adjust as needed
        };
        activityRefreshTimer.Tick += (_, _) => LoadActivityDataAsync(activityGrid, serviceProvider, logger);
        activityRefreshTimer.Start();

        return (dockingManager, leftDockPanel, rightDockPanel, activityGrid, activityRefreshTimer);
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
