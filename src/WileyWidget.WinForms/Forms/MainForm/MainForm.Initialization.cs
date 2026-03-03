using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WileyWidget.Services.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Initialization logic for MainForm: deferred async initialization, panel setup, ViewModel resolution.
/// Separated into partial to keep core MainForm focused on lifecycle orchestration.
/// </summary>
public partial class MainForm
{
    private int _initializeAsyncInvocationCount;
    private int _initializeAsyncStarted;
    private int _startupUiPhasesQueued;
    private int _startupUiPhasesIndex;
    private CancellationTokenSource? _onShownStartupCts;
    private System.Windows.Forms.Timer? _startupUiPhasesTimer;

    /// <summary>
    /// Implements IAsyncInitializable.InitializeAsync.
    /// Called after MainForm is shown to perform heavy/async initialization work.
    /// Optimized for docking layout restoration and ViewModel initialization.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var invocationId = Interlocked.Increment(ref _initializeAsyncInvocationCount);
        var isFirstInitializationCall = Interlocked.CompareExchange(ref _initializeAsyncStarted, 1, 0) == 0;
        var initStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogInformation(
            "[STARTUP-DIAG] MainForm.InitializeAsync call #{InvocationId} requested (FirstCall={FirstCall}, InvokeRequired={InvokeRequired}, ThreadId={ThreadId}, IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated})",
            invocationId,
            isFirstInitializationCall,
            InvokeRequired,
            Thread.CurrentThread.ManagedThreadId,
            IsDisposed,
            IsHandleCreated);

        if (!isFirstInitializationCall)
        {
            _logger?.LogWarning("[STARTUP-DIAG] MainForm.InitializeAsync call #{InvocationId} skipped - duplicate initialization request", invocationId);
            initStopwatch.Stop();
            return;
        }

        // === CRITICAL: Always marshal back to UI thread for ANY Syncfusion/WinForms work ===
        try
        {
            if (this.InvokeRequired)
            {
                _logger?.LogDebug("[STARTUP-DIAG] MainForm.InitializeAsync call #{InvocationId} marshalling to UI thread", invocationId);
                await this.InvokeAsyncNonBlockingTask(ct => InitializeAsyncCore(ct, invocationId)).ConfigureAwait(true);
                return;
            }

            await InitializeAsyncCore(cancellationToken, invocationId).ConfigureAwait(true);
        }
        finally
        {
            initStopwatch.Stop();
            _logger?.LogInformation("[STARTUP-DIAG] MainForm.InitializeAsync call #{InvocationId} finished in {ElapsedMs}ms", invocationId, initStopwatch.ElapsedMilliseconds);
        }
    }

    private async Task InitializeAsyncCore(CancellationToken ct, int invocationId)
    {
        _asyncLogger?.Information("MainForm.InitializeAsync call #{InvocationId} started - thread: {ThreadId}", invocationId, Thread.CurrentThread.ManagedThreadId);

        this.SuspendLayout();

        try
        {
            ct.ThrowIfCancellationRequested();

            // Chrome initialization is done in OnLoad.

            // Apply theme — SfSkinManager is sole authority, no per-control ThemeName assignments needed here.
            if (_themeService != null)
            {
                _themeService.ApplyTheme(_themeService.CurrentTheme);
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] _themeService is null in InitializeAsync");
            }

            // Notify ViewModels of initial visibility for lazy loading
            var rootControlCount = this.Controls.Count;
            _logger?.LogInformation("Triggering initial visibility notifications for all controls (call #{InvocationId}, rootControls={RootControlCount})", invocationId, rootControlCount);
            foreach (var control in this.Controls.Cast<Control>())
            {
                ct.ThrowIfCancellationRequested();
                _ = NotifyPanelVisibilityChangedAsync(control);
            }

            // === FIXED: Line 67 equivalent - layout on UI thread ===
            this.PerformLayout();                    // now guaranteed on UI thread

            // Enable navigation strip buttons now that the docking system is confirmed ready.
            try
            {
                if (_navigationStrip != null && IsHandleCreated && !IsDisposed)
                {
                    int enabledCount = 0;
                    foreach (ToolStripItem item in _navigationStrip.Items)
                    {
                        if (item is ToolStripButton button && !button.Enabled)
                        {
                            button.Enabled = true;
                            enabledCount++;
                        }
                    }
                    _logger?.LogDebug("[NAVIGATION] Enabled {Count} navigation buttons", enabledCount);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAVIGATION] Unexpected error enabling navigation buttons (non-critical)");
            }

            _asyncLogger?.Information("MainForm.InitializeAsync call #{InvocationId} completed successfully", invocationId);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("InitializeAsync call #{InvocationId} canceled", invocationId);
            _asyncLogger?.Information("InitializeAsync call #{InvocationId} canceled", invocationId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during InitializeAsync deferred initialization (call #{InvocationId})", invocationId);
            _asyncLogger?.Error(ex, "InitializeAsync call #{InvocationId} failed", invocationId);
        }
        finally
        {
            this.ResumeLayout(true);
        }
    }

    /// <summary>
    /// Run deferred initialization tasks after OnShown (health check, ViewModel, dashboard auto-show).
    /// Called from OnShown to perform non-blocking background initialization.
    /// </summary>
    private async Task RunDeferredInitializationAsync(CancellationToken cancellationToken)
    {
        // [PERF] Background startup health check
        _ = Task.Run(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_serviceProvider == null) return;

                using var scope = _serviceProvider.CreateScope();
                cancellationToken.ThrowIfCancellationRequested();
                await Program.RunStartupHealthCheckAsync(scope.ServiceProvider).ConfigureAwait(false);
                _logger?.LogInformation("Deferred startup health check completed");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Initialization timeout: Deferred startup health check canceled after 30 seconds");
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
                cancellationToken.ThrowIfCancellationRequested();
                if (_serviceProvider == null) return;
                await WileyWidget.WinForms.Services.UiTestDataSeeder.SeedIfEnabledAsync(_serviceProvider).ConfigureAwait(false);
                _logger?.LogDebug("Deferred test data seeding completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Initialization timeout: Deferred test data seeding canceled after 30 seconds");
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
            _logger?.LogInformation("OnShown: Starting deferred background initialization");
            ApplyStatus("Initializing...");
            cancellationToken.ThrowIfCancellationRequested();

            // MainViewModel initialization and dashboard data load
            _asyncLogger?.Information("MainForm OnShown: Initializing MainViewModel and dashboard data");
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
                    await MainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
                    if (this.InvokeRequired)
                    {
                        await this.InvokeAsyncNonBlockingTask(
                            _ =>
                            {
                                _logger?.LogInformation("MainViewModel initialized successfully");
                                _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                                return Task.CompletedTask;
                            }).ConfigureAwait(true);
                    }
                    else
                    {
                        _logger?.LogInformation("MainViewModel initialized successfully");
                        _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Dashboard initialization cancelled");
                    _asyncLogger?.Information("MainForm OnShown: Dashboard initialization cancelled");
                    ApplyStatus("Initialization cancelled");
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
                        catch (Exception uiEx)
                        {
                            _logger?.LogDebug(uiEx, "Suppressed UI helper failure while showing initialization error dialog");
                        }
                    }
                    return;
                }
            }
            else
            {
                _logger?.LogWarning("MainViewModel not available in service provider");
            }

            // [PERF] Auto-show initial dashboard panel
            if (!_dashboardAutoShown && _panelNavigator != null)
            {
                try
                {
                    _logger?.LogInformation("Showing initial dashboard panel...");
                    ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>("Enterprise Vital Signs", DockingStyle.Fill, allowFloating: false);
                    _dashboardAutoShown = true;
                    _logger?.LogInformation("Initial dashboard panel shown successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to show initial dashboard panel");
                    ShowErrorDialog("Startup Error", "Could not load dashboard. Check logs.");
                }
            }

            ApplyStatus("Ready");
            _logger?.LogInformation("OnShown: Deferred initialization completed");

            // [PERF] Late image validation for ribbon/menu (ensures all images loaded)
            try
            {
                _logger?.LogDebug("OnShown: Running late image validation pass");
                if (_ribbon != null)
                {
                    _ribbon.ValidateAndConvertImages(_logger);
                }
                if (_menuStrip != null)
                {
                    _menuStrip.ValidateAndConvertImages(_logger);
                }
                _logger?.LogDebug("OnShown: Late image validation completed");
            }
            catch (Exception validationEx)
            {
                _logger?.LogError(validationEx, "OnShown: Late image validation failed");
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("OnShown initialization cancelled");
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
            throw new InvalidOperationException("Form handle not created - cannot initialize layout");
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

            int totalLoaded = (loadedMru as System.Collections.ICollection) is System.Collections.ICollection coll ? coll.Count : loadedMru.Count();
            int filtered = totalLoaded - _mruList.Count;
            _logger?.LogDebug("MRU list loaded: {Count} items ({TotalLoaded} loaded, {Filtered} filtered)", _mruList.Count, totalLoaded, filtered);
            RefreshMruMenu();
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
            _panelNavigator.ShowPanel<WileyWidget.WinForms.Controls.Panels.AnalyticsHubPanel>("Analytics Hub", reportPath, DockingStyle.Right, allowFloating: true);
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
                    try { ShowErrorDialog("Invalid File Path", $"The file path '{file}' is invalid and will be skipped."); } catch (Exception uiEx) { _logger?.LogDebug(uiEx, "Suppressed UI helper failure while showing Invalid File Path dialog"); }
                    continue;
                }

                _asyncLogger?.Debug("Processing dropped file: {File} (ext: {Ext})", file, ext);
                _logger?.LogInformation("Processing dropped file: {File} (ext: {Ext})", file, ext);

                // Add to MRU with optimized in-memory update
                try
                {
                    _windowStateService.AddToMru(file);
                    var existingIndex = _mruList.IndexOf(file);
                    if (existingIndex >= 0)
                    {
                        _mruList.RemoveAt(existingIndex);
                    }

                    _mruList.Insert(0, file);
                    if (_mruList.Count > 10) _mruList.RemoveAt(_mruList.Count - 1);
                    RefreshMruMenu();
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
            catch (Exception uiEx) { _logger?.LogDebug(uiEx, "Suppressed UI helper failure while showing import completion dialog"); }
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

    protected override void OnShown(EventArgs e)
    {
        PrepareStartupFadeIn();
        base.OnShown(e);
        BeginStartupFadeInIfNeeded();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var redrawSuspended = TrySuspendRedraw("ONSHOWN");
        SuspendLayout();

        try
        {
            _logger?.LogInformation("🌟 MainForm.OnShown - keeping UI thread light for immediate responsiveness");
            StartUiResponsivenessProbe();

            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider unavailable.");
            }

            ApplyStatus("Ready — loading workspace...");
            Update();
            QueueDeferredStartupUiPhases();

            _logger?.LogInformation("✅ OnShown completed in {ElapsedMs}ms — form is now fully responsive", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OnShown critical failure");
            try
            {
                UIHelper.ShowMessageOnUI(this, "Startup error — check logs.", "Wiley Widget", MessageBoxButtons.OK, MessageBoxIcon.Warning, _logger);
            }
            catch (Exception uiEx)
            {
                _logger?.LogDebug(uiEx, "Failed to display OnShown startup error dialog");
            }
        }
        finally
        {
            ResumeLayout(performLayout: true);
            ResumeRedraw(redrawSuspended, "ONSHOWN");
        }
    }

    private void QueueDeferredStartupUiPhases()
    {
        if (Interlocked.Exchange(ref _startupUiPhasesQueued, 1) == 1)
        {
            return;
        }

        _startupUiPhasesIndex = 0;
        _startupUiPhasesTimer?.Stop();
        _startupUiPhasesTimer?.Dispose();

        _startupUiPhasesTimer = new System.Windows.Forms.Timer { Interval = 35 };
        _startupUiPhasesTimer.Tick += HandleDeferredStartupUiPhase;
        _startupUiPhasesTimer.Start();
    }

    private void HandleDeferredStartupUiPhase(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing)
        {
            CompleteDeferredStartupUiPhases();
            return;
        }

        try
        {
            switch (_startupUiPhasesIndex)
            {
                case 0:
                    InitializeChrome();
                    QueueDeferredChromeInitialization();
                    InitializeStartupNavigation();
                    LoadMruList();
                    ApplyStatus("Ready — loading dashboard...");
                    break;

                case 1:
                    if (ShouldAutoLoadPrimaryPanelOnStartup())
                    {
                        LoadPrimaryDashboardPanel();
                    }
                    else
                    {
                        _logger?.LogDebug("Startup primary panel preload skipped (set WILEYWIDGET_PRELOAD_PRIMARY_PANEL=true to enable)");
                    }
                    break;

                case 2:
                    if (ShouldAutoOpenJarvisOnStartup())
                    {
                        TryOpenJarvisStartupPanel();
                    }

                    _ = InitializeAsync(CancellationToken.None);
                    ApplyStatus("Ready");
                    CompleteDeferredStartupUiPhases();
                    return;

                default:
                    CompleteDeferredStartupUiPhases();
                    return;
            }

            _startupUiPhasesIndex++;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Deferred startup phase {Phase} failed", _startupUiPhasesIndex);
            CompleteDeferredStartupUiPhases();
            UIHelper.ShowMessageOnUI(this, "Dashboard load had an issue — check logs.", "Startup Note", MessageBoxButtons.OK, MessageBoxIcon.Information, _logger);
        }
    }

    private void CompleteDeferredStartupUiPhases()
    {
        if (_startupUiPhasesTimer != null)
        {
            _startupUiPhasesTimer.Tick -= HandleDeferredStartupUiPhase;
            _startupUiPhasesTimer.Stop();
            _startupUiPhasesTimer.Dispose();
            _startupUiPhasesTimer = null;
        }

        Interlocked.Exchange(ref _startupUiPhasesQueued, 0);
    }

    private void InitializeStartupNavigation()
    {
        if (_panelNavigationService != null)
        {
            return;
        }

        InitializeLayoutComponents();

        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider unavailable.");
        }

        var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetService<Microsoft.Extensions.Logging.ILogger<PanelNavigationService>>(_serviceProvider);

        _panelNavigationService = new PanelNavigationService(
            owner: this,
            serviceProvider: _serviceProvider,
            logger: navLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PanelNavigationService>.Instance);

        if (_tabbedMdi != null)
        {
            _panelNavigationService.SetTabbedManager(_tabbedMdi);
            _logger?.LogDebug("TabbedMDIManager wired");
        }

        _panelNavigator = _panelNavigationService;
    }

    private void LoadPrimaryDashboardPanel()
    {
        if (IsDisposed || Disposing || _panelNavigationService == null)
        {
            return;
        }

        var panelSw = System.Diagnostics.Stopwatch.StartNew();
        _panelNavigationService.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(
            panelName: "Enterprise Vital Signs",
            preferredStyle: DockingStyle.Fill,
            allowFloating: false);
        _logger?.LogDebug("Enterprise Vital Signs loaded in {ElapsedMs}ms", panelSw.ElapsedMilliseconds);
    }

    private void TryOpenJarvisStartupPanel()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (!ShouldAutoOpenJarvisOnStartup() || !EnsureRightDockPanelInitialized())
        {
            return;
        }

        var jarvisTab = FindRightDockTab("RightDockTab_JARVIS");
        if (jarvisTab != null && _rightDockTabs != null)
        {
            _rightDockTabs.SelectedTab = jarvisTab;
        }

        if (_rightDockPanel != null)
        {
            _rightDockPanel.Visible = true;
            _rightDockPanel.BringToFront();
        }
    }

    private bool ShouldAutoLoadPrimaryPanelOnStartup()
    {
        if (_uiConfig.IsUiTestHarness || IsUiTestEnvironment())
        {
            return true;
        }

        var preloadPrimaryPanel = Environment.GetEnvironmentVariable("WILEYWIDGET_PRELOAD_PRIMARY_PANEL");
        return string.Equals(preloadPrimaryPanel, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preloadPrimaryPanel, "1", StringComparison.OrdinalIgnoreCase);
    }

    private void QueueOnShownStartupWorkflow(CancellationToken cancellationToken)
    {
        try
        {
            BeginInvoke((MethodInvoker)(() => _ = RunOnShownStartupWorkflowAsync(cancellationToken)));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to queue OnShown startup workflow");
        }
    }

    private async Task RunOnShownStartupWorkflowAsync(CancellationToken cancellationToken)
    {
        var onShownStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var redrawSuspended = TrySuspendRedraw("ONSHOWN");
        SuspendLayout();

        try
        {
            _logger?.LogInformation("🌟 MainForm.OnShown - Wiley Widget launching default dashboard...");

            cancellationToken.ThrowIfCancellationRequested();

            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is not initialized.");
            }

            var dockingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // ── 1. Make sure the docking/tab system is ready (do this BEFORE navigation service)
            InitializeLayoutComponents();
            dockingStopwatch.Stop();
            _logger?.LogDebug("OnShown Phase 1 (Docking setup) in {ElapsedMs}ms", dockingStopwatch.ElapsedMilliseconds);

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            // ── 2. Create the navigation service (this was the missing piece!)
            var navServiceStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<Microsoft.Extensions.Logging.ILogger<PanelNavigationService>>(_serviceProvider);

            _panelNavigationService = new PanelNavigationService(
                owner: this,
                serviceProvider: _serviceProvider,
                logger: navLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PanelNavigationService>.Instance);

            // If you're using the beautiful TabbedMDIManager layout (recommended for modern look)
            if (_tabbedMdi != null)
            {
                _panelNavigationService.SetTabbedManager(_tabbedMdi);
                _logger?.LogDebug("TabbedMDIManager wired to PanelNavigationService");
            }

            // Synchronise _panelNavigator (used by Navigation.cs ShowPanel<T>/ShowForm<T>/ClosePanel)
            // with the concrete instance created here, so that both fields always share the same
            // PanelNavigationService and no second instance is lazily constructed on first navigation.
            _panelNavigator = _panelNavigationService;

            navServiceStopwatch.Stop();
            _logger?.LogDebug("OnShown Phase 2 (Navigation service setup) in {ElapsedMs}ms", navServiceStopwatch.ElapsedMilliseconds);

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            // NOTE: DI registration validation is handled by WinFormsDiValidator (runs on a
            // background thread — see startup log "OK JARVISChatUserControl registered successfully").
            // Do NOT probe panel registrations here by calling ActivatorUtilities.CreateInstance:
            // complex ScopedPanelBase controls (e.g. JARVISChatUserControl) create a DI scope in
            // their constructor and the probe dispose cascades into live scoped services, which
            // disposes the real panel that is already mounted in the right-dock tab.

            // ── 3. PERF OPTIMIZATION: Defer ALL panel creation to reduce OnShown blocking time (~150ms gain)
            // Show form immediately, load panels asynchronously
            _logger?.LogDebug("OnShown Phase 3: Deferring panel creation for faster perceived startup");

            // Defer primary panel creation to unblock OnShown completion.
            // ShowPanel is synchronous — no async/await required here.
            // The previous double-BeginInvoke (async void inside async void) caused FlaUI tests
            // to time out because the outer lambda exited before the inner lambda even queued on
            // the UI thread, leaving panels invisible to UI Automation for 30-90 s per test.
            QueueDeferredPrimaryPanelLoad(cancellationToken);

            // Defer secondary panel even further to reduce startup thrash
            BeginInvoke((MethodInvoker)(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (_panelNavigationService == null || IsDisposed || Disposing)
                    {
                        return;
                    }

                    if (!ShouldAutoOpenJarvisOnStartup())
                    {
                        _logger?.LogDebug("OnShown deferred phase: JARVIS Chat auto-open skipped");
                        return;
                    }

                    if (!EnsureRightDockPanelInitialized())
                    {
                        _logger?.LogWarning("OnShown deferred phase: right dock panel unavailable; JARVIS auto-show skipped");
                        return;
                    }

                    var jarvisTab = FindRightDockTab("RightDockTab_JARVIS");
                    if (jarvisTab != null && _rightDockTabs != null && !ReferenceEquals(_rightDockTabs.SelectedTab, jarvisTab))
                    {
                        _rightDockTabs.SelectedTab = jarvisTab;
                    }

                    if (_rightDockPanel != null)
                    {
                        _rightDockPanel.Visible = true;
                        _rightDockPanel.BringToFront();
                    }

                    _logger?.LogDebug("OnShown deferred phase: JARVIS Chat auto-show confirmed via right dock tab selection");
                }
                catch (Exception deferredEx)
                {
                    _logger?.LogWarning(deferredEx, "Failed to open deferred JARVIS Chat panel during OnShown");
                }
            }));

            // Req 5 (IAsyncInitializable): call InitializeAsync after panels are deferred.
            // Handles theme propagation, visibility notifications, and nav-strip hardening.
            // NOTE: Do NOT cast an async lambda to MethodInvoker — that creates an async-void
            // delegate whose Task is silently dropped.  Fire the Task explicitly and attach an
            // error-continuation on the UI scheduler so failures are still logged.
            BeginInvoke((MethodInvoker)(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _logger?.LogInformation("[STARTUP-DIAG] Invoking MainForm.InitializeAsync from OnShown deferred invoke (ThreadId={ThreadId})", Thread.CurrentThread.ManagedThreadId);
                var initializeTask = InitializeAsync(cancellationToken);

                _ = initializeTask.ContinueWith(
                    t => _logger?.LogError(
                        t.Exception?.InnerException ?? t.Exception,
                        "InitializeAsync failed during OnShown deferred invoke"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.FromCurrentSynchronizationContext());

                _ = initializeTask.ContinueWith(
                    _ => _logger?.LogInformation("[STARTUP-DIAG] MainForm.InitializeAsync completed from OnShown deferred invoke"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.FromCurrentSynchronizationContext());
            }));

            _logger?.LogInformation("✅ SUCCESS — OnShown completed, panels loading asynchronously for faster startup");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("OnShown startup workflow canceled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "💥 CRITICAL — Failed during OnShown initialization");

            // Show error in deferred context, not blocking OnShown
            BeginInvoke(() =>
            {
                UIHelper.ShowMessageOnUI(
                    this,
                    "Whoops! The application had a little hiccup starting up.\n\n" +
                    "Check the log file for details.\n\n" +
                    "We'll get this fixed faster than Brick can say 'Stay classy, Wiley!'",
                    "Wiley Widget Startup Issue",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    _logger);
            });
        }
        finally
        {
            ResumeLayout(performLayout: true);
            ResumeRedraw(redrawSuspended, "ONSHOWN");
            onShownStopwatch.Stop();
            _logger?.LogInformation("OnShown completed in {ElapsedMs}ms", onShownStopwatch.ElapsedMilliseconds);
        }
    }

    private void QueueDeferredPrimaryPanelLoad(CancellationToken cancellationToken)
    {
        try
        {
            BeginInvoke((MethodInvoker)(() => _ = RunDeferredPrimaryPanelLoadAsync(cancellationToken)));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to queue deferred primary panel load");
        }
    }

    private async Task RunDeferredPrimaryPanelLoadAsync(CancellationToken cancellationToken)
    {
        var primaryPanelStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger?.LogDebug("Deferred Phase 3a (Enterprise Vital Signs) scheduled");

        try
        {
            if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            {
                return;
            }

            await Task.Yield();

            _logger?.LogDebug("Deferred Phase 3a (Enterprise Vital Signs) executing on UI thread");

            if (cancellationToken.IsCancellationRequested || _panelNavigationService == null || IsDisposed || Disposing)
            {
                return;
            }

            _panelNavigationService.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(
                panelName: "Enterprise Vital Signs",
                preferredStyle: DockingStyle.Fill,
                allowFloating: false);

            primaryPanelStopwatch.Stop();
            _logger?.LogDebug("Deferred Phase 3a (Enterprise Vital Signs) in {ElapsedMs}ms", primaryPanelStopwatch.ElapsedMilliseconds);

            _statusProgressService?.Complete("Startup", "Welcome to Wiley Widget — Municipal Finance, Supercharged!");
        }
        catch (Exception ex) when (ex is Win32Exception)
        {
            _logger?.LogDebug(ex, "Deferred panel load hit handle race — retrying after delay");

            await Task.Delay(100, cancellationToken).ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                _panelNavigationService?.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(
                    panelName: "Enterprise Vital Signs",
                    preferredStyle: DockingStyle.Fill,
                    allowFloating: false);
            }
            catch (Exception retryEx)
            {
                _logger?.LogWarning(retryEx, "Deferred primary panel retry failed after handle-race delay");
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Deferred Phase 3a canceled");
        }
        catch (Exception primaryEx)
        {
            _logger?.LogError(primaryEx, "Failed to load primary panel in deferred phase");
            UIHelper.ShowMessageOnUI(
                this,
                "Whoops! The dashboard had a little hiccup starting up.\n\n" +
                "Check the log file for details.\n\n" +
                "We'll get this fixed faster than Brick can say 'Stay classy, Wiley!'",
                "Wiley Widget Startup Issue",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                _logger);

            try
            {
                _panelNavigationService?.ShowPanel<WileyWidget.WinForms.Controls.Panels.SettingsPanel>("Settings", DockingStyle.Fill);
            }
            catch (Exception fallbackPanelEx)
            {
                _logger?.LogWarning(fallbackPanelEx, "Failed to load fallback Settings panel after primary startup panel failure");
            }
        }
    }

    private void CancelOnShownStartupWorkflow()
    {
        CompleteDeferredStartupUiPhases();

        _onShownStartupCts?.Cancel();
        _onShownStartupCts?.Dispose();
        _onShownStartupCts = null;
    }

    private bool ShouldAutoOpenJarvisOnStartup()
    {
        if (_uiConfig.IsUiTestHarness || IsUiTestEnvironment())
        {
            // Allow an explicit test override to auto-open JARVIS during UI automation runs.
            // Tests may set WILEYWIDGET_UI_AUTOMATION_JARVIS=true to force the panel open
            var jarvisAuto = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS");
            if (string.Equals(jarvisAuto, "true", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("ShouldAutoOpenJarvisOnStartup: Jarvis automation override detected; auto-opening JARVIS for UI automation");
                return true;
            }

            return false;
        }

        var disableAutoOpen = Environment.GetEnvironmentVariable("WILEYWIDGET_DISABLE_STARTUP_JARVIS");
        if (string.Equals(disableAutoOpen, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(disableAutoOpen, "1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var forceAutoOpen = Environment.GetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS");
        var shouldForceAutoOpen = string.Equals(forceAutoOpen, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(forceAutoOpen, "1", StringComparison.OrdinalIgnoreCase);

        if (!shouldForceAutoOpen)
        {
            _logger?.LogDebug("ShouldAutoOpenJarvisOnStartup: defaulting to disabled for responsive startup (set WILEYWIDGET_AUTO_OPEN_JARVIS=true to enable)");
            return false;
        }

        // Microsoft.WinForms.Utilities.Shared.dll is a transitive dependency of BlazorWebView
        // It's part of Visual Studio IDE tooling and not guaranteed to be present in all
        // .NET 10 WindowsDesktop runtime installations. If missing, JARVIS may fail, but
        // the application should still function. Log at Debug level to avoid alarming users.
        var winFormsUtilitiesSharedPath = Path.Combine(AppContext.BaseDirectory, "Microsoft.WinForms.Utilities.Shared.dll");
        if (!File.Exists(winFormsUtilitiesSharedPath))
        {
            _logger?.LogDebug(
                "Optional JARVIS dependency not found: {DependencyPath}. " +
                "JARVIS Assistant may not function correctly if BlazorWebView is unavailable. " +
                "Ensure Visual Studio with .NET Desktop workload is installed for full functionality.",
                winFormsUtilitiesSharedPath);
            // Don't block startup - allow JARVIS to attempt initialization and fail gracefully if needed
        }

        return true;
    }

    private bool ShouldRunStartupFadeIn()
    {
        return !_uiConfig.IsUiTestHarness && !IsUiTestEnvironment();
    }

    private void PrepareStartupFadeIn()
    {
        if (_startupFadePrepared || !ShouldRunStartupFadeIn())
        {
            return;
        }

        try
        {
            Opacity = 1.0d; // Fixed: Full opacity from startup to ensure immediate visibility of panels and chrome
            _startupFadePrepared = true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Startup fade preparation failed; continuing without transition");
            _startupFadePrepared = true;
        }
    }

    private void BeginStartupFadeInIfNeeded()
    {
        if (!_startupFadePrepared || IsDisposed || Disposing)
        {
            return;
        }

        if (_startupFadeTimer != null)
        {
            return;
        }

        try
        {
            _startupFadeTimer = new System.Windows.Forms.Timer
            {
                Interval = 16
            };

            _startupFadeTimer.Tick += HandleStartupFadeTick;
            _startupFadeTimer.Start();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Startup fade timer failed; completing transition immediately");
            CompleteStartupFadeIn();
        }
    }

    private void HandleStartupFadeTick(object? sender, EventArgs e)
    {
        try
        {
            if (IsDisposed || Disposing)
            {
                CompleteStartupFadeIn();
                return;
            }

            Opacity = Math.Min(1d, Opacity + 0.1d);
            if (Opacity >= 1d)
            {
                CompleteStartupFadeIn();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Startup fade tick failed; completing transition immediately");
            CompleteStartupFadeIn();
        }
    }

    private void CompleteStartupFadeIn()
    {
        try
        {
            Opacity = 1d;
        }
        catch
        {
        }

        if (_startupFadeTimer != null)
        {
            _startupFadeTimer.Stop();
            _startupFadeTimer.Tick -= HandleStartupFadeTick;
            _startupFadeTimer.Dispose();
            _startupFadeTimer = null;
        }

        _startupFadePrepared = false;
    }

    // NEW: Safe initialization (called automatically in real app, manually in tests)
    private void InitializeLayoutComponents()
    {
        var preloadRightDock = Environment.GetEnvironmentVariable("WILEYWIDGET_PRELOAD_RIGHT_DOCK");
        var shouldPreloadRightDock = string.Equals(preloadRightDock, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preloadRightDock, "1", StringComparison.OrdinalIgnoreCase);

        // Microsoft UI-thread guidance recommends keeping startup callbacks short and deferring
        // non-essential work. The right dock (Activity Log + JARVIS composition) is expensive,
        // so we lazy-load it on first explicit use unless prewarm is explicitly requested.
        if (shouldPreloadRightDock && _serviceProvider != null)
        {
            try
            {
                var initialized = EnsureRightDockPanelInitialized();
                if (initialized)
                {
                    _logger?.LogDebug("InitializeLayoutComponents: preloaded real right dock panel via factory (WILEYWIDGET_PRELOAD_RIGHT_DOCK enabled)");
                }
                else
                {
                    _logger?.LogWarning("InitializeLayoutComponents: right dock prewarm requested but initialization returned false — using temporary panel");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "InitializeLayoutComponents: right dock prewarm failed — using temporary panel");
                _rightDockPanel = null;
                _rightDockTabs = null;
                _rightDockJarvisPanel = null;
            }
        }

        // Temporary right-dock panel: used when DI is absent (tests) OR when the factory threw above.
        // The catch block resets _rightDockPanel to null so this condition catches both cases.
        if (_rightDockPanel == null || _rightDockPanel.IsDisposed)
        {
            var jarvisPanel = new Panel
            {
                Name = "JarvisPanel",
                Tag = "Jarvis",
                Width = 500,
                Dock = DockStyle.Right,
                MinimumSize = new Size(350, 0),
                MaximumSize = new Size(0, 0),   // 0,0 = no maximum constraint
            };
            _rightDockPanel = jarvisPanel;
            var host = (_contentHostPanel as Control) ?? (Control)this;
            host.Controls.Add(jarvisPanel);
            _logger?.LogDebug("InitializeLayoutComponents: temporary right dock panel added to {Host} (factory unavailable or failed)", host.Name);
        }

        InitializeMDIManager();
    }
}
