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
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("CreateDockingHost: Starting docking creation");

        GradientPanelExt? centralDocumentPanel = null;
        ActivityLogPanel? activityLogPanel = null;
        System.Windows.Forms.Timer? activityRefreshTimer = null;
        DockingLayoutManager? layoutManager = null;

        try
        {
            // Suspend layout to prevent intermediate paints during docking setup
            mainForm.SuspendLayout();

            // Create DockingManager with better defaults
            var dockingManager = new DockingManager
            {
                ShowCaption = true,
                ThemeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme  // Explicit theme for consistency
            };

            // Create a dedicated container panel to host docking content so the Ribbon (top chrome) is never overlapped.
            var hostContainer = new DockingHostClientPanel
            {
                Name = "DockingHostContainer",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };

            // Add the host container to the main form before wiring DockingManager
            mainForm.Controls.Add(hostContainer);
            // Ensure the host container stays behind chrome controls (Ribbon/StatusBar)
            hostContainer.SendToBack();
            // Assign HostControl to the dedicated container
            dockingManager.HostControl = hostContainer;
            // Propagate active theme to host children (best-effort)
            try { hostContainer.PropagateThemeToChildren(dockingManager.ThemeName); } catch { }

            logger?.LogDebug("DockingManager created with Office2019 theme and HostControl set to dedicated container");

            // 1. Create central document panel FIRST to fill remaining space inside host container
            centralDocumentPanel = new GradientPanelExt
            {
                // Do NOT set Dock here; let Dock.Fill be set after addition
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(Color.WhiteSmoke),
                Name = "CentralDocumentPanel",
                Visible = true,
                AccessibleName = "Central Document Area",
                AccessibleDescription = "Main content area for documents and panels"
            };
            
            // Add a welcome label or placeholder content
            var welcomeLabel = new Label
            {
                Text = "Welcome to Wiley Widget\n\nSelect a panel from the navigation to get started.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = SystemColors.GrayText,
                Name = "WelcomeLabel"
            };
            centralDocumentPanel.Controls.Add(welcomeLabel);
            
            // CRITICAL: Add centralPanel to the host container and set Dock.Fill (not docked via manager)
            hostContainer.Controls.Add(centralDocumentPanel);
            centralDocumentPanel.Dock = DockStyle.Fill;
            centralDocumentPanel.BringToFront(); // Ensure it's on top within container

            // 2. Create left sidebar with navigation (generous width)
            var leftDockPanel = new GradientPanelExt
            {
                // Do NOT set Dock here; let DockControl() handle it
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(Color.WhiteSmoke),
                Name = "LeftDockPanel"
            };

            // Add header with accessibility attributes
            var navHeader = new Label
            {
                Text = "Navigation",
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle,
                Name = "NavHeader",
                AccessibleName = "Navigation Panel Header",
                AccessibleRole = AccessibleRole.Grouping,
                AccessibleDescription = "Quick access buttons for main application views and panels"
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

            // Add primary navigation buttons
            navButtonsPanel.Controls.Add(CreateNavButton("📊 Dashboard", () => mainForm.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Fill)));
            navButtonsPanel.Controls.Add(CreateNavButton("💰 Accounts", () => mainForm.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Right)));
            navButtonsPanel.Controls.Add(CreateNavButton("📈 Analytics Hub", () => mainForm.ShowPanel<WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right)));
            navButtonsPanel.Controls.Add(CreateNavButton("⚙️ Settings", () => mainForm.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right)));

            // Dock the left panel via manager (size 300)
            dockingManager.DockControl(leftDockPanel, hostContainer, DockingStyle.Left, 300);
            dockingManager.SetEnableDocking(leftDockPanel, true);
            dockingManager.SetDockLabel(leftDockPanel, "Navigation");
            dockingManager.SetAllowFloating(leftDockPanel, true);
            dockingManager.SetCloseButtonVisibility(leftDockPanel, true);
            dockingManager.SetAutoHideButtonVisibility(leftDockPanel, true);
            dockingManager.SetMenuButtonVisibility(leftDockPanel, true);

            // 3. Create right dock panel using RightDockPanelFactory (manages Activity Log + JARVIS Chat tabs)
            var (rightDockPanel, activityLogPanelTemp, _) = RightDockPanelFactory.CreateRightDockPanel(
                mainForm,
                serviceProvider,
                logger);

            activityLogPanel = activityLogPanelTemp;

            // Dock the right panel via manager (size 350)
            dockingManager.DockControl(rightDockPanel, hostContainer, DockingStyle.Right, 350);
            dockingManager.SetEnableDocking(rightDockPanel, true);
            dockingManager.SetDockLabel(rightDockPanel, "Activity");
            dockingManager.SetAllowFloating(rightDockPanel, true);
            dockingManager.SetCloseButtonVisibility(rightDockPanel, true);
            dockingManager.SetAutoHideButtonVisibility(rightDockPanel, true);
            dockingManager.SetMenuButtonVisibility(rightDockPanel, true);

            logger?.LogDebug("Central document panel created and docked to fill remaining space");

            // ActivityLogPanel is now created and managed by RightDockPanelFactory
            // No need for external timer - panel manages its own refresh cycle (5 seconds)
            activityRefreshTimer = null;

            // Create Layout Manager (dockingManager is guaranteed non-null at this point in the try block)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var layoutPath = Path.Combine(appData, "WileyWidget", "docking_layout.bin");
            layoutManager = new DockingLayoutManager(serviceProvider, panelNavigator, logger, layoutPath, mainForm, dockingManager!, leftDockPanel, rightDockPanel, centralDocumentPanel, activityLogPanel);

            // Ensure layout is calculated after all docking operations
            mainForm.PerformLayout();
            mainForm.Invalidate();

            // Resume layout now that docking is complete
            mainForm.ResumeLayout(true);

            logger?.LogInformation("Docking layout complete - Dashboard fills remaining central space (Left=300px, Right=350px)");

            sw.Stop();
            logger?.LogInformation("CreateDockingHost: Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return (dockingManager, leftDockPanel, rightDockPanel, centralDocumentPanel, activityLogPanel, activityRefreshTimer, layoutManager);
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

        try
        {
            dockingManager.DockControl(control, host, dockingStyle, size);
            control.SafeInvoke(() => control.Visible = true);

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
