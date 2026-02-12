using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Serilog;
using Serilog.Events;
using WileyWidget.Services.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using Panels = WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
// // using WileyWidget.WinForms.Utils; // Consolidated // Consolidated into Helpers
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Initialization logic for MainForm: deferred async initialization, panel setup, ViewModel resolution.
/// Separated into partial to keep core MainForm focused on lifecycle orchestration.
/// </summary>
public partial class MainForm
{
    /// <summary>
    /// Implements IAsyncInitializable.InitializeAsync.
    /// Called after MainForm is shown to perform heavy/async initialization work.
    /// Optimized for docking layout restoration and ViewModel initialization.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // [QUICK WIN] Exit early if form is already disposed to avoid ObjectDisposedException
        if (this.IsDisposed) return;

        var timelineService = _serviceProvider != null
            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider)
            : null;

        _asyncLogger?.Information("MainForm.InitializeAsync started - thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

        // [PERF] Theme and panel initialization from OnShown - deferred after docking is ready
        try
        {
            // Check for cancellation early
            cancellationToken.ThrowIfCancellationRequested();

            await InitializeDockingAsync(cancellationToken).ConfigureAwait(true);

            // Chrome initialization is now done in OnLoad, not here

            // [PERF] Allow structures time to develop before applying theme
            try
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Theme application delay cancelled - form may be disposing");
                return;
            }

            // Apply theme to UI controls after chrome is initialized
            if (_themeService != null)
            {
                try
                {
                    _themeService.ApplyTheme(_themeService.CurrentTheme);

                    // Explicitly set ThemeName on key Syncfusion controls for robustness
                    try
                    {
                        var currentTheme = _themeService.CurrentTheme;
                        if (_ribbon != null && !_ribbon.IsDisposed && _ribbon.IsHandleCreated)
                        {
                            _ribbon.ThemeName = currentTheme;
                        }
                        if (_navigationStrip != null && !_navigationStrip.IsDisposed && _navigationStrip.IsHandleCreated)
                        {
                            _navigationStrip.ThemeName = currentTheme;
                        }
                    }
                    catch (ArgumentException argEx)
                    {
                        _logger?.LogWarning(argEx, "Failed to set ThemeName on Syncfusion controls - controls may not be ready");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to set explicit ThemeName on Syncfusion controls");
                    }
                }
                catch (ArgumentOutOfRangeException aorEx)
                {
                    _logger?.LogWarning(aorEx, "ArgumentOutOfRangeException during theme application - controls may not be fully initialized");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to apply theme");
                }
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] _themeService is null in InitializeAsync");
            }

            // [PERF] Defer heavy chrome optimization (image validation, refresh) to background after form is shown
            // This avoids UI thread blocking during critical startup path (~500ms saved in logs)
            try
            {
                await DeferChromeOptimizationAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Deferred chrome optimization cancelled during shutdown");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Deferred chrome optimization failed - UI formatting may be suboptimal");
            }

            // CRITICAL: DockingManager is initialized in OnShown Phase 1.
            // Verify it exists before proceeding with panel operations.
            if (_dockingManager == null)
            {
                _logger?.LogWarning("[CRITICAL] DockingManager is null in InitializeAsync - docking was not initialized successfully in OnShown");
                _asyncLogger?.Warning("[CRITICAL] DockingManager is null in InitializeAsync");
                return;
            }

            if (_uiConfig.UseSyncfusionDocking)
            {
                _logger?.LogInformation("InitializeAsync: Loading docking layout");
                await LoadAndApplyDockingLayout(GetDockingLayoutPath(), cancellationToken).ConfigureAwait(true);
            }

            // Defensive: Ensure panel navigator is initialized before trying to show panels
            if (_uiConfig.UseSyncfusionDocking && _panelNavigator == null)
            {
                _logger?.LogInformation("[DEFENSIVE] PanelNavigator is null despite docking being enabled - attempting explicit initialization");
                try
                {
                    EnsurePanelNavigatorInitialized();
                    _logger?.LogInformation("[DEFENSIVE] PanelNavigator explicitly initialized");
                }
                catch (Exception defEx)
                {
                    _logger?.LogError(defEx, "[DEFENSIVE] Failed to initialize PanelNavigator");
                }
            }

            // Phase 1: Show priority panels for faster startup
            _logger?.LogInformation("[DIAGNOSTIC] UseSyncfusionDocking={Value}", _uiConfig.UseSyncfusionDocking);
            _logger?.LogInformation("[DIAGNOSTIC] _panelNavigator is null? {IsNull}", _panelNavigator == null);
            _logger?.LogInformation("[DIAGNOSTIC] _dockingManager is null? {IsNull}", _dockingManager == null);

            if (_uiConfig.UseSyncfusionDocking && _panelNavigator != null && _uiConfig.AutoShowDashboard)
            {
                _logger?.LogInformation("Showing priority panels for faster startup");
                try
                {
                    // Priority panels: Dashboard only to reduce clutter
                    _logger?.LogInformation("[PANEL] Showing Dashboard");
                    // Ensure UI handle is available; small delay helps controls create handles on slower machines
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogDebug("Dashboard show cancelled - form may be disposing");
                        return;
                    }

                    // Verify docking manager still exists and form is not disposing
                    if (_dockingManager == null || this.IsDisposed || !this.IsHandleCreated)
                    {
                        _logger?.LogWarning("[PANEL] Cannot show Dashboard - form or docking manager not ready");
                        return;
                    }

                    if (!_dashboardAutoShown && _uiConfig != null && _uiConfig.AutoShowDashboard)
                    {
                        _logger?.LogInformation("[PANEL] About to invoke ShowForm<BudgetDashboardForm>");
                        _panelNavigator.ShowForm<BudgetDashboardForm>("Dashboard", DockingStyle.Right, allowFloating: false);
                        _dashboardAutoShown = true;
                        _logger?.LogInformation("[PANEL] ShowPanel returned successfully");
                    }
                    else
                    {
                        _logger?.LogInformation("[PANEL] Skipping priority dashboard: AutoShown={Shown}, ConfigEnabled={Enabled}", _dashboardAutoShown, _uiConfig?.AutoShowDashboard);
                    }
                    _logger?.LogInformation("Priority panels shown successfully");
                }
                catch (ArgumentException argEx)
                {
                    _logger?.LogWarning(argEx, "[PANEL] ArgumentException while showing Dashboard - docking system may not be ready");
                }
                catch (NullReferenceException nrex)
                {
                    _logger?.LogError(nrex, "[CRITICAL NRE] NullReferenceException while showing panels. Stack: {Stack}", nrex.StackTrace);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to show priority panels: {Type}: {Message}", ex.GetType().Name, ex.Message);
                }
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] Skipping dashboard: UseSyncfusionDocking={Docking}, AutoShowDashboard={AutoShow}, _panelNavigator={Nav}", _uiConfig.UseSyncfusionDocking, _uiConfig.AutoShowDashboard, _panelNavigator != null ? "set" : "null");

                // Fallback: Show default panel when AutoShowDashboard is false to prevent blank initial view
                if (_uiConfig.UseSyncfusionDocking && _panelNavigator != null && !_uiConfig.AutoShowDashboard && !_dashboardAutoShown)
                {
                    _logger?.LogInformation("[FALLBACK] AutoShowDashboard is false, showing default RevenueTrends panel to prevent blank initial view");
                    try
                    {
                        _panelNavigator.ShowPanel<RevenueTrendsPanel>("Revenue Trends", DockingStyle.Right, allowFloating: false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to show fallback RevenueTrends panel");
                    }
                }
            }

            // Phase 2: Notify ViewModels of initial visibility for lazy loading
            if (_dockingManager != null)
            {
                _logger?.LogInformation("Triggering initial visibility notifications for all docked panels");
                foreach (Control control in this.Controls)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_dockingManager.GetEnableDocking(control))
                    {
                        await NotifyPanelVisibilityChangedAsync(control);
                    }
                }
            }

            // ============================================================================
            // NAVIGATION HARDENING: Enable ribbon and toolbar navigation buttons once docking system is confirmed ready
            // This prevents clicks on navigation buttons before the docking system has initialized
            // ============================================================================
            try
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    this.InvokeIfRequired(() =>
                    {
                        if (this.IsDisposed) return;

                        // Enable items on the small navigation strip (legacy toolbar)
                        int enabledCount = 0;
                        var navNames = new List<string>();
                        if (_navigationStrip != null)
                        {
                            foreach (ToolStripItem item in _navigationStrip.Items)
                            {
                                try
                                {
                                    if (item is ToolStripButton button && !button.Enabled)
                                    {
                                        button.Enabled = true;
                                        enabledCount++;
                                    }
                                    if (!string.IsNullOrWhiteSpace(item?.Name)) navNames.Add(item.Name);
                                }
                                catch { }
                            }
                        }

                        // Enable navigation items in the Ribbon (and any ToolStripEx panels).
                        int ribbonEnabled = 0;
                        try
                        {
                            if (_ribbon != null && !_ribbon.IsDisposed && _ribbon.IsHandleCreated)
                            {
                                // First: attempt to enable by name using helper FindToolStripItem (covers nested/remote locations)
                                foreach (var name in navNames)
                                {
                                    try
                                    {
                                        ToolStripItem? found = null;
                                        try { found = FindToolStripItem(_ribbon, name); } catch { }
                                        if (found == null)
                                        {
                                            try { found = FindToolStripItem(this, name); } catch { }
                                        }

                                        if (found != null && !found.Enabled)
                                        {
                                            found.Enabled = true;
                                            ribbonEnabled++;
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        _logger?.LogDebug(innerEx, "[NAVIGATION] Error enabling ribbon item by name {Name}", name);
                                    }
                                }

                                // Second: conservative scan - enable items with Name starting with "Nav_" and known whitelist
                                var whitelist = new[] { "ThemeToggle", "GlobalSearch" };

                                foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
                                {
                                    if (tab?.Panel == null) continue;

                                    foreach (Control ctrl in tab.Panel.Controls)
                                    {
                                        if (ctrl is ToolStripEx panelEx)
                                        {
                                            foreach (ToolStripItem item in panelEx.Items)
                                            {
                                                try
                                                {
                                                    if (item == null) continue;
                                                    var name = item.Name ?? string.Empty;
                                                    if (string.IsNullOrWhiteSpace(name)) continue;

                                                    bool shouldEnable = false;
                                                    if (name.StartsWith("Nav_", StringComparison.OrdinalIgnoreCase)) shouldEnable = true;
                                                    else
                                                    {
                                                        foreach (var w in whitelist)
                                                        {
                                                            if (string.Equals(name, w, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                shouldEnable = true;
                                                                break;
                                                            }
                                                        }
                                                    }

                                                    if (shouldEnable && !item.Enabled)
                                                    {
                                                        item.Enabled = true;
                                                        ribbonEnabled++;
                                                    }
                                                }
                                                catch (ObjectDisposedException) { /* control disposed while iterating - ignore */ }
                                                catch (InvalidOperationException) { /* control mutated - ignore */ }
                                                catch (Exception innerEx)
                                                {
                                                    _logger?.LogDebug(innerEx, "[NAVIGATION] Error enabling ribbon item {Name}", item?.Name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "[NAVIGATION] Failed scanning/enabling ribbon items (non-fatal)");
                        }

                        if (enabledCount > 0) _logger?.LogDebug("[NAVIGATION] Enabled {Count} navigation buttons", enabledCount);
                        _logger?.LogDebug("[NAVIGATION] Enabled {Count} ribbon navigation buttons", ribbonEnabled);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAVIGATION] Unexpected error enabling navigation buttons (non-critical)");
            }

            _asyncLogger?.Information("MainForm.InitializeAsync completed successfully");
        }
        catch (OperationCanceledException)
        {
            // ✅ FIX: Add context to cancellation logs
            var reason = this.IsDisposed ? "Form disposing" :
                         _initializationCts?.IsCancellationRequested == true ? "Startup timeout" :
                         "User cancelled";
            _logger?.LogInformation("InitializeAsync canceled: {Reason}", reason);
            _asyncLogger?.Information("InitializeAsync canceled: {Reason}", reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during InitializeAsync deferred initialization");
            _asyncLogger?.Error(ex, "InitializeAsync failed");
        }
    }

    private async Task InitializeDockingAsync(CancellationToken cancellationToken)
    {
        if (_syncfusionDockingInitialized || _uiConfig?.UseSyncfusionDocking != true)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (this.InvokeRequired)
        {
            await this.InvokeAsync(() =>
            {
                if (this.IsDisposed || _syncfusionDockingInitialized) return;

                _logger?.LogInformation("InitializeDockingAsync: Initializing docking before chrome");
                InitializeSyncfusionDocking();
                ConfigureDockingManagerChromeLayout();
                _syncfusionDockingInitialized = true;
            }).ConfigureAwait(true);
        }
        else
        {
            if (_syncfusionDockingInitialized) return;

            _logger?.LogInformation("InitializeDockingAsync: Initializing docking before chrome");
            InitializeSyncfusionDocking();
            ConfigureDockingManagerChromeLayout();
            _syncfusionDockingInitialized = true;
        }
    }

    /// <summary>
    /// Run deferred initialization tasks after OnShown (health check, ViewModel, dashboard auto-show).
    /// Called from OnShown to perform non-blocking background initialization.
    /// </summary>
    private async Task RunDeferredInitializationAsync(CancellationToken cancellationToken)
    {
        if (this.IsDisposed) return;

        // [PERF] Allow UI structures additional time to fully develop before starting background tasks
        await Task.Delay(50, cancellationToken).ConfigureAwait(true);

        // [PERF] WebView2 prewarm (non-blocking): create CoreWebView2Environment after the form is shown.
        StartWebView2Prewarm(cancellationToken);

        // [PERF] Background startup health check
        _ = Task.Run(async () =>
        {
            try
            {
                // Additional small delay for health check to allow full structure completion
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (_serviceProvider == null) return;

                using var scope = _serviceProvider.CreateScope();
                cancellationToken.ThrowIfCancellationRequested();
                await Program.RunStartupHealthCheckAsync(scope.ServiceProvider).ConfigureAwait(false);
                _logger?.LogInformation("Deferred startup health check completed");
            }
            catch (OperationCanceledException)
            {
                // ✅ FIX: Add context - timeout vs disposal
                var reason = this.IsDisposed ? "form disposing" : "startup timeout (30s)";
                _logger?.LogInformation("Deferred startup health check canceled: {Reason}", reason);
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("Deferred startup health check skipped due to disposal");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Deferred startup health check failed (non-fatal)");
            }
        }, cancellationToken);

        // [PERF] Deferred test data seeding
        _ = Task.Run(async () =>
        {
            try
            {
                // Additional small delay for data seeding
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (_serviceProvider == null) return;
                await WileyWidget.WinForms.Services.UiTestDataSeeder.SeedIfEnabledAsync(_serviceProvider).ConfigureAwait(false);
                _logger?.LogDebug("Deferred test data seeding completed successfully");
            }
            catch (OperationCanceledException)
            {
                // ✅ FIX: Add context - timeout vs disposal
                var reason = this.IsDisposed ? "form disposing" : "startup timeout (30s)";
                _logger?.LogInformation("Deferred test data seeding canceled: {Reason}", reason);
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("Deferred test data seeding skipped due to disposal");
            }
            catch (Exception seedEx)
            {
                _logger?.LogWarning(seedEx, "Deferred test data seeding failed (non-critical)");
            }
        }, cancellationToken);

        try
        {
            // [PERF] Phase 0: Pre-initialization status
            try
            {
                _logger?.LogInformation("OnShown: Starting deferred background initialization");
                _asyncLogger?.Information("→ About to call ApplyStatus...");
                ApplyStatus("Initializing...");
                _asyncLogger?.Information("→ ApplyStatus completed");
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception prephaseEx)
            {
                _asyncLogger?.Error(prephaseEx, "★ CRITICAL: Exception before Phase 1 code!");
                _logger?.LogError(prephaseEx, "★ CRITICAL: Exception before Phase 1 code!");
                throw;
            }

            _logger?.LogInformation("→ Phase 1: Docking already initialized at start of OnShown");
            _asyncLogger?.Information("→ Phase 1: Docking verification complete");

            // [PERF] Phase 2: MainViewModel initialization and dashboard data load
            _asyncLogger?.Information("MainForm OnShown: Phase 3 - Initializing MainViewModel and dashboard data");
            _logger?.LogInformation("Initializing MainViewModel");
            ApplyStatus("Loading dashboard data...");
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (_serviceProvider == null)
                {
                    _logger?.LogError("ServiceProvider is null during MainViewModel initialization");
                    ApplyStatus("Initialization error: ServiceProvider unavailable");
                    return;
                }

                _mainViewModelScope = _serviceProvider.CreateScope();
                var scopedServices = _mainViewModelScope.ServiceProvider;
                MainViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(scopedServices);
                _asyncLogger?.Information("MainForm OnShown: MainViewModel resolved from DI container");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to resolve MainViewModel from DI container");
                _asyncLogger?.Error(ex, "MainForm OnShown: Failed to resolve MainViewModel from DI container");
            }

            // [PERF] MainViewModel async initialization
            if (MainViewModel != null)
            {
                try
                {
                    _asyncLogger?.Information("MainForm OnShown: Calling MainViewModel.InitializeAsync");
                    await MainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(true);
                    if (this.InvokeRequired)
                    {
                        await this.InvokeAsync(() =>
                        {
                            if (this.IsDisposed) return;
                            _logger?.LogInformation("MainViewModel initialized successfully");
                            _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                        });
                    }
                    else
                    {
                        _logger?.LogInformation("MainViewModel initialized successfully");
                        _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                    }
                }
                catch (OperationCanceledException)
                {
                    // ✅ FIX: Add context - why was it cancelled?
                    var reason = this.IsDisposed ? "form disposing" :
                                 _initializationCts?.IsCancellationRequested == true ? "startup timeout" :
                                 "user cancelled";
                    _logger?.LogInformation("Dashboard initialization cancelled: {Reason}", reason);
                    _asyncLogger?.Information("MainForm OnShown: Dashboard initialization cancelled: {Reason}", reason);

                    // Don't update UI if disposing
                    if (!this.IsDisposed)
                    {
                        // Use safe invoke wrapper
                        try
                        {
                            if (this.InvokeRequired)
                                this.BeginInvoke(new global::System.Action(() => ApplyStatus("Initialization cancelled")));
                            else
                                ApplyStatus("Initialization cancelled");
                        }
                        catch { /* Suppress during shutdown */ }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize MainViewModel in OnShown");
                    _asyncLogger?.Error(ex, "MainForm OnShown: Failed to initialize MainViewModel in OnShown");
                    ApplyStatus("Error loading dashboard data");
                    if (this.IsHandleCreated)
                    {
                        try
                        {
                            UIHelper.ShowMessageOnUI(this,
                                $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                "Initialization Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning,
                                _logger);
                        }
                        catch { }
                    }
                    return;
                }
            }
            else
            {
                _logger?.LogWarning("MainViewModel not available in service provider");
            }

            // [PERF] Auto-show initial dashboard panel
            if (!_dashboardAutoShown && _panelNavigator != null && _uiConfig != null && _uiConfig.AutoShowDashboard)
            {
                try
                {
                    _logger?.LogInformation("Showing initial dashboard panel...");
                    ShowForm<BudgetDashboardForm>("Dashboard", null, DockingStyle.Top);
                    _dashboardAutoShown = true;
                    _logger?.LogInformation("Initial dashboard panel shown successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to show initial dashboard panel");
                    ShowErrorDialog("Startup Error", "Could not load dashboard. Check logs.");
                }
            }
            else
            {
                _logger?.LogInformation("[PANEL] Skipping auto-show dashboard in RunDeferredInitializationAsync: AutoShown={Shown}, ConfigEnabled={Enabled}", _dashboardAutoShown, _uiConfig?.AutoShowDashboard);
            }

            ApplyStatus("Ready");
            _logger?.LogInformation("OnShown: Deferred initialization completed");

            // [PERF] Late image validation for ribbon/menu (ensures all images loaded)
            try
            {
                _logger?.LogDebug("OnShown: Running late image validation pass");

                // [FIX] Use UIThreadHelper for safe marshalling. This replaces the unreliable InvokeRequired check
                // and ensures we marshal specifically to the UI thread before touching controls.
                await this.InvokeAsyncNonBlocking(() =>
                {
                    if (_ribbon != null && !_ribbon.IsDisposed)
                    {
                        _ribbon.ValidateAndConvertImages(_logger);
                    }
                    if (_menuStrip != null && !_menuStrip.IsDisposed)
                    {
                        _menuStrip.ValidateAndConvertImages(_logger);
                    }
                }, _logger);

                _logger?.LogDebug("OnShown: Late image validation completed");
            }
            catch (Exception validationEx)
            {
                _logger?.LogError(validationEx, "OnShown: Late image validation failed");
            }
        }
        catch (OperationCanceledException)
        {
            // ✅ FIX: Add context - why cancelled?
            var reason = this.IsDisposed ? "form disposing" : "user/timeout cancelled";
            _logger?.LogInformation("OnShown initialization cancelled: {Reason}", reason);
            ApplyStatus("Initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during OnShown deferred initialization");
            ApplyStatus("Initialization error");

            if (this.IsHandleCreated)
            {
                try
                {
                    UIHelper.ShowErrorOnUI(this,
                        $"An unexpected error occurred during initialization: {ex.Message}\n\nPlease check the logs for details.",
                        "Critical Error",
                        _logger);
                }
                catch { }
            }
        }
    }

    private void StartWebView2Prewarm(CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return;
        }

        if (_uiConfig != null && _uiConfig.IsUiTestHarness)
        {
            _logger?.LogDebug("[WEBVIEW2] Prewarm skipped in UI test harness mode");
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new global::System.Action(() => StartWebView2Prewarm(cancellationToken)));
            return;
        }

        _ = PrewarmWebView2Async(cancellationToken);
    }

    private async Task PrewarmWebView2Async(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            if (IsDisposed)
            {
                return;
            }

            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                _logger?.LogInformation("[WEBVIEW2] Prewarm starting (Runtime={Version})", version);

                // Use the same stable UDF path as JARVIS to avoid folder lock-up or profile conflicts
                var customUdfPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WileyWidget", "WebView2");
                if (!Directory.Exists(customUdfPath))
                {
                    Directory.CreateDirectory(customUdfPath);
                }

                _ = await CoreWebView2Environment.CreateAsync(null, customUdfPath, null).ConfigureAwait(true);
                _logger?.LogInformation("[WEBVIEW2] Prewarm completed with UDF: {UDF}", customUdfPath);
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                _logger?.LogWarning(ex, "[WEBVIEW2] Runtime not found - skipping prewarm");
                return;
            }
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80010106)
        {
            _logger?.LogDebug(ex, "[WEBVIEW2] Prewarm skipped due to COM apartment state");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("[WEBVIEW2] Prewarm canceled");
        }
        catch (ObjectDisposedException)
        {
            _logger?.LogDebug("[WEBVIEW2] Prewarm skipped due to disposal");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[WEBVIEW2] Prewarm failed (non-fatal)");
        }
    }

    /// <summary>
    /// Validates all critical initialization dependencies before proceeding.
    /// Throws InvalidOperationException if any critical dependency is missing.
    /// </summary>
    private void ValidateInitializationState()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceProvider not initialized - dependency injection setup failed");
        }

        if (!IsHandleCreated)
        {
            throw new InvalidOperationException("Form handle not created - cannot initialize DockingManager");
        }

        var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetService<WileyWidget.WinForms.Services.IThemeService>(_serviceProvider);
        if (themeService == null)
        {
            throw new InvalidOperationException("IThemeService not resolved from ServiceProvider");
        }

        var themeName = themeService.CurrentTheme;
        if (string.IsNullOrEmpty(themeName))
        {
            throw new InvalidOperationException("Theme name not configured in IThemeService");
        }

        _logger?.LogInformation("ValidateInitializationState: All dependencies validated - theme={Theme}", themeName);
    }

    /// <summary>
    /// Initializes async diagnostics Serilog logger for detailed MainForm events.
    /// Called from OnShown to capture async initialization phases.
    /// </summary>
    private void InitializeAsyncDiagnosticsLogger()
    {
        try
        {
            var logsDirectory = LogPathResolver.GetLogsDirectory();
            var asyncLogPath = Path.Combine(logsDirectory, "mainform-diagnostics-.log");
            _asyncLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(a => a.File(asyncLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    buffered: false,
                    shared: true,
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}"),
                    bufferSize: 10000,    // Configure queue size to prevent blocking
                    blockWhenFull: false) // Do not block producer if queue is full; drop if necessary
                .Enrich.FromLogContext()
                .CreateLogger();

            _asyncLogger.Information("✓ Async diagnostics logger initialized - path: {LogPath}", asyncLogPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize async logging for MainForm - falling back to main logger");
        }
    }

    /// <summary>
    /// Loads MRU list from persistent storage.
    /// Called once from OnLoad (not duplicated in InitializeAsync).
    /// </summary>
    private void LoadMruList()
    {
        try
        {
            _mruList.Clear();
            var loadedMru = _windowStateService.LoadMru();

            // Validate each path before adding to MRU list
            foreach (var file in loadedMru)
            {
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                // Only add files that still exist
                if (System.IO.File.Exists(file))
                {
                    _mruList.Add(file);
                }
                else
                {
                    _logger?.LogDebug("MRU file no longer exists, skipping: {File}", file);
                }
            }

            _logger?.LogDebug("MRU list loaded: {Count} items ({TotalLoaded} loaded, {Filtered} filtered)", _mruList.Count, loadedMru.Count(), loadedMru.Count() - _mruList.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load MRU list");
        }
    }

    /// <summary>
    /// Handles CLI-driven report viewer launch (optional feature).
    /// Called from OnLoad if ReportViewerLaunchOptions.ShowReportViewer is true.
    /// </summary>
    private void TryLaunchReportViewerOnLoad()
    {
        if (_reportViewerLaunchOptions == null || !_reportViewerLaunchOptions.ShowReportViewer)
        {
            return;
        }

        if (_reportViewerLaunched)
        {
            _logger?.LogDebug("Report viewer launch already handled for {ReportPath}", _reportViewerLaunchOptions.ReportPath);
            return;
        }

        var reportPath = _reportViewerLaunchOptions.ReportPath;
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            _logger?.LogWarning("Report viewer launch requested but no report path was supplied");
            return;
        }

        if (!File.Exists(reportPath))
        {
            _logger?.LogWarning("Report viewer launch requested but report file was missing: {ReportPath}", reportPath);
            return;
        }

        try
        {
            ShowAnalyticsHubPanel(reportPath);
            _reportViewerLaunched = true;
            _logger?.LogInformation("Analytics Hub opened for CLI path: {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open Analytics Hub for {ReportPath}", reportPath);
        }
    }

    /// <summary>
    /// Shows the Analytics Hub panel with optional auto-load path for CLI-launched reports.
    /// </summary>
    private void ShowAnalyticsHubPanel(string reportPath)
    {
        try
        {
            _panelNavigator.ShowPanel<WileyWidget.WinForms.Controls.Analytics.AnalyticsHubPanel>("Analytics Hub", reportPath, DockingStyle.Right, allowFloating: true);
            _logger?.LogInformation("Analytics Hub panel shown with auto-load path: {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Analytics Hub panel");
        }
    }

    /// <summary>
    /// Processes dropped files from drag-drop operation.
    /// Supports CSV, XLSX, XLS, JSON, XML file formats.
    /// </summary>
    private async Task ProcessDroppedFiles(string[] files, CancellationToken cancellationToken = default)
    {
        if (files == null || files.Length == 0)
        {
            _logger?.LogWarning("No files provided to ProcessDroppedFiles");
            return;
        }

        _asyncLogger?.Information("Processing {Count} dropped files", files.Length);

        foreach (var file in files)
        {
            try
            {
                // Validate file
                if (string.IsNullOrWhiteSpace(file))
                {
                    _logger?.LogWarning("Empty file path in dropped files");
                    continue;
                }

                if (!File.Exists(file))
                {
                    ShowErrorDialog("File Not Found", $"The file '{Path.GetFileName(file)}' does not exist.");
                    continue;
                }

                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > 100 * 1024 * 1024) // 100MB limit
                {
                    ShowErrorDialog("File Too Large", $"The file '{Path.GetFileName(file)}' is too large ({fileInfo.Length / 1024 / 1024}MB). Maximum size is 100MB.");
                    continue;
                }

                string ext;
                try
                {
                    ext = Path.GetExtension(file)?.ToLowerInvariant() ?? string.Empty;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    _logger?.LogWarning(ex, "Skipping dropped file due to malformed path: {File}", file);
                    try { ShowErrorDialog("Invalid File Path", $"The file path '{file}' is invalid and will be skipped."); } catch { }
                    continue;
                }

                _asyncLogger?.Debug("Processing dropped file: {File} (ext: {Ext})", file, ext);
                _logger?.LogInformation("Processing dropped file: {File} (ext: {Ext})", file, ext);

                // Add to MRU with optimized in-memory update
                try
                {
                    _windowStateService.AddToMru(file);
                    // Update in-memory list directly instead of reloading
                    if (!_mruList.Contains(file))
                    {
                        _mruList.Insert(0, file); // Add to front for MRU behavior
                        if (_mruList.Count > 10) _mruList.RemoveAt(_mruList.Count - 1); // Limit to 10 items
                    }
                }
                catch (DirectoryNotFoundException dnfEx)
                {
                    _logger?.LogWarning(dnfEx, "MRU storage directory not found - creating it");
                    // Storage service should handle directory creation, but log for diagnostics
                }
                catch (IOException ioEx)
                {
                    _logger?.LogWarning(ioEx, "Failed to add file to MRU (non-fatal): {File}", file);
                    // Continue with import even if MRU update fails
                }
                catch (Exception mruEx)
                {
                    _logger?.LogWarning(mruEx, "Unexpected error updating MRU (non-fatal): {File}", file);
                    // Continue with import even if MRU update fails
                }

                // Validate _fileImportService before attempting import
                if (_fileImportService == null)
                {
                    _logger?.LogError("FileImportService is null - DI may have failed");
                    ShowErrorDialog("Service Error", "File import service is not available. Please restart the application.");
                    continue;
                }

                if (ext == ".csv" || ext == ".xlsx" || ext == ".xls" || ext == ".json" || ext == ".xml")
                {
                    var importOperation = $"Import:{Path.GetFileName(file)}";
                    _statusProgressService?.Start(importOperation, $"Importing {Path.GetFileName(file)}...", isIndeterminate: false);

                    try
                    {
                        var result = await _fileImportService.ImportDataAsync<Dictionary<string, object>>(file, cancellationToken);
                        HandleImportResult(file, result);
                        _statusProgressService?.Report(importOperation, 100, "Import complete");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("File import canceled for {File}", Path.GetFileName(file));
                        ShowErrorDialog("Import Canceled", $"Import was canceled for '{Path.GetFileName(file)}'");
                        break; // Exit loop on cancellation
                    }
                    catch (IOException ioEx)
                    {
                        _logger?.LogError(ioEx, "IO error importing file: {File}", file);
                        ShowErrorDialog("Import Error", $"Failed to read file:\n{Path.GetFileName(file)}\n\nError: {ioEx.Message}");
                    }
                    finally
                    {
                        _statusProgressService?.Complete(importOperation, "Import finished");
                    }
                }
                else
                {
                    ShowErrorDialog("Unsupported File Type", $"Unsupported file type: {ext}\n\nSupported: CSV, XLSX, XLS, JSON, XML");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process dropped file: {File}", file);
                ShowErrorDialog("File Processing Error", $"Failed to process '{Path.GetFileName(file)}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Handles import result from file import service.
    /// Displays success or error message to user.
    /// <summary>
    /// Defers heavy chrome optimization to background (after form is shown).
    /// Moves image validation and refresh off the critical startup path to avoid UI thread blocking.
    /// This saves ~500ms from startup timeline by deferring Ribbon image validation to async phase.
    /// </summary>
    private async Task DeferChromeOptimizationAsync(CancellationToken cancellationToken)
    {
        if (_ribbon == null || _ribbon.IsDisposed)
        {
            return;
        }

        try
        {
            _logger?.LogInformation("[PERF] Deferred Chrome Optimization starting - deferring Ribbon image validation to background");

            // Avoid contending with other docking/theming operations
            await Task.Delay(150, cancellationToken).ConfigureAwait(true);

            // [FIX] E2E Thread Marshalling Evaluation:
            // Use the enhanced UIThreadHelper.InvokeAsyncSafe pattern.
            // This now handles waiting for the handle automatically and performs proper marshalling.
            // We no longer need the explicit IsHandleCreated check here as the helper encapsulates it.
            if (!_ribbon.IsDisposed)
            {
                await _ribbon.InvokeAsyncNonBlocking(() =>
                {
                    _logger?.LogDebug("[PERF] Validating Ribbon images (converting animated images to static bitmaps)");
                    _ribbon.ValidateAndConvertImages(_logger);

                    _ribbon.PerformLayout();
                    _ribbon.Refresh();
                    _ribbon.BringToFront();
                }, _logger);

                _logger?.LogInformation("[PERF] Deferred Chrome Optimization completed successfully");
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("[PERF] Deferred Chrome Optimization cancelled - form closing");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[PERF] Unexpected error in DeferChromeOptimizationAsync");
        }
    }

    /// Validates result.Data is not null before processing.
    /// </summary>
    private void HandleImportResult<T>(string file, Result<T> result) where T : class
    {
        if (result == null)
        {
            _logger?.LogWarning("HandleImportResult called with null result for {File}", Path.GetFileName(file));
            ShowErrorDialog("Import Error", "Import result was null");
            return;
        }

        if (result.IsSuccess && result.Data != null)
        {
            // Enhanced: Type-safe counting with fallback
            int count = 0;
            try
            {
                if (result.Data is System.Collections.IDictionary dict)
                {
                    count = dict.Count;
                }
                else if (result.Data is System.Collections.ICollection coll)
                {
                    count = coll.Count;
                }
                else
                {
                    count = 1; // Fallback for single objects
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to count imported data for {File}", Path.GetFileName(file));
                count = 0;
            }

            _logger?.LogInformation("Successfully imported {File}: {Count} items", Path.GetFileName(file), count);
            try
            {
                UIHelper.ShowMessageOnUI(this, $"File imported: {Path.GetFileName(file)}\nParsed {count} data items",
                "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information, _logger);
            }
            catch { }
        }
        else if (result.IsSuccess && result.Data == null)
        {
            // Edge case: IsSuccess but Data is null
            _logger?.LogWarning("Import reported success but returned null data for {File}", Path.GetFileName(file));
            ShowErrorDialog("Import Warning", $"File imported but no data was returned:\n{Path.GetFileName(file)}");
        }
        else
        {
            _logger?.LogWarning("Import failed for {File}: {Error}", Path.GetFileName(file), result.ErrorMessage);
            ShowErrorDialog("Import Failed", result.ErrorMessage ?? "Unknown error");
        }
    }
}
