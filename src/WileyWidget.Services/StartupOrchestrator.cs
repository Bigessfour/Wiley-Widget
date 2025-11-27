using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Syncfusion.Licensing;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Orchestrates application startup sequence with telemetry, progress reporting, and dependency initialization.
/// Implements IHostedService for automatic execution on app start.
/// </summary>
public class StartupOrchestrator : IHostedService
{
    private readonly ILogger<StartupOrchestrator> _logger;
    private readonly ITelemetryService _telemetry;
    private readonly IStartupProgressReporter _progressReporter;
    private readonly IServiceProvider _serviceProvider;
    private readonly Tracer _tracer;
    private readonly TaskCompletionSource<bool> _completionSource = new();

    public Task<bool> CompletionTask => _completionSource.Task;

    public StartupOrchestrator(
        ILogger<StartupOrchestrator> logger,
        ITelemetryService telemetry,
        IStartupProgressReporter progressReporter,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _telemetry = telemetry;
        _progressReporter = progressReporter;
        _serviceProvider = serviceProvider;
        _tracer = TracerProvider.Default.GetTracer("StartupOrchestrator");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.Orchestration", SpanKind.Internal);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting application startup orchestration");
        _progressReporter.Report(0, "Initializing application...", isIndeterminate: true);

        try
        {
            // Phase 1: License registration (critical path)
            await RegisterSyncfusionLicenseAsync(cancellationToken);
            _progressReporter.Report(20, "License registered");

            // Phase 2: Secret vault initialization
            await InitializeSecretVaultAsync(cancellationToken);
            _progressReporter.Report(40, "Secrets loaded");

            // Phase 3: Database warmup
            await WarmupDatabaseAsync(cancellationToken);
            _progressReporter.Report(60, "Database ready");

            // Phase 4: Telemetry setup
            await InitializeTelemetryAsync(cancellationToken);
            _progressReporter.Report(80, "Telemetry configured");

            // Phase 5: Dashboard data preload (optional, in background)
            _ = Task.Run(() => PreloadDashboardAsync(cancellationToken), cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation("Startup orchestration completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            span.SetAttribute("startup.duration_ms", stopwatch.ElapsedMilliseconds);
            _telemetry.RecordMetric("Startup.Duration.Ms", stopwatch.ElapsedMilliseconds);
            _telemetry.RecordMetric("Startup.Success", 1);

            _progressReporter.Complete("Application ready");
            _completionSource.SetResult(true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Startup orchestration failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            _telemetry.RecordException(ex, ("phase", "startup"), ("duration_ms", stopwatch.ElapsedMilliseconds));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            _progressReporter.Complete("Startup failed - check logs");
            _completionSource.SetException(ex);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Startup orchestrator stopping");
        return Task.CompletedTask;
    }

    private async Task RegisterSyncfusionLicenseAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.RegisterSyncfusionLicense", SpanKind.Internal);
        _logger.LogInformation("Registering Syncfusion license");

        try
        {
            var vaultService = _serviceProvider.GetService<ISecretVaultService>();
            if (vaultService == null)
            {
                _logger.LogWarning("Vault service not available - continuing with trial mode");
                return;
            }

            // Make vault operations cancellation-safe
            var licenseKey = await vaultService.GetSecretAsync("SyncfusionLicenseKey")
                .WaitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                _logger.LogInformation("Syncfusion license registered successfully");
                span.SetAttribute("license.registered", true);
            }
            else
            {
                _logger.LogWarning("Syncfusion license key not found in vault - trial mode active");
                span.SetAttribute("license.registered", false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("License registration cancelled");
            throw; // Re-throw to allow orchestrator to handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Syncfusion license");
            _telemetry.RecordException(ex, ("phase", "license_registration"));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            // Non-fatal - continue with trial mode
        }
    }

    private async Task InitializeSecretVaultAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.InitializeSecretVault", SpanKind.Internal);
        _logger.LogInformation("Initializing secret vault");

        try
        {
            var vaultService = _serviceProvider.GetService<ISecretVaultService>();
            if (vaultService != null)
            {
                // Validate vault accessibility with cancellation support
                await vaultService.TestConnectionAsync().WaitAsync(cancellationToken);
                _logger.LogInformation("Secret vault initialized");
                span.SetAttribute("vault.initialized", true);
            }
            else
            {
                _logger.LogWarning("Secret vault service not registered");
                span.SetAttribute("vault.initialized", false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Vault initialization cancelled");
            throw; // Re-throw to propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize secret vault");
            _telemetry.RecordException(ex, ("phase", "vault_init"));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            throw; // Fatal - cannot proceed without secrets
        }
    }

    private async Task WarmupDatabaseAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.WarmupDatabase", SpanKind.Internal);
        _logger.LogInformation("Warming up database connection pool");

        try
        {
            // Use DbContextFactory for proper pooling and lifecycle management
            var dbContextFactory = _serviceProvider.GetService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>();

            if (dbContextFactory != null)
            {
                // Create context from factory and test connection
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var canConnect = await Task.Run(() => dbContext.Database.CanConnect(), cancellationToken);
                
                if (canConnect)
                {
                    _logger.LogInformation("Database connection pool warmed up");
                    span.SetAttribute("database.ready", true);
                }
                else
                {
                    _logger.LogWarning("Database connection test failed");
                    span.SetAttribute("database.ready", false);
                }
            }
            else
            {
                _logger.LogWarning("DbContextFactory not available for warmup");
                span.SetAttribute("database.ready", false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Database warmup cancelled");
            // Non-fatal - just log and continue
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database warmup failed");
            _telemetry.RecordException(ex, ("phase", "database_warmup"));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            // Non-fatal - app can still function with cold start
        }
    }

    private async Task InitializeTelemetryAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.InitializeTelemetry", SpanKind.Internal);
        _logger.LogInformation("Initializing telemetry providers");

        try
        {
            // Validate OTEL endpoint connectivity
            await Task.Delay(50, cancellationToken); // Placeholder for actual endpoint health check
            _logger.LogInformation("Telemetry providers initialized");
            span.SetAttribute("telemetry.initialized", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry initialization failed");
            _telemetry.RecordException(ex, ("phase", "telemetry_init"));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            // Non-fatal - app can run without telemetry
        }
    }

    private async Task PreloadDashboardAsync(CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("Startup.PreloadDashboard", SpanKind.Internal);
        _logger.LogInformation("Preloading dashboard data in background");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dashboardService = scope.ServiceProvider.GetService<IDashboardService>();

            if (dashboardService != null)
            {
                var data = await dashboardService.GetDashboardDataAsync();
                _logger.LogInformation("Dashboard data preloaded successfully");
                span.SetAttribute("dashboard.preloaded", true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard preload failed - data will load on demand");
            _telemetry.RecordException(ex, ("phase", "dashboard_preload"));
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            // Non-fatal - dashboard will load on first view
        }
    }
}
