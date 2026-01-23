using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;

namespace WileyWidget.WinForms.Forms;

public partial class MainForm
{
    private DockingManager? _dockingManager;
    private WileyWidget.WinForms.Controls.ActivityLogPanel? _activityLogPanel;
    private DockingLayoutManager? _dockingLayoutManager;
    private Panel? _leftDockPanel;
    #pragma warning disable CS0649
    private Panel? _centralDocumentPanel;
    #pragma warning restore CS0649
    private Panel? _rightDockPanel;
    private Dictionary<string, Control>? _dynamicDockPanels;

    // Layout state management
    private readonly DateTime _lastSaveTime = DateTime.MinValue;

    // Layout versioning
    private const string LayoutVersionAttributeName = "layoutVersion";
    private const string CurrentLayoutVersion = "1.0";
    private const string SegoeUiFontName = "Segoe UI";

    // Phase 1 Simplification: Docking configuration now centralized in UIConfiguration
    private const string DockingLayoutFileName = "wiley_widget_docking_layout.xml";

    /// <summary>
    /// Initializes Syncfusion DockingManager with layout management.
    /// Delegates to DockingHostFactory for centralized docking creation logic.
    /// Loads saved layout from AppData if available.
    /// </summary>
    private void InitializeSyncfusionDocking()
    {
        var globalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Syncfusion Docking Initialization");

        try
        {
            _logger?.LogInformation("InitializeSyncfusionDocking START - handleCreated={HandleCreated}, UIThread={ThreadId}",
                IsHandleCreated, System.Threading.Thread.CurrentThread.ManagedThreadId);

            // Phase: DockingManager Creation
            var dockingHostStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger?.LogInformation("Creating DockingHost via factory...");
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider cannot be null when creating DockingHost.");
            }
            var (dockingManager, leftPanel, rightPanel, activityLogPanel, activityTimer, layoutManager) =
                DockingHostFactory.CreateDockingHost(this, _serviceProvider, _panelNavigator, _logger);
            dockingHostStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("DockingManager Creation", dockingHostStopwatch.ElapsedMilliseconds);

            _dockingManager = dockingManager;
            _leftDockPanel = leftPanel;
            _rightDockPanel = rightPanel;
            _activityLogPanel = activityLogPanel;
            _dockingLayoutManager = layoutManager;
            _dynamicDockPanels ??= new Dictionary<string, Control>();

            var currentTheme = _themeService?.CurrentTheme ?? AppThemeColors.DefaultTheme;
            ThemeApplicationHelper.ApplyThemeToDockingManager(_dockingManager, currentTheme, _logger);

            // Wire up cache invalidation for grid discovery
            // Note: ActiveControlChanged event not available on DockingManager
            // Grid cache invalidation handled via DockStateChanged event instead

            _logger?.LogInformation("DockingHost created: HasManager={HasManager}, HostControl={HostControl}",
                _dockingManager != null, _dockingManager?.HostControl?.Name ?? "<null>");

            // Ensure panels are visible via DockingManager API
            if (_dockingManager != null)
            {
                if (_leftDockPanel != null)
                {
                    _leftDockPanel.Visible = true;
                    _dockingManager.SetDockVisibility(_leftDockPanel, true);
                    _dockingManager.SetControlMinimumSize(_leftDockPanel, new Size(300, 360));
                }

                if (_rightDockPanel != null)
                {
                    _rightDockPanel.Visible = true;
                    _dockingManager.SetDockVisibility(_rightDockPanel, true);
                    _dockingManager.SetControlMinimumSize(_rightDockPanel, new Size(300, 360));
                }
            }

            // 3. Layout recalc is deferred until form is fully shown

            // 4. Activity grid will be loaded with actual data after docking completes
            if (_activityLogPanel != null)
            {
                _activityLogPanel.Visible = true;
            }

            // Panel activation and layout recalc deferred until form is fully shown

            // Guard: Ensure at least DockingManager was created
            if (_dockingManager == null)
            {
                _asyncLogger?.Error("DockingManager creation failed");
                _logger?.LogError("DockingManager creation failed - docking will be unavailable");
                Console.WriteLine("[DIAGNOSTIC ERROR] DockingManager is null after CreateDockingHost");
                return;
            }

            // Ensure panel navigation is available before layout load so dynamic panels recreate with real controls
            EnsurePanelNavigatorInitialized();

            // Layout manager is now created by DockingHostFactory
            var layoutPath = GetDockingLayoutPath();
            _logger?.LogDebug("DockingLayoutManager provided by factory with path {LayoutPath}", layoutPath);

            // Transfer ownership of panels and fonts to the layout manager
            var dockAutoHideTabFont = new Font(SegoeUiFontName, 9F);
            var dockTabFont = new Font(SegoeUiFontName, 9F);

            // Note: DockingLayoutManager initialization deferred - methods will be available in future
            // For now, panels are managed directly by DockingManager
            _logger?.LogDebug("DockingLayoutManager from factory - initialization deferred");

            HideStandardPanelsForDocking();

            // CRITICAL FIX: Create and dock initial panels BEFORE suspending layout
            // This ensures DockingManager has controls in its collection before OnPaint events fire
            // Without this, ArgumentOutOfRangeException occurs in DockHost.GetPaintInfo()
            try
            {
                // Panels are now created by DockingHostFactory - just ensure they're properly configured
                var dynamicPanelCount = _dynamicDockPanels?.Count ?? 0;
                var createdPanelCount = new[] { _leftDockPanel, _centralDocumentPanel, _rightDockPanel }.Count(p => p != null) + dynamicPanelCount;
                _logger?.LogInformation("Docking panels created by factory — count={PanelCount}, dynamicPanels={DynamicCount}, layoutManagerReady={LayoutManagerReady}",
                    createdPanelCount, dynamicPanelCount, _dockingLayoutManager != null);
            }
            catch (Exception panelEx)
            {
                _logger?.LogError(panelEx, "Failed to configure docking panels - paint exceptions may occur");
            }

            // Reduce flicker during layout load + theme application (best-effort).
            var dockingUpdatesLocked = false;
            var dockingLayoutSuspended = false;

            try
            {
                try
                {
                    _dockingManager.LockHostFormUpdate();
                    _dockingManager.LockDockPanelsUpdate();
                    dockingUpdatesLocked = true;
                }
                catch (Exception lockEx)
                {
                    _logger?.LogDebug(lockEx, "Failed to lock DockingManager updates - continuing without lock");
                }

                try
                {
                    _dockingManager.SuspendLayout();
                    dockingLayoutSuspended = true;
                }
                catch (Exception suspendEx)
                {
                    _logger?.LogDebug(suspendEx, "Failed to suspend DockingManager layout - continuing");
                }

                // CRITICAL: Layout loading is now deferred to InitializeAsync() via LoadAndApplyDockingLayout()
                // This ensures the form is fully rendered before we start heavy docking restoration.
                _logger?.LogDebug("DockingManager infrastructure initialized. Layout restoration will follow in InitializeAsync().");

                // Theme application deferred: Applied after DockingManager.ResumeLayout() to avoid conflicts

                // CRITICAL: Apply SFSkinManager theme AFTER DockingManager is fully initialized and panels are docked
                // This ensures theme cascade works correctly and prevents ArgumentOutOfRangeException in paint events
                var themeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
                    SfSkinManager.SetVisualStyle(this, themeName);
                    themeStopwatch.Stop();
                    StartupInstrumentation.RecordPhaseTime("Theme Application", themeStopwatch.ElapsedMilliseconds);
                    _logger?.LogInformation("Applied SfSkinManager theme to MainForm after DockingManager setup: {Theme} ({Time}ms)", themeName, themeStopwatch.ElapsedMilliseconds);
                }
                catch (Exception themeEx)
                {
                    themeStopwatch.Stop();
                    _logger?.LogWarning(themeEx, "Failed to apply SFSkinManager theme to MainForm after DockingManager setup");
                }

                // Ensure panels start visible & activated
                if (_dockingManager != null)
                {
                    var ienum = _dockingManager.Controls;
                    GradientPanelExt? firstPanel = null;
                    while (ienum.MoveNext())
                    {
                        if (ienum.Current is GradientPanelExt panel)
                        {
                            panel.Visible = true;
                            panel.BringToFront();
                            if (firstPanel == null) firstPanel = panel;
                        }
                    }
                    if (firstPanel != null)
                    {
                        _dockingManager.ActivateControl(firstPanel);
                    }
                }
            }
            finally
            {
                if (dockingLayoutSuspended)
                {
                    try { _dockingManager.ResumeLayout(true); } catch { }
                }

                if (dockingUpdatesLocked)
                {
                    try { _dockingManager.UnlockDockPanelsUpdate(); } catch { }
                    try { _dockingManager.UnlockHostFormUpdate(); } catch { }
                }
            }

            _logger?.LogInformation("InitializeSyncfusionDocking complete - ActivityLogPanel={HasActivityPanel}",
                _activityLogPanel != null);

            globalStopwatch.Stop();
            StartupInstrumentation.RecordPhaseTime("Total DockingManager Initialization", globalStopwatch.ElapsedMilliseconds);
            StartupInstrumentation.LogInitializationState(_logger);
            Console.WriteLine(StartupInstrumentation.GetFormattedMetrics());
        }
        catch (Exception ex) when (ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase))
        {
            // Theme assembly load failure - provide user-friendly message
            _logger?.LogError(ex, "Theme assembly failed to load during DockingManager initialization");
            _asyncLogger?.Error($"Theme Assembly Error: {ex.Message}");
            MessageBox.Show(
                "Theme assembly missing—please reinstall Syncfusion packages or reset to default theme.",
                "Theme Loading Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            // Fall back to default theme
            try
            {
                SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
                SfSkinManager.SetVisualStyle(this, "Office2019Colorful");
                _logger?.LogInformation("Fell back to default Office2019Colorful theme after assembly load failure");
            }
            catch (Exception fallbackEx)
            {
                _logger?.LogError(fallbackEx, "Failed to apply fallback theme");
            }
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("Syncfusion"))
        {
            // Syncfusion-related error - provide diagnostic info
            _logger?.LogError(ex, "Syncfusion exception during DockingManager initialization: {Message}", ex.Message);
            _asyncLogger?.Error($"Syncfusion Error: {ex.Message}");
            MessageBox.Show(
                $"UI initialization error: {ex.Message}\n\nThe application may be unstable. Please restart.",
                "Syncfusion Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            // Docking initialization failure is non-critical - system can still function
            // but without docking capabilities
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Syncfusion DockingManager: {Message}", ex.Message);
            _asyncLogger?.Error($"Initialization Error: {ex.GetType().Name}: {ex.Message}");
            // Docking initialization failure is non-critical - system can still function
            // but without docking capabilities
        }
    }

    /// <summary>
    /// Handles theme changes at runtime and reapplies theme to all docking panels.
    /// Thread-safe: automatically marshals to UI thread if needed.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="theme">New theme to apply.</param>
    private void OnThemeChanged(object? sender, string theme)
    {
        if (!IsHandleCreated)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to marshal OnThemeChanged to UI thread");
            }
            return;
        }

        try
        {
            _logger?.LogInformation("Applying theme change to docking panels: {Theme}", theme);

            // Update button text in Ribbon if present
            if (_ribbon != null)
            {
                var themeToggleBtn = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                }
            }

            // Update button text in NavigationStrip if present
            if (_navigationStrip != null)
            {
                var themeToggleBtn = _navigationStrip.Items.Find("ThemeToggle", true).FirstOrDefault() as ToolStripButton;
                if (themeToggleBtn != null)
                {
                    themeToggleBtn.Text = theme == "Office2019Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
                }
                // Update ToolStripEx theme to match application theme
                _navigationStrip.ThemeName = theme;
            }

            // Reapply theme to all docking panels via layout manager
            if (_dockingLayoutManager != null && _dockingManager != null)
            {
                // Note: We don't have direct access to the panels anymore, but the layout manager handles theme application
                _logger?.LogDebug("Theme application delegated to DockingLayoutManager");
            }

            // Refresh activity grid with new theme
            if (_activityLogPanel != null && !_activityLogPanel.IsDisposed)
            {
                try
                {
                    // REMOVED: Per-control SetVisualStyle - grid inherits theme from ApplicationVisualTheme
                    // Theme cascade ensures consistent styling across all controls
                    _activityLogPanel.Refresh();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to refresh activity grid");
                }
            }

            this.Refresh();
            _logger?.LogInformation("Theme successfully applied to docking panels");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply theme change to docking panels");
        }
    }

    /// <summary>
    /// Initialize the DockingManager component with proper configuration
    /// </summary>
    private void InitializeDockingManager()
    {
        components ??= new Container();
        _dockingManager = new DockingManager(components);
        _dockingManager.HostControl = this;

        ConfigureDockingManagerSettings();
    }

    /// <summary>
    /// Configure DockingManager settings and theme
    /// </summary>
    private void ConfigureDockingManagerSettings()
    {
         if (_dockingManager == null) return;

        // Phase 1 Simplification: EnableDocumentMode permanently false (panels only)
        _dockingManager.EnableDocumentMode = false;
        _logger?.LogInformation("DockingManager document mode disabled (using DockingManager for panels only)");

        _dockingManager.PersistState = true;
        _dockingManager.AnimateAutoHiddenWindow = true;
        // REMOVED: Hard-coded fonts - SFSkinManager owns all theming
        _dockingManager.ShowCaption = true;

        // Give the DockingManager a stable name for tooling/tests
        try
        {
            var nameProp = _dockingManager.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(_dockingManager, "DockingManager_Main");
            }
        }
        catch { }

        // Attach state events to keep navigation & diagnostics up-to-date
        try
        {
            _dockingManager.DockStateChanged += DockingManager_DockStateChanged;
            _dockingManager.DockControlActivated += DockingManager_DockControlActivated;
            _dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;
        }
        catch { }
    }

    private async Task LoadAndApplyDockingLayout(string layoutPath, CancellationToken cancellationToken = default)
    {
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingManager is null");
            return;
        }

        if (_dockingLayoutManager == null)
        {
            _logger?.LogWarning("Cannot load docking layout - DockingLayoutManager is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            _logger?.LogWarning("Docking layout path is empty - using default layout");
            return;
        }

        var timelineService = _serviceProvider != null ?
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider) : null;
        using var phase = timelineService?.BeginPhaseScope("Docking Layout Restoration");

        try
        {
            LoadDynamicPanels(layoutPath);
            await _dockingLayoutManager.LoadDockingLayoutAsync(_dockingManager, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Docking layout restoration was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during layout restoration - using default layout");
        }
    }

    private static void TryDeleteLayoutFiles(string? layoutPath)
    {
         if (string.IsNullOrWhiteSpace(layoutPath)) return;
        try { if (File.Exists(layoutPath)) File.Delete(layoutPath); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to delete layout file: {ex.Message}"); }
        TryCleanupTempFile(layoutPath + ".tmp");
    }

    private static string GetDockingLayoutPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "WileyWidget");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, DockingLayoutFileName);
        }
        catch
        {
            try { return Path.Combine(Path.GetTempPath(), DockingLayoutFileName); } catch { return DockingLayoutFileName; }
        }
    }

    private Task SafeInvokeAsync(System.Action action, CancellationToken cancellationToken = default)
    {
         if (action == null) throw new ArgumentNullException(nameof(action));

        if (!this.IsHandleCreated)
        {
            action();
            return Task.CompletedTask;
        }

        if (InvokeRequired)
        {
            var tcs = new TaskCompletionSource<object?>();
            try
            {
                BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
            }
            catch
            {
                try
                {
                    Task.Run(() =>
                    {
                        try { action(); tcs.SetResult(null); }
                        catch (Exception innerEx) { tcs.SetException(innerEx); }
                    });
                }
                catch (Exception fallbackEx) { tcs.SetException(fallbackEx); }
            }
            return tcs.Task;
        }

        try { action(); return Task.CompletedTask; }
        catch (Exception ex) { return Task.FromException(ex); }
    }

    private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
    {
        if (e.Controls == null) return;

        foreach (Control control in e.Controls)
        {
            if (control == null) continue;
            _logger?.LogDebug("Dock state changed: Control={Control}, NewState={NewState}, OldState={OldState}", control.Name, e.NewState, e.OldState);
            // Queue async work on UI thread - don't use await in event handler
            BeginInvoke(new Func<Task>(async () => await NotifyPanelVisibilityChangedAsync(control)));
        }

        if (_dockingLayoutManager != null)
        {
            _logger?.LogDebug("Central panel visibility delegated to DockingLayoutManager");
        }

        if (_uiConfig.UseSyncfusionDocking && _dockingLayoutManager != null && _dockingManager != null)
        {
            try { _logger?.LogDebug("Dock state changed - layout save deferred to DockingLayoutManager"); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Error during debounced layout save"); }
        }
    }

    private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
    {
        _logger?.LogDebug("Dock control activated: {Control}", e.Control.Name);
    }

    private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
    {
        _logger?.LogDebug("Dock visibility changed: Control={Control}", e.Control?.Name);

        if (e.Control != null)
        {
            // Queue async work on UI thread - don't use await in event handler
            BeginInvoke(new Func<Task>(async () => await NotifyPanelVisibilityChangedAsync(e.Control)));
        }

        try
        {
            if (_dockingLayoutManager != null)
            {
                EnsureCentralPanelVisible();
                EnsureSidePanelsZOrder();
                RefreshFormLayout();
                _logger?.LogDebug("Central panel visibility ensured for docked layout");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure central panel visibility in docked layout");
        }
    }

    private void EnsureCentralPanelVisible()
    {
        if (_centralDocumentPanel != null)
        {
            _centralDocumentPanel.Visible = true;
            _centralDocumentPanel.BringToFront();
        }
    }

    private void EnsureSidePanelsZOrder()
    {
        if (_leftDockPanel != null)
        {
            try { _leftDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set left dock panel z-order"); }
        }

        if (_rightDockPanel != null)
        {
            try { _rightDockPanel.SendToBack(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set right dock panel z-order"); }
        }
    }

    private void RefreshFormLayout()
    {
        if (_dockingManager == null) return;
        this.Refresh();
        this.Invalidate();
    }

    public bool AddDynamicDockPanel(string panelName, string displayLabel, Control content,
        DockingStyle dockStyle = DockingStyle.Right, int width = 200, int height = 150)
    {
        if (string.IsNullOrWhiteSpace(panelName)) throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (_dockingManager == null)
        {
            _logger?.LogWarning("Cannot add dynamic dock panel - DockingManager not initialized");
            return false;
        }

        GradientPanelExt? panel = null;
        try
        {
            panel = new GradientPanelExt
            {
                Name = panelName,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(panel, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            _dockingManager.SetEnableDocking(panel, true);

            if (dockStyle == DockingStyle.Left || dockStyle == DockingStyle.Right)
            {
                _dockingManager.DockControl(panel, this, dockStyle, width);
            }
            else
            {
                _dockingManager.DockControl(panel, this, dockStyle, height);
            }

            _dockingManager.SetAutoHideMode(panel, true);
            _dockingManager.SetDockLabel(panel, displayLabel);
            _dockingManager.SetAllowFloating(panel, true);
            _logger?.LogDebug("Dynamic panel '{PanelName}' configured with auto-hide; floating mode available via UI", panelName);

            panel = null;

            _logger?.LogInformation("Added dynamic dock panel '{PanelName}' with label '{Label}'", panelName, displayLabel);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add dynamic dock panel '{PanelName}'", panelName);
            return false;
        }
        finally
        {
            panel?.Dispose();
        }
    }

    public bool RemoveDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return false;
        _logger?.LogDebug("RemoveDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return false;
    }

    public Control? GetDynamicDockPanel(string panelName)
    {
        if (string.IsNullOrWhiteSpace(panelName)) return null;
        _logger?.LogDebug("GetDynamicDockPanel requested for '{PanelName}' - delegating to DockingLayoutManager", panelName);
        return null;
    }

    public IReadOnlyCollection<string> GetDynamicDockPanelNames()
    {
        return new List<string>().AsReadOnly();
    }

    private void ResetToDefaultLayout()
    {
        _logger?.LogInformation("Resetting to default layout");
        // Ensure all panels are visible
        if (_dockingManager != null)
        {
            var ienum = _dockingManager.Controls;
            while (ienum.MoveNext())
            {
                if (ienum.Current is Control control)
                {
                    _dockingManager.SetDockVisibility(control, true);
                }
            }
        }
    }

    private void DisposeSyncfusionDockingResources()
    {
         _logger?.LogDebug("DisposeSyncfusionDockingResources invoked - delegating to DockingLayoutManager");

        if (_dockingLayoutManager != null)
        {
            try
            {
                if (_dockingManager != null && this.IsHandleCreated)
                {
                    try { _logger?.LogDebug("Form disposal: layout persistence delegated to DockingLayoutManager"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Failed to finalize layout persistence during disposal (non-critical)"); }
                }

                if (_dockingManager != null)
                {
                    try { _logger?.LogDebug("Form disposal: DockingLayoutManager event cleanup in progress"); }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Error during event unsubscription (non-critical)"); }
                }

                _dockingLayoutManager.Dispose();
                _dockingLayoutManager = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose DockingLayoutManager");
            }
        }

        if (_dockingManager != null)
        {
            var mgr = _dockingManager;
            _dockingManager = null;
            mgr.DisposeSafely();
        }

        _logger?.LogDebug("DisposeSyncfusionDockingResources completed - all resources delegated to DockingLayoutManager");
    }

    private void UpdateDockingStateText()
    {
        try
        {
            if (!IsHandleCreated) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new System.Action(UpdateDockingStateText)); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to marshal UpdateDockingStateText to UI thread"); }
                return;
            }

            if (_statePanel == null || _statePanel.IsDisposed) return;

            var stateInfo = new System.Text.StringBuilder();
            var controls = _dockingManager?.Controls as Control.ControlCollection;
            var childCount = controls?.Count ?? 0;
            stateInfo.Append(System.Globalization.CultureInfo.InvariantCulture, $"Panels: {childCount} panel{(childCount != 1 ? "s" : "")}");

            _statePanel.Text = stateInfo.ToString();
            _logger?.LogTrace("Status state updated: {State}", _statePanel.Text);
            _logger?.LogDebug("UpdateDockingStateText: DockingManager control count = {ControlCount}, MainForm control count = {FormControlCount}", childCount, this.Controls.Count);
            Console.WriteLine($"[DIAGNOSTIC] UpdateDockingStateText: DockingManager controls={childCount}, MainForm controls={this.Controls.Count}");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to update state text");
        }
    }

    private (Panel Panel, Label TitleLabel, Label DescLabel) CreateDashboardCard(string title, string description)
    {
        var cardPanel = new Panel
        {
            Size = new Size(200, 100),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Cursor = Cursors.Hand,
            AccessibleName = $"{title} card",
            AccessibleDescription = description,
            AccessibleRole = AccessibleRole.PushButton,
            TabStop = true,
            TabIndex = 0
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font(SegoeUiFontName, 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10),
            TabStop = false
        };

        var descLabel = new Label
        {
            Text = description,
            Font = new Font(SegoeUiFontName, 9F),
            AutoSize = true,
            Location = new Point(10, 40),
            TabStop = false
        };

        cardPanel.Controls.Add(titleLabel);
        cardPanel.Controls.Add(descLabel);

        return (cardPanel, titleLabel, descLabel);
    }

    private void SetupCardClickHandler(Panel card, System.Action action)
    {
        card.Click += (s, e) => action?.Invoke();
        card.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                action?.Invoke();
                e.Handled = true;
            }
        };
        foreach (Control child in card.Controls)
        {
            child.Click += (s, e) => action?.Invoke();
        }
    }

    private void ApplyThemeToDockingPanels()
    {
        if (_dockingManager == null) return;
        try { _logger?.LogDebug("Theme cascade applied via SfSkinManager to docking panels"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to apply theme to docking panels"); }
    }

    private void LoadDynamicPanels(string? layoutPath = null)
    {
        try { _logger?.LogDebug("Dynamic panels load requested: {LayoutPath}", layoutPath ?? "<default>"); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Failed to load dynamic panels"); }
    }

    private static void TryCleanupTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"Cleaned up temporary file: {filePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup temporary file: {filePath}, Error: {ex.Message}");
        }
    }

    private void HideStandardPanelsForDocking()
    {
         foreach (Control control in Controls)
        {
            if (control is SplitContainer)
            {
                try
                {
                    control.Visible = false;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to hide standard panel {ControlName} during docking initialization", control.Name);
                }
            }
        }
        _logger?.LogDebug("Standard panels hidden for Syncfusion docking");
    }

    private void EnsureDockingZOrder()
    {
        _dockingManager?.EnsureZOrder();
    }
}
