using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using Syncfusion.WinForms.DataGrid;
using Action = System.Action;

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
        ActivityLogPanel? activityLogPanel,
        System.Windows.Forms.Timer? activityRefreshTimer,
        DockingLayoutManager? layoutManager
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("CreateDockingHost: Starting docking creation");

        try
        {
            // Create DockingManager with better defaults
            var dockingManager = new DockingManager
            {
                HostControl = mainForm, // CRITICAL: Must assign HostControl before docking controls
                // DockLayout = DockLayoutType.Free, // Not available in this version
                // AllowFloating = true, // Set per control
                // AllowAutoHide = true, // Set per control
                // AllowDocking = true, // Set per control
                ShowCaption = true,
                // ShowCaptionIcon = true,
                // ShowCaptionButtons = true,
                DockToFill = true,      // Set to true to allow filling the remaining space
                ThemeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme  // Explicit theme for consistency
            };

            logger?.LogDebug("DockingManager created with Free layout and Office2019 theme");

            // 1. Dock left sidebar with navigation (dock left first, generous width)
            var leftDockPanel = new GradientPanelExt
            {
                Dock = DockStyle.Left,
                Width = 300,
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(Color.WhiteSmoke),
                Name = "LeftDockPanel"
            };

            // Add header
            var navHeader = new Label
            {
                Text = "Navigation",
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle,
                Name = "NavHeader"
            };
            leftDockPanel.Controls.Add(navHeader);

            // Add navigation buttons panel with scrolling
            var navButtonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8),
                BackColor = Color.Transparent,
                Name = "NavButtonsPanel"
            };
            leftDockPanel.Controls.Add(navButtonsPanel);

            // Helper to create navigation button
            Func<string, System.Action, Button> createNavButton = (text, clickHandler) =>
            {
                var btn = new Button
                {
                    Text = text,
                    Dock = DockStyle.Top,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 8, 0),
                    Font = new Font("Segoe UI", 9),
                    ForeColor = SystemColors.ControlText,
                    AutoSize = false
                };
                btn.Click += (s, e) => clickHandler();
                return btn;
            };

            // Add primary navigation buttons
            navButtonsPanel.Controls.Add(createNavButton("📊 Dashboard", () => mainForm.ShowPanel<DashboardPanel>()));
            navButtonsPanel.Controls.Add(createNavButton("💰 Accounts", () => mainForm.ShowPanel<AccountsPanel>()));
            navButtonsPanel.Controls.Add(createNavButton("📈 Budget", () => mainForm.ShowPanel<BudgetPanel>()));
            navButtonsPanel.Controls.Add(createNavButton("📉 Analytics", () => mainForm.ShowPanel<BudgetAnalyticsPanel>()));
            navButtonsPanel.Controls.Add(createNavButton("📄 Reports", () => mainForm.ShowPanel<ReportsPanel>()));
            navButtonsPanel.Controls.Add(createNavButton("⚙️ Settings", () => mainForm.ShowPanel<SettingsPanel>()));

            // CRITICAL: Add controls to mainForm BEFORE docking them
            mainForm.Controls.Add(leftDockPanel);

            // Dock left panel AFTER adding to mainForm
            dockingManager.DockControl(leftDockPanel, mainForm, DockingStyle.Left, 300);

            // 3. Create right dock panel using RightDockPanelFactory (manages Activity Log + JARVIS Chat tabs)
            var (rightDockPanel, activityLogPanel, _) = RightDockPanelFactory.CreateRightDockPanel(
                mainForm,
                serviceProvider,
                logger);

            // CRITICAL: Add rightPanel to mainForm BEFORE docking it
            mainForm.Controls.Add(rightDockPanel);
            dockingManager.DockControl(rightDockPanel, mainForm, DockingStyle.Right, 350);

            // 4. Central space fills automatically with DockToFill=true
            // The DockingManager with DockToFill=true will auto-fill remaining space
            // No need to manually create a dashboard panel here - it will be shown on demand via ShowPanel
            logger?.LogDebug("Central space configured to auto-fill between left (300px) and right (350px) docking panels");

            // ActivityLogPanel is now created and managed by RightDockPanelFactory
            // No need for external timer - panel manages its own refresh cycle (5 seconds)
            System.Windows.Forms.Timer? activityRefreshTimer = null;

            // Create Layout Manager
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var layoutPath = Path.Combine(appData, "WileyWidget", "docking_layout.bin");
            var layoutManager = new DockingLayoutManager(serviceProvider, panelNavigator, logger, layoutPath, mainForm, dockingManager, leftDockPanel, rightDockPanel, activityLogPanel);

            logger?.LogInformation("Docking layout complete - Dashboard fills remaining central space (Left=300px, Right=350px)");

            sw.Stop();
            logger?.LogInformation("CreateDockingHost: Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return (dockingManager, leftDockPanel, rightDockPanel, activityLogPanel, activityRefreshTimer, layoutManager);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create docking host - falling back to basics");

            // Fallback: Return safe minimal instances to prevent downstream null refs
            var fallbackManager = new DockingManager
            {
                HostControl = mainForm, // CRITICAL: Ensure HostControl is set
                ThemeName = "Office2019Colorful"
            };

            return (
                fallbackManager,
                new GradientPanelExt { Dock = DockStyle.Left },
                new GradientPanelExt { Dock = DockStyle.Right },
                null,
                null,
                null
            );
        }
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
            dockingManager.DockControl(control, host, dockingStyle, size);
            control.Visible = true;

            logger?.LogInformation("TryDockControl: Successfully docked {ControlName} to {HostName} with style {Style}",
                control.Name, host.Name, dockingStyle);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "TryDockControl: Failed to dock {ControlName}", control.Name);
            return false;
        }
    }

    /// <summary>
    /// Load activity data with timeout protection to prevent infinite hangs.
    /// </summary>
    private static async Task LoadActivityDataWithTimeoutAsync(
        SfDataGrid activityGrid,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        const int timeoutSeconds = 5;
        try
        {
            if (activityGrid.IsDisposed) return;

            // Try to resolve activity repository from service provider
            var activityRepo = serviceProvider.GetService(typeof(IActivityLogRepository)) as IActivityLogRepository;
            if (activityRepo == null)
            {
                logger?.LogDebug("Activity repository not resolved - loading fallback data");
                LoadFallbackActivityData(activityGrid);
                return;
            }

            // Load with timeout protection
            var loadTask = activityRepo.GetRecentActivitiesAsync(skip: 0, take: 50);
            var activities = await loadTask.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);

            if (activityGrid.InvokeRequired)
            {
                activityGrid.Invoke(() =>
                {
                    if (!activityGrid.IsDisposed)
                    {
                        activityGrid.DataSource = activities;
                        activityGrid.Refresh();
                        UpdateActivityHeaderForRealData(activityGrid);
                        logger?.LogInformation("Activity grid loaded real data - {Count} items", activities?.Count ?? 0);
                    }
                });
            }
            else
            {
                if (!activityGrid.IsDisposed)
                {
                    activityGrid.DataSource = activities;
                    activityGrid.Refresh();
                    UpdateActivityHeaderForRealData(activityGrid);
                    logger?.LogInformation("Activity grid loaded real data - {Count} items", activities?.Count ?? 0);
                }
            }
        }
        catch (TimeoutException ex)
        {
            logger?.LogWarning(ex, "Activity data load timed out after {TimeoutSeconds}s - loading fallback data", timeoutSeconds);
            LoadFallbackActivityData(activityGrid);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 547)
        {
            logger?.LogError(ex, "Database constraint violation during activity load");
            LoadFallbackActivityData(activityGrid);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load activity data from database - loading fallback");
            LoadFallbackActivityData(activityGrid);
        }
    }

    /// <summary>
    /// Asynchronously load activity data into the grid.
    /// </summary>
    private static Task LoadActivityDataAsync(
        SfDataGrid activityGrid,
        IServiceProvider serviceProvider,
        ILogger? logger)
    {
        return LoadActivityDataAsyncInternal(activityGrid, serviceProvider, logger);
    }

    /// <summary>
    /// Internal async implementation for loading activity data.
    /// </summary>
    private static async Task LoadActivityDataAsyncInternal(
        SfDataGrid activityGrid,
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
                // logger?.LogWarning("Activity repository not resolved - loading fallback data"); // Reduce noise
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
    /// Uses ActivityFallbackDataService for comprehensive, reusable sample data with session lifetime caching.
    /// Updates header to indicate fallback data is being used.
    /// </summary>
    private static void LoadFallbackActivityData(SfDataGrid activityGrid)
    {
        if (activityGrid == null || activityGrid.IsDisposed)
            return;

        // Load comprehensive fallback data from service (cached, session-lifetime)
        var activities = ActivityFallbackDataService.GetFallbackActivityData();

        if (activityGrid.InvokeRequired)
        {
            activityGrid.Invoke(() =>
            {
                if (!activityGrid.IsDisposed)
                {
                    activityGrid.DataSource = activities;
                    activityGrid.Refresh();

                    // Update header to show fallback status
                    UpdateActivityHeaderForFallback(activityGrid);
                }
            });
        }
        else
        {
            if (!activityGrid.IsDisposed)
            {
                activityGrid.DataSource = activities;
                activityGrid.Refresh();

                // Update header to show fallback status
                UpdateActivityHeaderForFallback(activityGrid);
            }
        }
    }

    /// <summary>
    /// Update the activity grid header label to indicate fallback data is in use.
    /// </summary>
    private static void UpdateActivityHeaderForFallback(SfDataGrid activityGrid)
    {
        try
        {
            // Find the parent container of the grid (the right dock panel)
            var parent = activityGrid.Parent;
            if (parent == null) return;

            // Find the header label in the parent's controls
            var headerLabel = parent.Controls["ActivityHeaderLabel"] as Label;
            if (headerLabel != null && !headerLabel.IsDisposed)
            {
                headerLabel.Text = "Recent Activity (Fallback — real data unavailable)";
                headerLabel.ForeColor = SystemColors.GrayText;
            }
        }
        catch
        {
            // Silently fail if header update fails (not critical)
        }
    }

    /// <summary>
    /// Reset the activity grid header label to normal (when real data loads successfully).
    /// </summary>
    private static void UpdateActivityHeaderForRealData(SfDataGrid activityGrid)
    {
        try
        {
            // Find the parent container of the grid (the right dock panel)
            var parent = activityGrid.Parent;
            if (parent == null) return;

            // Find the header label in the parent's controls
            var headerLabel = parent.Controls["ActivityHeaderLabel"] as Label;
            if (headerLabel != null && !headerLabel.IsDisposed)
            {
                headerLabel.Text = "Recent Activity";
                headerLabel.ForeColor = SystemColors.ControlText;
            }
        }
        catch
        {
            // Silently fail if header update fails (not critical)
        }
    }
}
