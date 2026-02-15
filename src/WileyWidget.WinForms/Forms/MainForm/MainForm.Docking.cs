// MainForm.Docking.cs - Clean, minimal Syncfusion DockingManager implementation (v32.x+ compatible)
// Based directly on Syncfusion's official Getting Started example:
// https://help.syncfusion.com/windowsforms/docking-manager/getting-started

using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    // Core docking components (managed by DockingHostFactory)
    private DockingManager? _dockingManager;
    private UserControl? _dockingHostContainer;
    private LegacyGradientPanel? _leftDockPanel;         // Navigation
    private LegacyGradientPanel? _centralDocumentPanel;  // Main content area (can host TabControl later)
    private LegacyGradientPanel? _rightDockPanel;        // Activity Log container
    private ActivityLogPanel? _activityLogPanel; // Activity Log panel instance
    private System.Windows.Forms.Timer? _activityRefreshTimer; // Activity log refresh timer
    private bool _dockStateLoadCompleted; // Tracks when NewDockStateEndLoad fires - signals docking is ready for mutations

    /// <summary>
    /// Initialize Syncfusion DockingManager using DockingHostFactory.
    /// Call this from OnLoad after chrome initialization.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        if (_dockingManager != null)
        {
            _logger?.LogDebug("InitializeSyncfusionDocking skipped - docking already initialized");
            return;
        }

        _logger?.LogInformation("[DOCKING] Starting Syncfusion DockingManager initialization via DockingHostFactory");

        try
        {
            EnsureDockingHostContainer();
            ContainerControl dockingHost = this;
            if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
            {
                dockingHost = _dockingHostContainer;
            }

            // Use DockingHostFactory for consistent initialization (same path as integration tests)
            (_dockingManager, _leftDockPanel, _rightDockPanel, _centralDocumentPanel, _activityLogPanel, _activityRefreshTimer, _) =
                DockingHostFactory.CreateDockingHost(
                    this,
                    _serviceProvider ?? throw new InvalidOperationException("ServiceProvider required for docking initialization"),
                    _panelNavigator,
                    dockingHost,
                    _logger);

            _logger?.LogInformation("[DOCKING] DockingManager created via factory with all panels");

            // Subscribe to NewDockStateEndLoad for layout state management
            if (_dockingManager != null)
            {
                _dockingManager.NewDockStateEndLoad += OnDockStateEndLoad;
                _logger?.LogDebug("[DOCKING] Subscribed to NewDockStateEndLoad event");
            }

            // Apply global theme to all docking components
            ApplyDockingTheme();

            // TableLayoutPanel-based main layout is initialized during InitializeChrome().
            // At this point we only need to ensure the docking host is placed into row 1.

            // Ensure navigator is ready so JARVIS and other panels are managed by DockingManager
            EnsurePanelNavigatorInitialized();

            // Keep first paint clean: suppress empty side dock surfaces until a panel is explicitly shown.
            // Run deferred so DockingManager internal control lists are stable before visibility mutations.
            if (IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)SuppressEmptyDockSurfacesOnFirstPaint);
            }
            else
            {
                SuppressEmptyDockSurfacesOnFirstPaint();
            }

            // Explicitly re-assert chrome z-order + layout so docking host starts below the ribbon.
            FinalizeDockingChromeLayout();

            // Run one deferred pass after first layout so Ribbon final height is respected
            // before DockingManager performs its stable arrangement.
            if (IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)FinalizeDockingChromeLayout);
            }

            _logger?.LogInformation("[DOCKING] Syncfusion DockingManager initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[DOCKING] Failed to initialize Syncfusion DockingManager");
            throw;
        }
    }

    /// <summary>
    /// Registers docking resources created outside of MainForm lifecycle methods.
    /// Ensures OnHandleDestroyed/Dispose can clean up Syncfusion objects reliably.
    /// </summary>
    internal void RegisterDockingResources(
        DockingManager dockingManager,
        LegacyGradientPanel? leftDockPanel,
        LegacyGradientPanel? rightDockPanel,
        LegacyGradientPanel? centralDocumentPanel,
        ActivityLogPanel? activityLogPanel,
        System.Windows.Forms.Timer? activityRefreshTimer)
    {
        _dockingManager = dockingManager;
        _leftDockPanel = leftDockPanel;
        _rightDockPanel = rightDockPanel;
        _centralDocumentPanel = centralDocumentPanel;
        _activityLogPanel = activityLogPanel;
        _activityRefreshTimer = activityRefreshTimer;
    }

    /// <summary>
    /// Event handler for DockingManager.NewDockStateEndLoad.
    /// Ensures layout is properly applied after dock state changes.
    /// </summary>
    private void OnDockStateEndLoad(object? sender, EventArgs e)
    {
        if (IsDisposed || _dockingManager == null)
        {
            return;
        }

        try
        {
            _logger?.LogDebug("[DOCKING] NewDockStateEndLoad fired - ensuring layout consistency");
            _dockStateLoadCompleted = true; // Signal that docking is ready for mutations

            // Re-assert chrome z-order/layout after dock-state transitions.
            FinalizeDockingChromeLayout();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Exception during NewDockStateEndLoad handler");
        }
    }

    /// <summary>
    /// Initialize main layout using TableLayoutPanel for reliable vertical stacking of chrome components.
    /// This eliminates the need for complex z-order manipulation and guarantees that the docking area
    /// always starts exactly at the bottom of the ribbon.
    /// Call this from InitializeChrome after ribbon and status bar are created.
    /// </summary>
    private void InitializeMainLayout()
    {
        if (_mainLayoutPanel != null && !_mainLayoutPanel.IsDisposed)
        {
            _logger?.LogDebug("[LAYOUT] InitializeMainLayout skipped - layout panel already exists");
            return;
        }

        _logger?.LogInformation("[LAYOUT] Initializing TableLayoutPanel-based main layout");

        SuspendLayout();

        try
        {
            // Create the layout panel - it will own the vertical stacking
            _mainLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Name = "MainLayoutPanel"
            };

            // Row styles: ribbon auto, docking fill, status auto
            _mainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Row 0: Ribbon
            _mainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Row 1: Docking host
            _mainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Row 2: Status bar

            _mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _logger?.LogDebug("[LAYOUT] Created TableLayoutPanel with 3 rows (ribbon/docking/status)");

            // Add layout panel to form FIRST (becomes background)
            if (!Controls.Contains(_mainLayoutPanel))
            {
                Controls.Add(_mainLayoutPanel);
                _logger?.LogDebug("[LAYOUT] Added TableLayoutPanel to form controls");
            }

            // Move existing chrome controls into the table cells
            if (_ribbon != null && !_ribbon.IsDisposed)
            {
                _logger?.LogDebug("[LAYOUT] Moving ribbon to TableLayoutPanel row 0");
                if (Controls.Contains(_ribbon))
                {
                    Controls.Remove(_ribbon);
                }
                _mainLayoutPanel.Controls.Add(_ribbon, 0, 0);
                // Ribbon uses Syncfusion.Windows.Forms.Tools.DockStyleEx, not System.Windows.Forms.DockStyle
                _ribbon.Dock = Syncfusion.Windows.Forms.Tools.DockStyleEx.Fill;  // Fill its cell
            }

            if (_statusBar != null && !_statusBar.IsDisposed)
            {
                _logger?.LogDebug("[LAYOUT] Moving status bar to TableLayoutPanel row 2");
                if (Controls.Contains(_statusBar))
                {
                    Controls.Remove(_statusBar);
                }
                _mainLayoutPanel.Controls.Add(_statusBar, 0, 2);
                _statusBar.Dock = DockStyle.Fill;
            }

            // Docking host container goes in the middle row
            if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
            {
                _logger?.LogDebug("[LAYOUT] Moving docking host container to TableLayoutPanel row 1");
                if (Controls.Contains(_dockingHostContainer))
                {
                    Controls.Remove(_dockingHostContainer);
                }
                _mainLayoutPanel.Controls.Add(_dockingHostContainer, 0, 1);
                _dockingHostContainer.Dock = DockStyle.Fill;
            }

            // Bring layout panel to back so chrome can paint over if needed (usually not required)
            _mainLayoutPanel.SendToBack();

            _logger?.LogInformation("[LAYOUT] TableLayoutPanel initialization complete - ribbon/docking/status properly stacked");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[LAYOUT] Failed to initialize TableLayoutPanel layout");
            throw;
        }
        finally
        {
            ResumeLayout(true);
            PerformLayout();
        }
    }

    /// <summary>
    /// Ensures dock host fill panel stays behind top/bottom chrome and forces a layout pass
    /// so docked panel captions are not clipped under the ribbon.
    /// NOTE: With TableLayoutPanel-based layout, complex z-order manipulation is no longer needed.
    /// This method now performs minimal layout validation and refresh.
    /// </summary>
    private void FinalizeDockingChromeLayout()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // If using TableLayoutPanel, ensure docking host is in the layout
            if (_mainLayoutPanel != null && !_mainLayoutPanel.IsDisposed)
            {
                if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
                {
                    if (!_mainLayoutPanel.Controls.Contains(_dockingHostContainer))
                    {
                        _logger?.LogDebug("[DOCKING] Adding docking host container to TableLayoutPanel row 1");
                        _mainLayoutPanel.Controls.Add(_dockingHostContainer, 0, 1);
                        _dockingHostContainer.Dock = DockStyle.Fill;
                    }
                }

                // TableLayoutPanel handles vertical stacking - just refresh layout
                _mainLayoutPanel.PerformLayout();
                PerformLayout();
                Invalidate(true);

                _logger?.LogDebug("[DOCKING] Layout finalized using TableLayoutPanel");
                return;
            }

            // Fallback: Legacy z-order approach if TableLayoutPanel is not initialized
            // (should not happen in normal flow, but kept for safety)
            if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
            {
                if (!Controls.Contains(_dockingHostContainer))
                {
                    Controls.Add(_dockingHostContainer);
                    _dockingHostContainer.Dock = DockStyle.Fill;
                }
            }

            // Bring chrome elements to visual front for painting
            if (_ribbon != null && !_ribbon.IsDisposed && _ribbon.Visible && _ribbon.IsHandleCreated)
            {
                _ribbon.BringToFront();
            }

            if (_statusBar != null && !_statusBar.IsDisposed && _statusBar.Visible)
            {
                _statusBar.BringToFront();
            }

            if (_menuStrip != null && !_menuStrip.IsDisposed && _menuStrip.Visible)
            {
                _menuStrip.BringToFront();
            }

            if (_navigationStrip != null && !_navigationStrip.IsDisposed && _navigationStrip.Visible)
            {
                _navigationStrip.BringToFront();
            }

            // Final layout pass
            _dockingHostContainer?.PerformLayout();
            PerformLayout();
            Invalidate(true);

            _logger?.LogDebug("[DOCKING] Layout finalized (legacy mode)");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] FinalizeDockingChromeLayout failed");
        }
    }

    // Panel creation, configuration, and layout methods removed - now handled by DockingHostFactory

    /// <summary>
    /// Apply global theme to all docking components via SfSkinManager.
    /// Theme cascades automatically from SfSkinManager.ApplicationVisualTheme set in Program.cs.
    /// </summary>
    private void ApplyDockingTheme()
    {
        _logger?.LogDebug("[DOCKING] Applying global theme to docking components");

        try
        {
            // Get global theme (already set in Program.cs via SfSkinManager.ApplicationVisualTheme)
            var themeName = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme
                ?? _themeService?.CurrentTheme
                ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            // Ensure DockingManager uses the global theme
            if (_dockingManager != null)
            {
                _dockingManager.ThemeName = themeName;
            }

            // Theme automatically cascades from ApplicationVisualTheme to all controls
            _logger?.LogInformation("[DOCKING] Global theme '{ThemeName}' applied to docking (cascades from Program.cs)", themeName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Failed to apply theme to docking components");
            // Continue - theme is cosmetic
        }
    }

    /// <summary>
    /// Hides placeholder dock surfaces on startup to avoid blank white dock regions on first paint.
    /// Panels are shown later by navigation actions.
    /// </summary>
    private void SuppressEmptyDockSurfacesOnFirstPaint()
    {
        if (_dockingManager == null)
        {
            return;
        }

        try
        {
            if (_centralDocumentPanel != null && !_centralDocumentPanel.IsDisposed)
            {
                _centralDocumentPanel.Visible = true;
                _centralDocumentPanel.Dock = DockStyle.Fill;
            }

            SuppressDockSurfaceIfPlaceholderOnly(_leftDockPanel);
            SuppressDockSurfaceIfPlaceholderOnly(_rightDockPanel);

            _logger?.LogDebug("[DOCKING] Startup dock surface suppression applied");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] Failed to suppress startup dock surfaces");
        }
    }

    private void SuppressDockSurfaceIfPlaceholderOnly(Control? dockSurface)
    {
        if (dockSurface == null || dockSurface.IsDisposed)
        {
            return;
        }

        if (HasOnlyPlaceholderChildren(dockSurface))
        {
            HideDockSurface(dockSurface);
            return;
        }

        dockSurface.Visible = true;
    }

    private static bool HasOnlyPlaceholderChildren(Control dockSurface)
    {
        if (dockSurface.Controls.Count == 0)
        {
            return true;
        }

        foreach (Control child in dockSurface.Controls)
        {
            if (child == null || child.IsDisposed)
            {
                continue;
            }

            var childName = child.Name ?? string.Empty;
            var isPlaceholder =
                childName.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                childName.StartsWith("_", StringComparison.Ordinal);

            if (!isPlaceholder)
            {
                return false;
            }
        }

        return true;
    }

    private void HideDockSurface(Control? dockSurface)
    {
        if (dockSurface == null || dockSurface.IsDisposed)
        {
            return;
        }

        dockSurface.Visible = false;
    }

    /// <summary>
    /// Configure DockingManager chrome layout (z-order management).
    /// Ensures ribbon and status bar stay on top of docking host.
    /// </summary>
    private void ConfigureDockingManagerChromeLayout()
    {
        _logger?.LogDebug("[DOCKING] Configuring chrome z-order");

        try
        {
            // Bring chrome elements to front
            _ribbon?.BringToFront();
            _statusBar?.BringToFront();

            _ribbon?.BringToFront();
            _statusBar?.BringToFront();

            // Force layout refresh
            this.PerformLayout();

            _logger?.LogDebug("[DOCKING] Chrome z-order configured successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Failed to configure chrome z-order");
            // Continue - z-order issues are usually cosmetic
        }
    }

    /// <summary>
    /// Dispose Syncfusion docking resources.
    /// Called from OnHandleDestroyed to prevent Paint NRE (Syncfusion recommended pattern).
    /// Disposing after handle destruction prevents paint events on disposed controls.
    /// </summary>
    private void DisposeSyncfusionDockingResources()
    {
        _logger?.LogDebug("[DOCKING] Disposing Syncfusion docking resources");

        try
        {
            // Unsubscribe from events
            if (_dockingManager != null)
            {
                _dockingManager.NewDockStateEndLoad -= OnDockStateEndLoad;
            }

            // Dispose activity refresh timer
            _activityRefreshTimer?.Dispose();
            _activityRefreshTimer = null;

            // Dispose DockingManager first (releases all docked controls)
            if (_dockingManager != null)
            {
                _dockingManager.Dispose();
                _dockingManager = null;
                _logger?.LogDebug("[DOCKING] DockingManager disposed");
            }

            // Dispose panels (if not already disposed by DockingManager)
            _leftDockPanel?.Dispose();
            _leftDockPanel = null;

            _centralDocumentPanel?.Dispose();
            _centralDocumentPanel = null;

            _rightDockPanel?.Dispose();
            _rightDockPanel = null;

            _activityLogPanel?.Dispose();
            _activityLogPanel = null;

            if (_dockingHostContainer != null)
            {
                _dockingHostContainer.Dispose();
                _dockingHostContainer = null;
            }

            _logger?.LogInformation("[DOCKING] All Syncfusion docking resources disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Exception during docking resources disposal");
            // Continue - disposal exceptions should not block cleanup
        }
    }

    /// <summary>
    /// Event handler for theme changes.
    /// Re-applies theme to all docking components when theme changes at runtime.
    /// </summary>
    private void OnThemeChanged(object? sender, string themeName)
    {
        _logger?.LogDebug("[DOCKING] Theme changed event received - re-applying theme '{ThemeName}' to docking components", themeName);

        try
        {
            ApplyDockingTheme();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Failed to apply theme changes to docking components");
        }
    }

    private void EnsureDockingHostContainer()
    {
        if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
        {
            if (!Controls.Contains(_dockingHostContainer))
            {
                // Add to Controls but DON'T set Dock yet
                Controls.Add(_dockingHostContainer);
            }

            // Set Dock property after it's in the collection
            _dockingHostContainer.Dock = DockStyle.Fill;
            _dockingHostContainer.Margin = Padding.Empty;
            _dockingHostContainer.Padding = Padding.Empty;
            // Don't call SendToBack here - z-order will be set explicitly in FinalizeDockingChromeLayout
            return;
        }

        _dockingHostContainer = new UserControl
        {
            Name = "_dockingClientPanel",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            TabStop = false,
            BackColor = SystemColors.Control
        };

        Controls.Add(_dockingHostContainer);

        // Permanent hidden placeholder ensures HostControl is never empty (prevents Syncfusion paint NRE)
        var hostPlaceholder = new Panel
        {
            Name = "DockingHostPermanentPlaceholder",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = SystemColors.Control
        };
        _dockingHostContainer.Controls.Add(hostPlaceholder);
        hostPlaceholder.SendToBack();

        _dockingHostContainer.SendToBack();
    }

    /// <summary>
    /// Attempts to update dock visibility only when DockingManager is fully ready.
    /// Falls back to plain Control.Visible assignment if manager state is not stable.
    /// </summary>
    private bool TrySetDockVisibilitySafe(Control control, bool visible, string context)
    {
        if (control == null || control.IsDisposed)
        {
            return false;
        }

        if (ReferenceEquals(control, _leftDockPanel) ||
            ReferenceEquals(control, _rightDockPanel) ||
            ReferenceEquals(control, _centralDocumentPanel))
        {
            control.Visible = visible;
            return false;
        }

        if (!IsDockingManagerReadyForMutatingOperations() || _dockingManager == null)
        {
            control.Visible = visible;
            return false;
        }

        var hostControl = _dockingManager.HostControl;
        if (hostControl == null || hostControl.IsDisposed || hostControl.Controls.Count == 0)
        {
            _logger?.LogDebug("[DOCKING] Skipping SetDockVisibility for {ControlName} during {Context}: HostControl not ready", control.Name, context);
            control.Visible = visible;
            return false;
        }

        if (control.Parent == null || control.Parent.IsDisposed)
        {
            _logger?.LogDebug("[DOCKING] Skipping SetDockVisibility for {ControlName} during {Context}: parent not ready", control.Name, context);
            control.Visible = visible;
            return false;
        }

        try
        {
            _dockingManager.SetDockVisibility(control, visible);
            control.Visible = visible;
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger?.LogWarning(ex,
                "[DOCKING] SetDockVisibility deferred for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            control.Visible = visible;
            return false;
        }
        catch (DockingManagerException ex)
        {
            _logger?.LogDebug(ex,
                "[DOCKING] SetDockVisibility failed for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            control.Visible = visible;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex,
                "[DOCKING] Unexpected SetDockVisibility failure for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            control.Visible = visible;
            return false;
        }
    }

    /// <summary>
    /// Check if DockingManager is ready for mutating operations.
    /// </summary>
    private bool IsDockingManagerReadyForMutatingOperations()
    {
        if (_dockingManager == null)
        {
            return false;
        }

        if (!IsHandleCreated)
        {
            return false;
        }

        // CRITICAL: Wait for NewDockStateEndLoad to fire before allowing mutations
        // This prevents race conditions where panels dock before internal docking state is ready
        if (!_dockStateLoadCompleted)
        {
            return false;
        }

        return true;
    }

}
