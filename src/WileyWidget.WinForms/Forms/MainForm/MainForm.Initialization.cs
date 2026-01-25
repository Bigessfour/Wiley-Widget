using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Services;

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
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
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

            // Align UI with persisted theme from service
            if (_themeService != null)
            {
                _themeService.ApplyTheme(_themeService.CurrentTheme);
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] _themeService is null in InitializeAsync");
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
                // Docking layout loading moved to OnShown for better timing
            }

            // Phase 1: Show priority panels for faster startup
            if (_uiConfig.UseSyncfusionDocking && _panelNavigator != null)
            {
                _logger?.LogInformation("Showing priority panels for faster startup");
                try
                {
                    // Priority panels: Dashboard only to reduce clutter
                    _logger?.LogInformation("[PANEL] Showing Dashboard");
                    // Ensure UI handle is available; small delay helps controls create handles on slower machines
                    try { await Task.Delay(100, cancellationToken); } catch (OperationCanceledException) { return; }
                    this.InvokeIfRequired(() => _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Right, allowFloating: true));
                    _logger?.LogInformation("Priority panels shown successfully");
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

            _asyncLogger?.Information("MainForm.InitializeAsync completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("InitializeAsync canceled");
            _asyncLogger?.Information("InitializeAsync canceled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during InitializeAsync deferred initialization");
            _asyncLogger?.Error(ex, "InitializeAsync failed");
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

            // [PERF] Z-order management for docking
            if (_uiConfig.UseSyncfusionDocking)
            {
                try { EnsureDockingZOrder(); }
                catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure docking z-order"); }
            }

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
                    await MainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
                    if (this.InvokeRequired)
                    {
                        await this.InvokeAsync(() =>
                        {
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
            if (!_dashboardAutoShown && _panelNavigator != null)
            {
                try
                {
                    _logger?.LogInformation("Showing initial dashboard panel...");
                    ShowPanel<Controls.DashboardPanel>("Dashboard", null, DockingStyle.Top);
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
            var projectRoot = Directory.GetCurrentDirectory();
            var logsDirectory = Path.Combine(projectRoot, "logs");
            Directory.CreateDirectory(logsDirectory);
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
            
            _logger?.LogDebug("MRU list loaded: {Count} items ({TotalLoaded} loaded, {Filtered} filtered)", 
                _mruList.Count, loadedMru.Count(), loadedMru.Count() - _mruList.Count);
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
            ShowReportsPanel(reportPath);
            _reportViewerLaunched = true;
            _logger?.LogInformation("Report viewer opened for CLI path: {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open report viewer for {ReportPath}", reportPath);
        }
    }

    /// <summary>
    /// Shows the Reports panel with optional auto-load path for CLI-launched reports.
    /// </summary>
    private void ShowReportsPanel(string reportPath)
    {
        try
        {
            _panelNavigator.ShowPanel<Controls.ReportsPanel>("Reports", reportPath, DockingStyle.Right, allowFloating: true);
            _logger?.LogInformation("Reports panel shown with auto-load path: {ReportPath}", reportPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show reports panel");
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

                // Add to MRU with error handling for storage directory issues
                try
                {
                    _windowStateService.AddToMru(file);
                    _mruList.Clear();
                    _mruList.AddRange(_windowStateService.LoadMru());
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
                    try
                    {
                        var result = await _fileImportService.ImportDataAsync<Dictionary<string, object>>(file, cancellationToken);
                        HandleImportResult(file, result);
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
            var count = (result.Data as System.Collections.IDictionary)?.Count ?? 0;
            _logger?.LogInformation("Successfully imported {File}: {Count} properties", Path.GetFileName(file), count);
            try { UIHelper.ShowMessageOnUI(this, $"File imported: {Path.GetFileName(file)}\nParsed {count} data properties",
                "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information, _logger); } catch { }
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
