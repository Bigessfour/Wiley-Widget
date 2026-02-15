// MainForm.Docking.cs - Clean, minimal Syncfusion DockingManager implementation (v32.x+ compatible)
// Based directly on Syncfusion's official Getting Started example:
// https://help.syncfusion.com/windowsforms/docking-manager/getting-started

using System;
using System.ComponentModel;
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
    // NOTE: DockingClientPanel is created by DockingHostFactory and accessed via _dockingManager.HostControl
    // No separate field needed - Syncfusion manages the client panel automatically via SizeToFit
    private LegacyGradientPanel? _leftDockPanel;         // Navigation
    private LegacyGradientPanel? _centralDocumentPanel;  // Main content area (can host TabControl later)
    private LegacyGradientPanel? _rightDockPanel;        // Activity Log container
    private ActivityLogPanel? _activityLogPanel; // Activity Log panel instance
    private System.Windows.Forms.Timer? _activityRefreshTimer; // Activity log refresh timer
    private bool _dockStateLoadCompleted; // Tracks when NewDockStateEndLoad fires - signals docking is ready for mutations
    private Control? _dockingHostControl; // Cached DockingClientPanel host used for layout integration

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
            // Pass MainForm as dockingHost - DockingHostFactory will create DockingClientPanel automatically
            // This ensures SizeToFit integration and proper Syncfusion docking architecture
            SuspendLayout();
            try
            {
                // Use DockingHostFactory for consistent initialization (same path as integration tests)
                // Passing 'this' (MainForm) triggers DockingClientPanel creation with SizeToFit=true
                (_dockingManager, _leftDockPanel, _rightDockPanel, _centralDocumentPanel, _activityLogPanel, _activityRefreshTimer, _) =
                    DockingHostFactory.CreateDockingHost(
                        this,
                        _serviceProvider ?? throw new InvalidOperationException("ServiceProvider required for docking initialization"),
                        _panelNavigator,
                        this, // Pass MainForm directly - factory will create DockingClientPanel
                        _logger);

                _dockingHostControl = _dockingManager?.HostControl;
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }

            _logger?.LogInformation("[DOCKING] DockingManager created via factory with all panels");

            // Subscribe to available DockingManager events per Syncfusion API
            // Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
            if (_dockingManager != null)
            {
                // State change events (confirmed available in Syncfusion.Windows.Forms.Tools)
                _dockingManager.NewDockStateEndLoad += OnDockStateEndLoad;
                _dockingManager.DockStateChanged += OnDockStateChanged;
                _dockingManager.DockVisibilityChanged += OnDockVisibilityChanged;
                _dockingManager.DockControlActivated += OnDockControlActivated;
                _dockingManager.DockMenuClick += OnDockMenuClick;
                
                _logger?.LogInformation("[DOCKING] Subscribed to 5 DockingManager events (state, visibility, activation, menu)");
            }

            var persistedLayoutLoaded = TryLoadPersistedDockLayoutOnStartup();

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

            if (!persistedLayoutLoaded)
            {
                MarkDockingReadyForDefaultLayout();
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
        _dockingHostControl = dockingManager?.HostControl;
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
            var context = _dockStateLoadCompleted ? "runtime" : "initialization";
            _logger?.LogInformation("[DOCKING] NewDockStateEndLoad fired ({Context}) - docking ready for mutations", context);
            _dockStateLoadCompleted = true; // Signal that docking is ready for mutations

            // Re-assert chrome z-order/layout after dock-state transitions.
            FinalizeDockingChromeLayout();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Exception during NewDockStateEndLoad handler");
        }
    }

    private void MarkDockingReadyForDefaultLayout()
    {
        if (IsDisposed || _dockingManager == null || _dockStateLoadCompleted)
        {
            return;
        }

        try
        {
            var hostControl = _dockingManager.HostControl;
            if (hostControl == null || hostControl.IsDisposed || !hostControl.IsHandleCreated || hostControl.Controls.Count == 0)
            {
                return;
            }

            _dockStateLoadCompleted = true;
            _logger?.LogInformation("[DOCKING] No persisted dock-state loaded; marking docking ready after default layout initialization");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] Default-layout readiness check failed");
        }
    }

    /// <summary>
    /// Force-marks docking as ready when DockingManager is operationally ready but NewDockStateEndLoad hasn't fired.
    /// Called from navigation paths as a safety measure to unblock navigation.
    /// </summary>
    private void ForceMarkDockingReadyIfOperational()
    {
        if (_dockStateLoadCompleted || _dockingManager == null || IsDisposed)
        {
            return;
        }

        var hostControl = _dockingManager.HostControl;
        if (hostControl != null && !hostControl.IsDisposed && hostControl.IsHandleCreated && hostControl.Controls.Count > 0)
        {
            _dockStateLoadCompleted = true;
            _logger?.LogWarning("[DOCKING] Force-marked docking as ready - NewDockStateEndLoad did not fire but DockingManager is operational");
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

            // DockingClientPanel (accessed via DockingManager.HostControl) goes in the middle row
            // Note: DockingClientPanel is added later during InitializeSyncfusionDocking
            // This section ensures it's in the correct TableLayoutPanel cell
            // SizeToFit=true handles automatic layout, no manual Dock assignment needed

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
    /// <summary>
    /// Finalizes chrome layout for ribbon, status bar, and docking integration.
    /// DockingClientPanel (accessed via DockingManager.HostControl) uses SizeToFit for automatic layout.
    /// No manual z-order or Dock assignments needed - Syncfusion handles this.
    /// 
    /// SYNCFUSION INTEGRATION: DockingClientPanel.SizeToFit automatically manages layout
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-client-panel
    /// </summary>
    private void FinalizeDockingChromeLayout()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // Access DockingClientPanel via HostControl (with validation/fallback)
            var hostControl = GetValidDockingHostControl();

            // If using TableLayoutPanel, ensure docking host is in the layout
            if (_mainLayoutPanel != null && !_mainLayoutPanel.IsDisposed)
            {
                if (hostControl != null && !hostControl.IsDisposed)
                {
                    if (!_mainLayoutPanel.Controls.Contains(hostControl))
                    {
                        _logger?.LogDebug("[DOCKING] Adding DockingClientPanel to TableLayoutPanel row 1");
                        _mainLayoutPanel.Controls.Add(hostControl, 0, 1);
                        // No Dock assignment needed - DockingClientPanel.SizeToFit handles layout
                    }
                }

                // TableLayoutPanel handles vertical stacking - just refresh layout
                _mainLayoutPanel.PerformLayout();
                PerformLayout();
                Invalidate(true);

                _logger?.LogDebug("[DOCKING] Layout finalized using TableLayoutPanel with DockingClientPanel.SizeToFit");
                return;
            }

            // Fallback: Add DockingClientPanel directly to form if TableLayoutPanel not available
            if (hostControl != null && !hostControl.IsDisposed)
            {
                if (!Controls.Contains(hostControl))
                {
                    Controls.Add(hostControl);
                    _logger?.LogDebug("[DOCKING] Added DockingClientPanel directly to MainForm (TableLayoutPanel fallback)");
                }
                // No Dock assignment needed - SizeToFit=true handles automatic layout
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

            // Final layout pass - trigger on HostControl
            hostControl?.PerformLayout();
            PerformLayout();
            Invalidate(true);

            _logger?.LogDebug("[DOCKING] Layout finalized with DockingClientPanel.SizeToFit (legacy mode)");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] FinalizeDockingChromeLayout failed");
        }
    }

    /// <summary>
    /// Returns a usable DockingClientPanel host for layout and repairs HostControl if it was reset to a top-level form.
    /// </summary>
    private Control? GetValidDockingHostControl()
    {
        var hostControl = _dockingHostControl ?? _dockingManager?.HostControl;

        if (hostControl is Form)
        {
            _logger?.LogWarning("[DOCKING] HostControl is a top-level form; skipping layout attachment to avoid invalid parentage");
            return null;
        }

        if (hostControl != null && !hostControl.IsDisposed)
        {
            _dockingHostControl = hostControl;
            return hostControl;
        }

        // Recover by locating the DockingClientPanel already created by DockingHostFactory
        foreach (Control child in Controls)
        {
            if (child is DockingClientPanel panel && !panel.IsDisposed)
            {
                _dockingHostControl = panel;
                _logger?.LogInformation("[DOCKING] Recovered DockingClientPanel host for layout integration");
                return panel;
            }
        }

        _logger?.LogWarning("[DOCKING] No valid DockingClientPanel host found; skipping layout update");
        return null;
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

            // DockingClientPanel is owned by DockingManager and will be disposed automatically
            // No manual disposal needed - Syncfusion manages this

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

    // REMOVED: EnsureDockingHostContainer() - obsolete, DockingClientPanel now created by DockingHostFactory

    /// <summary>
    /// Attempts to update dock visibility only when DockingManager is fully ready.
    /// Uses Syncfusion SetDockVisibility API for all docked panels.
    /// 
    /// SYNCFUSION API: DockingManager.SetDockVisibility(Control, bool)
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
    /// </summary>
    private bool TrySetDockVisibilitySafe(Control control, bool visible, string context)
    {
        if (control == null || control.IsDisposed)
        {
            return false;
        }

        if (!IsDockingManagerReadyForMutatingOperations() || _dockingManager == null)
        {
            // Fallback to manual visibility when DockingManager not ready
            control.Visible = visible;
            _logger?.LogDebug("[DOCKING] Fallback to manual visibility for {ControlName} - DockingManager not ready", control.Name);
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
            // Use Syncfusion API for all docked controls (left, right, and dynamically docked panels)
            // Central panel (Fill style) is not managed by DockingManager, handle separately
            if (ReferenceEquals(control, _centralDocumentPanel))
            {
                control.Visible = visible;
                _logger?.LogDebug("[DOCKING] Central panel visibility set to {Visible} (not docked)", visible);
                return false;
            }

            // Use Syncfusion SetDockVisibility for left, right, and all docked panels
            if (_dockingManager.GetEnableDocking(control))
            {
                _dockingManager.SetDockVisibility(control, visible);
                _logger?.LogDebug("[DOCKING] SetDockVisibility({Visible}) called for {ControlName} via Syncfusion API", visible, control.Name);
                return true;
            }
            else
            {
                // Control not docked, use manual visibility
                control.Visible = visible;
                _logger?.LogDebug("[DOCKING] Manual visibility for {ControlName} (not docked)", control.Name);
                return false;
            }
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

        var hostControl = _dockingManager.HostControl;
        if (hostControl == null || hostControl.IsDisposed || !hostControl.IsHandleCreated)
        {
            return false;
        }

        if (hostControl.Controls.Count == 0)
        {
            return false;
        }

        // RELAXED: Allow navigation even if _dockStateLoadCompleted is false
        // The flag is advisory only - DockingManager can handle operations after HostControl is ready
        // Original strict check was preventing navigation when docking was actually ready
        // Keep the flag for logging purposes but don't block navigation
        if (!_dockStateLoadCompleted)
        {
            _logger?.LogDebug("[DOCKING] Navigation proceeding before NewDockStateEndLoad fired - DockingManager is operationally ready");
        }

        return true;
    }

    #region Syncfusion DockingManager Event Handlers

    /// <summary>
    /// Event handler for DockingManager.DockStateChanged.
    /// Tracks when docked controls change state (docked/floating/auto-hidden).
    /// 
    /// SYNCFUSION EVENT: DockStateChanged
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
    /// </summary>
    private void OnDockStateChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("[DOCKING_EVENT] DockStateChanged fired");
            // Event args properties are accessed dynamically if needed
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING_EVENT] DockStateChanged handler failed");
        }
    }

    /// <summary>
    /// Event handler for DockingManager.DockVisibilityChanged.
    /// Tracks when docked controls visibility changes.
    /// 
    /// SYNCFUSION EVENT: DockVisibilityChanged
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
    /// </summary>
    private void OnDockVisibilityChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("[DOCKING_EVENT] DockVisibilityChanged fired");
            // Event args properties are accessed dynamically if needed
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING_EVENT] DockVisibilityChanged handler failed");
        }
    }

    /// <summary>
    /// Event handler for DockingManager.DockControlActivated.
    /// Tracks when docked controls are activated (gain focus).
    /// 
    /// SYNCFUSION EVENT: DockControlActivated
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
    /// </summary>
    private void OnDockControlActivated(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("[DOCKING_EVENT] DockControlActivated fired");
            // Event args properties are accessed dynamically if needed
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING_EVENT] DockControlActivated handler failed");
        }
    }

    /// <summary>
    /// Event handler for DockingManager.DockMenuClick.
    /// Handles context menu item clicks on docked panels.
    /// 
    /// SYNCFUSION EVENT: DockMenuClick
    /// Reference: https://help.syncfusion.com/windowsforms/docking-manager/docking-events
    /// </summary>
    private void OnDockMenuClick(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("[DOCKING_EVENT] DockMenuClick fired");
            // Optional: Custom context menu actions, logging, etc.
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING_EVENT] DockMenuClick handler failed");
        }
    }

    #endregion

}
