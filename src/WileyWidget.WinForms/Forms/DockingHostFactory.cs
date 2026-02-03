using System;
using System.ComponentModel;
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
using WileyWidget.WinForms.Controls.Analytics;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using Syncfusion.WinForms.DataGrid;
using Action = System.Action;
using WileyWidget.WinForms.Helpers;

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
    /// <returns>Tuple of (DockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, activityRefreshTimer, layoutManager)</returns>
    /// <remarks>Central panel fills remaining space to prevent DockingManager paint crashes</remarks>
#pragma warning disable CA1508 // activityLogPanel is always null by design; reserved for future implementation
    public static (
        DockingManager dockingManager,
        GradientPanelExt leftDockPanel,
        GradientPanelExt rightDockPanel,
        GradientPanelExt centralDocumentPanel,
        ActivityLogPanel? activityLogPanel,
        System.Windows.Forms.Timer? activityRefreshTimer,
        DockingLayoutManager? layoutManager
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
#pragma warning restore CA1508
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("CreateDockingHost: Starting docking creation");
        logger?.LogDebug("CreateDockingHost: MainForm={MainForm}, PanelNavigator={PanelNavigator}",
            mainForm.GetType().Name, panelNavigator != null);

        ThemeColors.EnsureThemeAssemblyLoaded(logger);

        GradientPanelExt leftDockPanel;
        GradientPanelExt centralDocumentPanel;
        GradientPanelExt rightDockPanel;
        ActivityLogPanel? activityLogPanel = null;
        System.Windows.Forms.Timer? activityRefreshTimer = null;
        DockingLayoutManager? layoutManager = null;
        try
        {
            // Create DockingManager with better defaults
            var dockingManager = new DockingManager
            {
                ShowCaption = true,
                DockToFill = false
            };
            var dockingManagerInit = (ISupportInitialize)dockingManager;
            try { dockingManagerInit.BeginInit(); } catch { }
            logger?.LogDebug("CreateDockingHost: DockingManager created with ShowCaption={ShowCaption}, DockToFill={DockToFill}",
                dockingManager.ShowCaption, dockingManager.DockToFill);

            try
            {
                var hostControl = (ContainerControl)mainForm;

                // Assign HostForm + HostControl directly to the MainForm
                dockingManager.HostForm = mainForm;
                dockingManager.HostControl = hostControl;
                logger?.LogDebug("CreateDockingHost: HostControl set to MainForm (direct integration)");

                // 1. Create left dock panel (empty container)
                leftDockPanel = new GradientPanelExt
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "LeftDockPanel",
                    AccessibleName = "Left Dock Panel",
                    AccessibleDescription = "Left dock panel container"
                };
                logger?.LogDebug("CreateDockingHost: Left dock panel created: Name={Name}", leftDockPanel.Name);

                // 2. Create central document panel (empty container)
                centralDocumentPanel = new GradientPanelExt
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "CentralDocumentPanel",
                    Visible = true,
                    AccessibleName = "Central Document Area",
                    AccessibleDescription = "Main content area container"
                };
                logger?.LogDebug("CreateDockingHost: Central document panel created: Name={Name}, Visible={Visible}", centralDocumentPanel.Name, centralDocumentPanel.Visible);

                // 3. Create right dock panel (empty container)
                rightDockPanel = new GradientPanelExt
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "RightDockPanel",
                    AccessibleName = "Right Dock Panel",
                    AccessibleDescription = "Right dock panel container"
                };
                logger?.LogDebug("CreateDockingHost: Right dock panel created: Name={Name}", rightDockPanel.Name);

                // Ensure base panels are real children of the host control before docking operations.
                mainForm.Controls.Add(leftDockPanel);
                mainForm.Controls.Add(centralDocumentPanel);
                mainForm.Controls.Add(rightDockPanel);
                leftDockPanel.SendToBack();
                centralDocumentPanel.SendToBack();
                rightDockPanel.SendToBack();

                ArgumentNullException.ThrowIfNull(leftDockPanel);
                ArgumentNullException.ThrowIfNull(centralDocumentPanel);
                ArgumentNullException.ThrowIfNull(rightDockPanel);

                logger?.LogInformation("Docking controls registered: Left={Left}, Central={Central}, Right={Right}, Activity=deferred",
                    leftDockPanel.Name, centralDocumentPanel.Name, rightDockPanel.Name);

                // Suspend layout to prevent intermediate paints during docking setup
                mainForm.SuspendLayout();
                try
                {
                    // DO NOT dock central with Fill to host – forbidden by Syncfusion.
                    // Central fills remaining space automatically.
                    centralDocumentPanel.Dock = DockStyle.Fill;
                    if (!ReferenceEquals(centralDocumentPanel.Parent, hostControl))
                    {
                        hostControl.Controls.Add(centralDocumentPanel);
                    }
                    centralDocumentPanel.BringToFront();
                    logger?.LogInformation("Central panel set to Fill parent (no explicit docking needed)");

                    var leftDocked =
                        TryDockControl(dockingManager, leftDockPanel, centralDocumentPanel, DockingStyle.Left, 300, logger);
                    var rightDocked =
                        TryDockControl(dockingManager, rightDockPanel, centralDocumentPanel, DockingStyle.Right, 350, logger);

                    if (!leftDocked || !rightDocked)
                    {
                        logger?.LogWarning("CreateDockingHost: Docking failed; falling back to basic DockStyle layout");
                        centralDocumentPanel.Dock = DockStyle.Fill;
                        leftDockPanel.Dock = DockStyle.Left;
                        rightDockPanel.Dock = DockStyle.Right;
                    }

                    if (leftDocked)
                    {
                        dockingManager.SetControlMinimumSize(leftDockPanel, new Size(250, 0));
                        dockingManager.SetAutoHideMode(leftDockPanel, true);
                        dockingManager.SetDockVisibility(leftDockPanel, true);
                    }

                    if (rightDocked)
                    {
                        dockingManager.SetControlMinimumSize(rightDockPanel, new Size(320, 0));
                        dockingManager.SetAutoHideMode(rightDockPanel, true);
                        dockingManager.SetDockVisibility(rightDockPanel, true);
                    }

                    logger?.LogDebug("CreateDockingHost: Docking layout applied (Left={LeftDocked}, Right={RightDocked})",
                        leftDocked, rightDocked);

                    // Ensure layout is calculated after all docking operations
                    mainForm.PerformLayout();
                    mainForm.Invalidate();
                }
                finally
                {
                    // Resume layout now that docking is complete
                    mainForm.ResumeLayout(true);
                }

                leftDockPanel.Refresh();
                centralDocumentPanel.Refresh();
                rightDockPanel.Refresh();

            logger?.LogDebug("Docking panels created and docked (left, center, right) with empty containers");
            activityRefreshTimer = null;

            // Create Layout Manager (dockingManager is guaranteed non-null at this point in the try block)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var layoutPath = Path.Combine(appData, "WileyWidget", "docking_layout.bin");
            layoutManager = new DockingLayoutManager(serviceProvider!, panelNavigator, logger, layoutPath, mainForm, dockingManager!, leftDockPanel, rightDockPanel, centralDocumentPanel, activityLogPanel);

            logger?.LogInformation("Docking layout complete - Dashboard hosted in tabbed document area (Left=300px, Right=350px)");

            sw.Stop();
            logger?.LogInformation("CreateDockingHost: Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return (dockingManager, leftDockPanel, rightDockPanel, centralDocumentPanel, activityLogPanel, activityRefreshTimer, layoutManager);
            }
            finally
            {
                try { dockingManagerInit.EndInit(); } catch { }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create docking host - falling back to basics");

            // Fallback: Return safe minimal instances to prevent downstream null refs
            var fallbackManager = new DockingManager
            {
                HostControl = mainForm, // CRITICAL: Ensure HostControl is set
                DockToFill = true,
                ShowCaption = true
            };

            return (
                fallbackManager,
                new GradientPanelExt { Dock = DockStyle.Left },
                new GradientPanelExt { Dock = DockStyle.Right },
                new GradientPanelExt { Dock = DockStyle.Fill },
                null,
                null,
                null
            );
        }
    }

    private static Button CreateNavButton(string text, Action onClick)
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
            AutoSize = false,
            AccessibleName = $"Navigate to {text.Replace("📊 ", "").Replace("💰 ", "").Replace("📈 ", "").Replace("⚙️ ", "")}",
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleDescription = $"Click to open the {text.Replace("📊 ", "").Replace("💰 ", "").Replace("📈 ", "").Replace("⚙️ ", "")} panel"
        };
        btn.Click += (s, e) => onClick();
        return btn;
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

        if (ReferenceEquals(host, dockingManager.HostControl) && dockingStyle == DockingStyle.Fill)
        {
            logger?.LogWarning("Skipping invalid Fill dock to host control (Syncfusion restriction)");
            return false;
        }

        try
        {
            if (ReferenceEquals(host, dockingManager.HostControl) && dockingStyle == DockingStyle.Tabbed)
            {
                logger?.LogDebug("TryDockControl: HostControl does not support DockingStyle.Tabbed. Switching to DockingStyle.Fill for {ControlName}.", control.Name);
                dockingStyle = DockingStyle.Fill;
            }

            dockingManager.DockControl(control, host, dockingStyle, size);
            control.SafeInvoke(() => control.Visible = true);

            logger?.LogInformation("TryDockControl: Successfully docked {ControlName} to {HostName} with style {Style}",
                control.Name, host.Name, dockingStyle);
            return true;
        }
        catch (DockingManagerException dmEx)
        {
            logger?.LogError(dmEx, "TryDockControl: Syncfusion docking failure for {ControlName}", control.Name);
            return false;
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
                headerLabel.SafeInvoke(() =>
                {
                    headerLabel.Text = "Recent Activity (Fallback — real data unavailable)";
                    headerLabel.ForeColor = SystemColors.GrayText;
                });
            }
        }
        catch
        {
            // Silently fail if header update fails (not critical)
        }
    }

    /// <summary>
    /// Update the activity grid header label to normal (when real data loads successfully).
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
                headerLabel.SafeInvoke(() =>
                {
                    headerLabel.Text = "Recent Activity";
                    headerLabel.ForeColor = SystemColors.ControlText;
                });
            }
        }
        catch
        {
            // Silently fail if header update fails (not critical)
        }
    }
}
