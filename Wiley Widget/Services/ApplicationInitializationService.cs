using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Service for coordinating application initialization.
/// Manages the startup sequence and coordinates all initialization services.
/// </summary>
public class ApplicationInitializationService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly LoggingService _loggingService;
    private readonly ErrorHandlingService _errorHandlingService;
    private readonly AssemblyValidationService _assemblyValidationService;
    private readonly SplashScreenService _splashScreenService;
    private readonly StartupPerformanceService _performanceService;
    private readonly ResourceMonitorService _resourceMonitorService;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ApplicationInitializationService.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="loggingService">The logging service.</param>
    /// <param name="errorHandlingService">The error handling service.</param>
    /// <param name="assemblyValidationService">The assembly validation service.</param>
    /// <param name="splashScreenService">The splash screen service.</param>
    /// <param name="performanceService">The startup performance service.</param>
    /// <param name="resourceMonitorService">The resource monitor service.</param>
    public ApplicationInitializationService(
        IConfiguration configuration,
        LoggingService loggingService,
        ErrorHandlingService errorHandlingService,
        AssemblyValidationService assemblyValidationService,
        SplashScreenService splashScreenService,
        StartupPerformanceService performanceService,
        ResourceMonitorService resourceMonitorService)
    {
        _configuration = configuration;
        _loggingService = loggingService;
        _errorHandlingService = errorHandlingService;
        _assemblyValidationService = assemblyValidationService;
        _splashScreenService = splashScreenService;
        _performanceService = performanceService;
        _resourceMonitorService = resourceMonitorService;
    }

    /// <summary>
    /// Initializes the application with the complete startup sequence.
    /// </summary>
    public async Task InitializeApplicationAsync()
    {
        var startupTimer = Stopwatch.StartNew();

        try
        {
            Log.Information("🚀 === Core Startup Sequence Started ===");

            // Record startup begin
            _performanceService.RecordCoreStartupBegin();

            // Microsoft Performance Goal: Track startup time and memory usage
            var initialMemory = GC.GetTotalMemory(true) / 1024 / 1024;
            var initialThreads = Process.GetCurrentProcess().Threads.Count;
            Log.Information("📊 Initial State - Memory: {MemoryMB}MB, Threads: {Threads}", initialMemory, initialThreads);

            // Execute initialization steps safely
            await ExecuteInitializationStepsAsync();

            // Start resource monitoring
            _resourceMonitorService.StartMonitoring();

            // Complete startup
            startupTimer.Stop();
            _performanceService.RecordCoreStartupComplete();

            Log.Information("✅ Core Startup Complete - Elapsed: {ElapsedMs}ms", startupTimer.ElapsedMilliseconds);

            // Generate final report
            var totalElapsedMs = _performanceService.GenerateStartupReport();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 CRITICAL: Core Startup failed");
            _errorHandlingService.HandleStartupFailure(ex);
        }
    }

    /// <summary>
    /// Executes all initialization steps in the correct order.
    /// </summary>
    private async Task ExecuteInitializationStepsAsync()
    {
        // Step 1: Configure logging
        await _errorHandlingService.SafeExecuteAsync("Configure Serilog Logger",
            () => Task.Run(() => _loggingService.ConfigureSerilogLogger()));

        // Step 2: Enable self-logging
        await _errorHandlingService.SafeExecuteAsync("Enable Serilog SelfLog",
            () => Task.Run(() => _loggingService.EnableSerilogSelfLog()));

        // Step 3: Configure global exception handling
        _errorHandlingService.SafeExecute("Configure Global Exception Handling",
            () => _errorHandlingService.ConfigureGlobalExceptionHandling());

        // Step 4: Diagnose license bootstrap (NEW - helps debug license registration)
        _errorHandlingService.SafeExecute("Diagnose License Bootstrap",
            () => _loggingService.DiagnoseLicenseBootstrap());

        // Step 5: Validate assemblies
        _errorHandlingService.SafeExecute("Validate Syncfusion Assemblies",
            () => _assemblyValidationService.ValidateSyncfusionAssemblies());

        // Step 6: Configure services
        _errorHandlingService.SafeExecute("Configure Services",
            () => ConfigureServices());
    }

    /// <summary>
    /// Configures additional services.
    /// </summary>
    private void ConfigureServices()
    {
        // Configure any additional services here
        Log.Information("🔧 Additional services configured");
    }

    /// <summary>
    /// Gets the startup performance service.
    /// </summary>
    public StartupPerformanceService PerformanceService => _performanceService;

    /// <summary>
    /// Gets the resource monitor service.
    /// </summary>
    public ResourceMonitorService ResourceMonitorService => _resourceMonitorService;

    /// <summary>
    /// Disposes of the application initialization service.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _resourceMonitorService?.Dispose();
            }
            _disposed = true;
        }
    }
}
