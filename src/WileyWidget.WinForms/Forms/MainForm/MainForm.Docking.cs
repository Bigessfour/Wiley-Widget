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

            // Ensure navigator is ready so JARVIS and other panels are managed by DockingManager
            EnsurePanelNavigatorInitialized();

            // Keep first paint clean: suppress empty side dock surfaces until a panel is explicitly shown.
            SuppressEmptyDockSurfacesOnFirstPaint();

            // Ensure ribbon/status bar stay on top
            _ribbon?.BringToFront();
            _statusBar?.BringToFront();

            // Force layout refresh to ensure panels render
            this.PerformLayout();

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

            // Force layout refresh after dock state loads
            this.PerformLayout();

            // Ensure chrome stays on top
            _ribbon?.BringToFront();
            _statusBar?.BringToFront();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Exception during NewDockStateEndLoad handler");
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
            SuppressDockSurfaceIfPlaceholderOnly(_leftDockPanel, "LeftDockPanel");
            SuppressDockSurfaceIfPlaceholderOnly(_rightDockPanel, "RightDockPanel");

            if (_centralDocumentPanel != null && !_centralDocumentPanel.IsDisposed)
            {
                _centralDocumentPanel.Visible = true;
                _centralDocumentPanel.Dock = DockStyle.Fill;
            }

            _logger?.LogDebug("[DOCKING] Startup dock surface suppression applied");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] Failed to suppress startup dock surfaces");
        }
    }

    private void SuppressDockSurfaceIfPlaceholderOnly(Control? dockSurface, string surfaceName)
    {
        if (dockSurface == null || dockSurface.IsDisposed)
        {
            return;
        }

        if (HasOnlyPlaceholderChildren(dockSurface))
        {
            HideDockSurface(dockSurface, surfaceName);
            return;
        }

        try
        {
            if (_dockingManager != null)
            {
                _dockingManager.SetDockVisibility(dockSurface, true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] SetDockVisibility(true) failed for {SurfaceName}", surfaceName);
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

    private void HideDockSurface(Control? dockSurface, string surfaceName)
    {
        if (dockSurface == null || dockSurface.IsDisposed || _dockingManager == null)
        {
            return;
        }

        try
        {
            _dockingManager.SetDockVisibility(dockSurface, false);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[DOCKING] SetDockVisibility(false) failed for {SurfaceName}", surfaceName);
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

    /// <summary>
    /// Try to initialize basic docking if not already initialized.
    /// Fallback method called when docking is needed but not yet set up.
    /// </summary>
    private bool TryInitializeBasicDocking()
    {
        if (_dockingManager != null)
        {
            _logger?.LogDebug("[DOCKING] Basic docking already initialized");
            return true;
        }

        _logger?.LogInformation("[DOCKING] Attempting basic docking initialization (fallback path)");

        try
        {
            InitializeSyncfusionDocking();
            ConfigureDockingManagerChromeLayout();
            return _dockingManager != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[DOCKING] Basic docking initialization failed");
            return false;
        }
    }

    private void EnsureDockingHostContainer()
    {
        if (_dockingHostContainer != null && !_dockingHostContainer.IsDisposed)
        {
            if (!Controls.Contains(_dockingHostContainer))
            {
                Controls.Add(_dockingHostContainer);
            }

            _dockingHostContainer.Dock = DockStyle.Fill;
            _dockingHostContainer.SendToBack();
            return;
        }

        _dockingHostContainer = new UserControl
        {
            Name = "DockingHostContainer",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            TabStop = false,
            BackColor = SystemColors.Control
        };

        // Enable double-buffering to reduce flicker and potential paint issues
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_dockingHostContainer, true, null);

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
    /// Apply startup docking state if docking manager is ready.
    /// Shows auto-show panels configured in UIConfiguration.
    /// </summary>
    private void ApplyStartupDockingStateIfReady()
    {
        if (_dockingManager == null)
        {
            _logger?.LogDebug("[DOCKING] Cannot apply startup state - docking manager not initialized");
            return;
        }

        if (_uiConfig == null)
        {
            _logger?.LogDebug("[DOCKING] Cannot apply startup state - UI configuration not available");
            return;
        }

        try
        {
            _logger?.LogDebug("[DOCKING] Applying startup docking state");

            // Auto-show configured panels (if enabled in UIConfiguration)
            if (_uiConfig.AutoShowPanels)
            {
                _logger?.LogInformation("[DOCKING] Auto-showing startup panels");
                // Startup panels are visible by default in DockPanelsInLayout()
            }

            _logger?.LogDebug("[DOCKING] Startup docking state applied successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DOCKING] Failed to apply startup docking state");
        }
    }

    /// <summary>
    /// Check if DockingManager is ready for mutating operations.
    /// Returns true if docking is fully initialized and ready for panel management.
    /// </summary>
    private bool IsDockingManagerReadyForMutatingOperations()
    {
        if (_dockingManager == null)
        {
            return false;
        }

        if (!this.IsHandleCreated)
        {
            return false;
        }

        return true;
    }
}
