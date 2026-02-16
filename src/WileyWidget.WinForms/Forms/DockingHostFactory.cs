using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
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
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
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
    private const string DockingClientPanelName = "_dockingClientPanel";
    private const string DockingHostContainerName = "_dockingHostContainer";
    private const int LeftDockMinimumWidth = 280;
    private const int RightDockMinimumWidth = 300;

    public static (
        DockingManager dockingManager,
        LegacyGradientPanel? leftDockPanel,
        LegacyGradientPanel? rightDockPanel,
        LegacyGradientPanel centralDocumentPanel,
        ActivityLogPanel? activityLogPanel,
        System.Windows.Forms.Timer? activityRefreshTimer,
        DockingLayoutManager? dockingLayoutManager
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        ILogger? logger)
    {
        return CreateDockingHost(mainForm, serviceProvider, panelNavigator, mainForm, logger);
    }

    /// <summary>
    /// Create and configure DockingManager with all docking panels.
    /// </summary>
    /// <param name="mainForm">Parent MainForm instance</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="panelNavigator">Panel navigation service</param>
    /// <param name="dockingHostPanel">Dedicated panel to use as DockingManager.HostControl</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Tuple of (DockingManager, leftPanel, rightPanel, centralPanel, activityLogPanel, activityRefreshTimer)</returns>
    /// <remarks>Central panel fills remaining space to prevent DockingManager paint crashes</remarks>
#pragma warning disable CA1508 // activityLogPanel is always null by design; reserved for future implementation
    public static (
        DockingManager dockingManager,
        LegacyGradientPanel? leftDockPanel,
        LegacyGradientPanel? rightDockPanel,
        LegacyGradientPanel centralDocumentPanel,
        ActivityLogPanel? activityLogPanel,
        System.Windows.Forms.Timer? activityRefreshTimer,
        DockingLayoutManager? dockingLayoutManager
    ) CreateDockingHost(
        MainForm mainForm,
        IServiceProvider serviceProvider,
        IPanelNavigationService? panelNavigator,
        Control dockingHostPanel,
        ILogger? logger)
#pragma warning restore CA1508
    {
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
        if (dockingHostPanel == null) throw new ArgumentNullException(nameof(dockingHostPanel));

        var hostControl = ResolveDockingHostControl(mainForm, dockingHostPanel, logger);

        var sw = Stopwatch.StartNew();
        logger?.LogInformation("CreateDockingHost: Starting docking creation");
        logger?.LogDebug("CreateDockingHost: MainForm={MainForm}, PanelNavigator={PanelNavigator}",
            mainForm.GetType().Name, panelNavigator != null);

        WileyWidget.WinForms.Themes.ThemeColors.EnsureThemeAssemblyLoaded(logger);

        LegacyGradientPanel? leftDockPanel = null;
        LegacyGradientPanel centralDocumentPanel;
        LegacyGradientPanel? rightDockPanel = null;
        ActivityLogPanel? activityLogPanel = null;
        System.Windows.Forms.Timer? activityRefreshTimer = null;
        try
        {
            // Create DockingManager with Syncfusion-standard interactive defaults.
            var dockingManager = new DockingManager
            {
                ShowCaption = false,
                DockToFill = false,
                CloseEnabled = true,
                PersistState = false,
                AnimateAutoHiddenWindow = false
            };
            var dockingManagerInit = (ISupportInitialize)dockingManager;
            try { dockingManagerInit.BeginInit(); } catch { }
            logger?.LogDebug("CreateDockingHost: DockingManager created with ShowCaption={ShowCaption}, DockToFill={DockToFill}",
                dockingManager.ShowCaption, dockingManager.DockToFill);

            var themeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            dockingManager.ThemeName = themeName;
            SfSkinManager.SetVisualStyle(dockingManager, themeName);
            dockingManager.DragProviderStyle = DragProviderStyle.VS2012;
            dockingManager.EnableDocumentMode = false;

            try
            {
                // Assignment of HostForm + HostControl to the dedicated docking host panel
                dockingManager.HostForm = mainForm;

                dockingManager.HostControl = hostControl;
                
                // hostControl initially visible - layout finalized after docking configuration
                logger?.LogDebug("CreateDockingHost: HostControl set to dedicated docking host container");

                // Suspend layout during bulk control creation for performance
                hostControl.SuspendLayout();
                try
                {
                    // 2. Create central document panel (empty container) - ALWAYS created
                    centralDocumentPanel = new LegacyGradientPanel
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "CentralDocumentPanel",
                    Visible = false, // Defer visibility until docking completes
                    Padding = new Padding(0, 0, 0, 0),
                    AccessibleName = "Central Document Area",
                    AccessibleDescription = "Main content area container"
                };
                // Add placeholder to prevent empty DockHost painting crash (Syncfusion requires Controls.Count > 0)
                var centralPlaceholder = new Panel
                {
                    Dock = DockStyle.Fill,
                    Name = "_centralPlaceholder",
                    BackColor = SystemColors.Window,
                    Visible = true
                };
                centralDocumentPanel.Controls.Add(centralPlaceholder);
                logger?.LogDebug("CreateDockingHost: Central document panel created: Name={Name}, Visible={Visible}", centralDocumentPanel.Name, centralDocumentPanel.Visible);

                // Always create side panels (ignore MinimalMode for panel creation)
                // 1. Create left dock panel (empty container)
                leftDockPanel = new LegacyGradientPanel
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "LeftDockPanel",
                    AccessibleName = "Left Dock Panel",
                    AccessibleDescription = "Left dock panel container",
                    Visible = false  // Defer visibility until docking completes
                };
                // Add placeholder to prevent empty DockHost painting crash (Syncfusion requires Controls.Count > 0)
                var leftPlaceholder = new Panel
                {
                    Dock = DockStyle.Fill,
                    Name = "_navPlaceholder",
                    BackColor = SystemColors.Control,
                    Visible = true
                };
                leftDockPanel.Controls.Add(leftPlaceholder);
                logger?.LogDebug("CreateDockingHost: Left dock panel created: Name={Name}", leftDockPanel.Name);

                // 3. Create right dock panel (Activity Log only)
                rightDockPanel = new LegacyGradientPanel
                {
                    BorderStyle = BorderStyle.None,
                    BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                    Name = "RightDockPanel",
                    AccessibleName = "Right Dock Panel",
                    AccessibleDescription = "Right dock panel container (Activity Log)",
                    Visible = false  // Defer visibility until docking completes
                };
                // Permanent placeholder prevents empty-panel paint issues (Syncfusion best practice)
                var rightPlaceholder = new Panel
                {
                    Dock = DockStyle.Fill,
                    Name = "_rightPlaceholder",
                    BackColor = SystemColors.Control,
                    Visible = false
                };
                rightDockPanel.Controls.Add(rightPlaceholder);
                rightPlaceholder.SendToBack();

                    var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetRequiredService<IServiceScopeFactory>(serviceProvider);
                    var activityLogLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                            .GetService<ILogger<ActivityLogPanel>>(serviceProvider)
                        ?? (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                            .GetService<ILogger>(serviceProvider) as ILogger<ActivityLogPanel>)
                        ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivityLogPanel>.Instance;

                    activityLogPanel = new ActivityLogPanel(scopeFactory, activityLogLogger)
                    {
                        Dock = DockStyle.Fill,
                        Name = "ActivityLogPanel"
                    };

                if (!ReferenceEquals(activityLogPanel.Parent, rightDockPanel))
                {
                    rightDockPanel.Controls.Add(activityLogPanel);
                }
                logger?.LogInformation("Pre-docked ActivityLogPanel via explicit parent assignment");

                logger?.LogInformation("CreateDockingHost: Right dock panel created with ActivityLogPanel");
                }
                finally
                {
                    hostControl.ResumeLayout(false); // false - docking layout finalization follows
                }

                ArgumentNullException.ThrowIfNull(centralDocumentPanel);

                try
                {
                    ConfigureFixedDockingPanels(dockingManager, hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
                    EnsurePanelsVisible(hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed applying fixed docking layout");
                    throw new InvalidOperationException("Fixed docking layout configuration failed; falling back to safe non-docking layout.", ex);
                }

                dockingManager.NewDockStateEndLoad += (_, _) =>
                {
                    if (mainForm.IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        EnsurePanelsVisible(hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed stabilizing panel visibility after state load");
                        EnsurePanelsVisible(hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
                    }
                };

                leftDockPanel.Refresh();
                centralDocumentPanel.Refresh();
                rightDockPanel.Refresh();

                logger?.LogDebug("Docking panels created and docked (left, center, right) with empty containers");
                activityRefreshTimer = null;

                logger?.LogInformation("Docking layout initialized with adaptive sizing (dock/float/auto-hide enabled)");

                mainForm.RegisterDockingResources(
                    dockingManager,
                    leftDockPanel,
                    rightDockPanel,
                    centralDocumentPanel,
                    activityLogPanel,
                    activityRefreshTimer);

                sw.Stop();
                logger?.LogInformation("CreateDockingHost: Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

                return (dockingManager, leftDockPanel, rightDockPanel, centralDocumentPanel, activityLogPanel, activityRefreshTimer, null);
            }
            finally
            {
                try { dockingManagerInit.EndInit(); } catch { }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create docking host - falling back to basics");

            ResetHostControlForFallback(hostControl, logger);

            var fallbackLeft = new LegacyGradientPanel
            {
                Name = "LeftDockPanel",
                Dock = DockStyle.Left,
                Width = 300,
                MinimumSize = new Size(280, 0),
                Visible = true
            };

            var fallbackRight = new LegacyGradientPanel
            {
                Name = "RightDockPanel",
                Dock = DockStyle.Right,
                Width = 350,
                MinimumSize = new Size(300, 0),
                Visible = true
            };

            var fallbackCenter = new LegacyGradientPanel
            {
                Name = "CentralDocumentPanel",
                Dock = DockStyle.Fill,
                Visible = true
            };

            if (fallbackCenter.Controls.Count == 0)
            {
                fallbackCenter.Controls.Add(new Panel
                {
                    Dock = DockStyle.Fill,
                    Name = "_centralFallbackPlaceholder",
                    Visible = true
                });
            }

            ApplyEmergencyPanelLayout(hostControl, fallbackLeft, fallbackRight, fallbackCenter, logger);

            // Fallback: Return safe minimal instances to prevent downstream null refs
        var fallbackHostContainer = hostControl as ContainerControl
            ?? throw new InvalidOperationException($"HostControl must be ContainerControl-compatible, got {hostControl.GetType().Name}");

        var fallbackManager = new DockingManager
        {
            HostForm = mainForm,
            HostControl = fallbackHostContainer,
            DockToFill = false,
            ShowCaption = false,
            PersistState = false
        };

            var fallbackTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            fallbackManager.ThemeName = fallbackTheme;
            try { SfSkinManager.SetVisualStyle(fallbackManager, fallbackTheme); } catch { }

            mainForm.RegisterDockingResources(
                fallbackManager,
                fallbackLeft,
                fallbackRight,
                fallbackCenter,
                null,
                null);

            return (
                fallbackManager,
                fallbackLeft,
                fallbackRight,
                fallbackCenter,
                null,
                null,
                null
            );
        }
    }

    private static void ResetHostControlForFallback(Control hostControl, ILogger? logger)
    {
        if (hostControl.IsDisposed)
        {
            return;
        }

        try
        {
            hostControl.SuspendLayout();
            try
            {
                for (var index = hostControl.Controls.Count - 1; index >= 0; index--)
                {
                    var child = hostControl.Controls[index];
                    hostControl.Controls.RemoveAt(index);

                    try
                    {
                        child.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                hostControl.ResumeLayout(true);
            }

            logger?.LogWarning("Docking host control reset before fallback layout initialization");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fully reset docking host before fallback initialization");
        }
    }

    /// <summary>
    /// Resolves or creates the docking host control for the DockingManager.
    /// Uses a ContainerControl-compatible host to satisfy DockingManager requirements.
    /// </summary>
    private static ContainerControl ResolveDockingHostControl(MainForm mainForm, Control requestedHost, ILogger? logger)
    {
        if (requestedHost is ContainerControl container && !ReferenceEquals(requestedHost, mainForm))
        {
            EnsureHostPanelLayout(container);
            return container;
        }

        ContainerControl? hostContainer = null;
        foreach (Control child in mainForm.Controls)
        {
            if (string.Equals(child.Name, DockingHostContainerName, StringComparison.Ordinal)
                && child is ContainerControl containerControl)
            {
                hostContainer = containerControl;
                break;
            }
        }

        if (hostContainer == null)
        {
            hostContainer = new UserControl
            {
                Name = DockingHostContainerName,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                TabStop = false,
                BackColor = Color.Transparent
            };

            mainForm.Controls.Add(hostContainer);
            logger?.LogInformation("Created docking host container for DockingManager");
        }
        else if (!ReferenceEquals(hostContainer.Parent, mainForm))
        {
            hostContainer.Parent?.Controls.Remove(hostContainer);
            mainForm.Controls.Add(hostContainer);
        }
        else if (!mainForm.Controls.Contains(hostContainer))
        {
            mainForm.Controls.Add(hostContainer);
        }

        EnsureHostPanelLayout(hostContainer);
        return hostContainer;
    }

    /// <summary>
    /// Ensures host panel layout is properly configured.
    /// For DockingClientPanel, this is minimal since SizeToFit handles automatic layout.
    /// For custom host panels, ensures Fill docking and padding.
    /// </summary>
    private static void EnsureHostPanelLayout(Control hostControl)
    {
        // DockingClientPanel with SizeToFit handles layout automatically
        if (hostControl is DockingClientPanel dockingClientPanel)
        {
            if (!dockingClientPanel.SizeToFit)
            {
                dockingClientPanel.SizeToFit = true;
            }
            return;
        }

        // For non-DockingClientPanel hosts (custom panels), use manual fill
        hostControl.Dock = DockStyle.Fill;

        if (hostControl is ScrollableControl scrollableControl)
        {
            scrollableControl.Margin = Padding.Empty;
            scrollableControl.Padding = Padding.Empty;
        }
    }

    private static void EnsurePanelsVisible(
        Control hostControl,
        Control leftDockPanel,
        Control rightDockPanel,
        Control centralDocumentPanel,
        ILogger? logger)
    {
        try
        {
            EnsureControlAndParentsVisible(hostControl, logger);

            // CRITICAL FIX: Make ALL dock panels visible, not just central
            if (!leftDockPanel.IsDisposed)
            {
                leftDockPanel.Visible = true;
                logger?.LogDebug("Left dock panel set to visible");
            }

            if (!rightDockPanel.IsDisposed)
            {
                rightDockPanel.Visible = true;
                logger?.LogDebug("Right dock panel set to visible");
            }

            if (!centralDocumentPanel.IsDisposed)
            {
                centralDocumentPanel.Visible = true;
                logger?.LogDebug("Central document panel set to visible");
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "EnsurePanelsVisible encountered an exception");
        }
    }

    private static void EnsureControlAndParentsVisible(Control control, ILogger? logger)
    {
        var current = control;
        while (current != null)
        {
            if (!current.Visible)
            {
                logger?.LogDebug("Control '{ControlName}' was hidden - forcing visible", current.Name);
                current.Visible = true;
            }

            current = current.Parent;
        }
    }

    private static void ApplyEmergencyPanelLayout(
        Control hostControl,
        LegacyGradientPanel leftDockPanel,
        LegacyGradientPanel rightDockPanel,
        LegacyGradientPanel centralDocumentPanel,
        ILogger? logger)
    {
        if (hostControl.IsDisposed || leftDockPanel.IsDisposed || rightDockPanel.IsDisposed || centralDocumentPanel.IsDisposed)
        {
            return;
        }

        hostControl.SuspendLayout();
        try
        {
            EnsureControlAndParentsVisible(hostControl, logger);

            if (!ReferenceEquals(leftDockPanel.Parent, hostControl))
            {
                leftDockPanel.Parent?.Controls.Remove(leftDockPanel);
                hostControl.Controls.Add(leftDockPanel);
            }

            if (!ReferenceEquals(rightDockPanel.Parent, hostControl))
            {
                rightDockPanel.Parent?.Controls.Remove(rightDockPanel);
                hostControl.Controls.Add(rightDockPanel);
            }

            if (!ReferenceEquals(centralDocumentPanel.Parent, hostControl))
            {
                centralDocumentPanel.Parent?.Controls.Remove(centralDocumentPanel);
                hostControl.Controls.Add(centralDocumentPanel);
            }

            var leftFallbackWidth = CalculateDockSize(hostControl, DockingStyle.Left, LeftDockMinimumWidth, 0.28f, 0.52f);
            leftDockPanel.Dock = DockStyle.Left;
            leftDockPanel.Width = Math.Max(leftDockPanel.Width, leftFallbackWidth);
            leftDockPanel.MinimumSize = new Size(LeftDockMinimumWidth, 0);

            var rightFallbackWidth = CalculateDockSize(hostControl, DockingStyle.Right, RightDockMinimumWidth, 0.30f, 0.56f);
            rightDockPanel.Dock = DockStyle.Right;
            rightDockPanel.Width = Math.Max(rightDockPanel.Width, rightFallbackWidth);
            rightDockPanel.MinimumSize = new Size(RightDockMinimumWidth, 0);

            centralDocumentPanel.Dock = DockStyle.Fill;

            EnsurePanelsVisible(hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
            centralDocumentPanel.SendToBack();
            leftDockPanel.BringToFront();
            rightDockPanel.BringToFront();

            logger?.LogWarning("Emergency panel layout applied (non-docking fallback)");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed applying emergency panel layout");
        }
        finally
        {
            hostControl.ResumeLayout(true);
            hostControl.PerformLayout();
            hostControl.Invalidate(true);
        }
    }

    private static void ConfigureFixedDockingPanels(
        DockingManager dockingManager,
        Control hostControl,
        LegacyGradientPanel leftDockPanel,
        LegacyGradientPanel rightDockPanel,
        LegacyGradientPanel centralDocumentPanel,
        ILogger? logger)
    {
        if (hostControl.IsDisposed || leftDockPanel.IsDisposed || rightDockPanel.IsDisposed || centralDocumentPanel.IsDisposed)
        {
            return;
        }

        hostControl.SuspendLayout();
        try
        {
            EnsurePanelHasAtLeastOneChild(leftDockPanel, "_leftDockFallbackPlaceholder", logger);
            EnsurePanelHasAtLeastOneChild(rightDockPanel, "_rightDockFallbackPlaceholder", logger);
            EnsurePanelHasAtLeastOneChild(centralDocumentPanel, "_centralDockFallbackPlaceholder", logger);

            dockingManager.SetEnableDocking(leftDockPanel, true);
            dockingManager.SetEnableDocking(rightDockPanel, true);
            dockingManager.SetEnableDocking(centralDocumentPanel, false);

            var leftDockWidth = CalculateDockSize(hostControl, DockingStyle.Left, LeftDockMinimumWidth, 0.28f, 0.50f);
            var rightDockWidth = CalculateDockSize(hostControl, DockingStyle.Right, RightDockMinimumWidth, 0.30f, 0.56f);

            var leftDocked = EnsureDockedControlRegistered(dockingManager, leftDockPanel, hostControl, DockingStyle.Left, leftDockWidth, logger);
            var rightDocked = EnsureDockedControlRegistered(dockingManager, rightDockPanel, hostControl, DockingStyle.Right, rightDockWidth, logger);

            if (!leftDocked || !rightDocked)
            {
                throw new InvalidOperationException("One or more fixed docking panels failed to dock.");
            }

            dockingManager.SetDockLabel(leftDockPanel, "Navigation");
            dockingManager.SetControlMinimumSize(leftDockPanel, new Size(LeftDockMinimumWidth, 0));
            dockingManager.SetControlSize(leftDockPanel, new Size(leftDockWidth, Math.Max(hostControl.ClientSize.Height, 1)));
            // Visibility is applied after successful docking configuration

            dockingManager.SetDockLabel(rightDockPanel, "Activity");
            dockingManager.SetControlMinimumSize(rightDockPanel, new Size(RightDockMinimumWidth, 0));
            dockingManager.SetControlSize(rightDockPanel, new Size(rightDockWidth, Math.Max(hostControl.ClientSize.Height, 1)));
            // Visibility is applied after successful docking configuration

            if (!ReferenceEquals(centralDocumentPanel.Parent, hostControl))
            {
                centralDocumentPanel.Parent?.Controls.Remove(centralDocumentPanel);
                hostControl.Controls.Add(centralDocumentPanel);
            }

            centralDocumentPanel.Dock = DockStyle.Fill;
            centralDocumentPanel.BackColor = SystemColors.Window;
            // Visibility is applied after successful docking configuration
            centralDocumentPanel.SendToBack();

            logger?.LogInformation("Fixed docking panels configured safely after dock state load");
        }
        finally
        {
            EnsurePanelsVisible(hostControl, leftDockPanel, rightDockPanel, centralDocumentPanel, logger);
            hostControl.ResumeLayout(true);
            hostControl.PerformLayout();
            hostControl.Invalidate(true);
        }
    }

    private static int CalculateDockSize(
        Control host,
        DockingStyle style,
        int minimumSize,
        float preferredRatio,
        float maxRatio)
    {
        var extent = style is DockingStyle.Left or DockingStyle.Right
            ? host.ClientSize.Width
            : host.ClientSize.Height;

        if (extent <= 0)
        {
            return minimumSize;
        }

        var preferredSize = (int)Math.Round(extent * preferredRatio);
        var maxSize = Math.Max(minimumSize, (int)Math.Round(extent * maxRatio));
        return Math.Clamp(preferredSize, minimumSize, maxSize);
    }

    private static bool EnsureDockedControlRegistered(
        DockingManager dockingManager,
        Control control,
        Control host,
        DockingStyle dockingStyle,
        int size,
        ILogger? logger)
    {
        if (control.IsDisposed || host.IsDisposed)
        {
            return false;
        }

        if (dockingStyle == DockingStyle.Fill)
        {
            logger?.LogWarning(
                "EnsureDockedControlRegistered: DockingStyle.Fill is handled via direct DockStyle.Fill layout for {ControlName}; DockingManager.DockControl is skipped.",
                control.Name);

            try
            {
                if (!ReferenceEquals(control.Parent, host))
                {
                    control.Parent?.Controls.Remove(control);
                    host.Controls.Add(control);
                }

                EnsurePanelHasAtLeastOneChild(control, "_fillDockFallbackPlaceholder", logger);
                control.Dock = DockStyle.Fill;
                control.Visible = true;
                control.SendToBack();
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "EnsureDockedControlRegistered: Failed direct Fill layout fallback for {ControlName}", control.Name);
                return false;
            }
        }

        try
        {
            if (ReferenceEquals(control.Parent, host) && dockingManager.GetEnableDocking(control))
            {
                return true;
            }
        }
        catch
        {
            // best effort only; proceed to re-register
        }

        if (control.Parent != null && !ReferenceEquals(control.Parent, host))
        {
            logger?.LogDebug("EnsureDockedControlRegistered: Reparenting {ControlName} from {ParentName} to docking host",
                control.Name,
                control.Parent.Name);
            control.Parent.Controls.Remove(control);
        }

        return TryDockControl(dockingManager, control, host, dockingStyle, size, logger);
    }

    private static void EnsurePanelHasAtLeastOneChild(Control panel, string placeholderName, ILogger? logger)
    {
        if (panel.IsDisposed || panel.Controls.Count > 0)
        {
            return;
        }

        panel.Controls.Add(new Panel
        {
            Dock = DockStyle.Fill,
            Name = placeholderName,
            Visible = true
        });

        logger?.LogWarning("Panel {PanelName} had no children; added fallback placeholder {PlaceholderName}", panel.Name, placeholderName);
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
            if (dockingStyle == DockingStyle.Fill)
            {
                logger?.LogWarning(
                    "TryDockControl: DockingStyle.Fill requested for {ControlName}; Syncfusion host docking path does not allow this. Skipping DockControl call.",
                    control.Name);
                return false;
            }

            if (ReferenceEquals(host, dockingManager.HostControl) && dockingStyle == DockingStyle.Fill)
            {
                logger?.LogWarning(
                    "TryDockControl: DockingStyle.Fill is not allowed directly on HostControl for {ControlName}; skipping dock request.",
                    control.Name);
                return false;
            }

            if (ReferenceEquals(host, dockingManager.HostControl) && dockingStyle == DockingStyle.Tabbed)
            {
                logger?.LogDebug("TryDockControl: HostControl does not support DockingStyle.Tabbed. Switching to DockingStyle.Right for {ControlName}.", control.Name);
                dockingStyle = DockingStyle.Right;
            }

            // CRITICAL: Ensure control has at least one child before docking to prevent DockHost paint crashes
            if (control.Controls.Count == 0)
            {
                var placeholder = new Label
                {
                    Text = "",
                    Dock = DockStyle.Fill,
                    AutoSize = false
                };
                control.Controls.Add(placeholder);
                logger?.LogDebug("Added placeholder control to {ControlName} before docking", control.Name);
            }

            // CRITICAL: Suspend layout on host control to prevent paint during docking operations
            var hostControl = dockingManager.HostControl;
            hostControl?.SuspendLayout();
            try
            {
                dockingManager.DockControl(control, host, dockingStyle, size);
            }
            finally
            {
                hostControl?.ResumeLayout(performLayout: false);
            }
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
