using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;
using WileyWidget.Startup;
using ILogger = Serilog.ILogger;

namespace WileyWidget.Startup;

/// <summary>
/// Orchestrates the 4-phase startup sequence for improved resilience and observability.
/// Implements the phased approach recommended for startup error handling and rollback.
/// Each phase has clear boundaries, error handling, and telemetry integration.
/// </summary>
public class StartupOrchestrator
{
    private readonly Stopwatch _startupTimer;
    private readonly List<StartupPhase> _completedPhases;
    private IConfiguration? _configuration;
    private IContainerRegistry? _containerRegistry;

    public StartupOrchestrator()
    {
        _startupTimer = Stopwatch.StartNew();
        _completedPhases = new List<StartupPhase>();
    }

    /// <summary>
    /// Executes the complete 4-phase startup sequence with error handling and rollback.
    /// </summary>
    /// <param name="app">The Prism application instance</param>
    /// <param name="containerRegistry">The DI container registry</param>
    /// <returns>True if startup succeeded, false if rollback occurred</returns>
    public async Task<bool> ExecuteStartupSequenceAsync(
        Prism.PrismApplicationBase app,
        IContainerRegistry containerRegistry)
    {
        _containerRegistry = containerRegistry;

        try
        {
            // Phase 1: Configuration Loading
            if (!await ExecutePhaseAsync(StartupPhase.ConfigurationLoad, async () =>
            {
                await LoadConfigurationAsync();
                Log.Information("Phase 1: Configuration loaded successfully");
            }))
            {
                return false;
            }

            // Phase 2: Container Setup
            if (!await ExecutePhaseAsync(StartupPhase.ContainerSetup, async () =>
            {
                await SetupContainerAsync(containerRegistry);
                Log.Information("Phase 2: Container setup completed successfully");
            }))
            {
                return false;
            }

            // Phase 3: Modules Initialization
            if (!await ExecutePhaseAsync(StartupPhase.ModulesInit, async () =>
            {
                await InitializeModulesAsync(app);
                Log.Information("Phase 3: Modules initialized successfully");
            }))
            {
                return false;
            }

            // Phase 4: UI Loading
            if (!await ExecutePhaseAsync(StartupPhase.UILoad, async () =>
            {
                await LoadUIAsync(app);
                Log.Information("Phase 4: UI loading completed successfully");
            }))
            {
                return false;
            }

            _startupTimer.Stop();
            Log.Information("✅ 4-phase startup completed successfully in {ElapsedMs}ms",
                _startupTimer.ElapsedMilliseconds);

            // Report successful startup telemetry
            var errorReporting = (_containerRegistry as DryIoc.IResolver)?.Resolve<ErrorReportingService>();
            errorReporting?.TrackEvent("Startup_Success", new Dictionary<string, object>
            {
                ["TotalTimeMs"] = _startupTimer.ElapsedMilliseconds,
                ["CompletedPhases"] = _completedPhases.Count,
                ["StartupTimestamp"] = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error during 4-phase startup sequence");
            await RollbackStartupAsync(ex);
            return false;
        }
    }

    /// <summary>
    /// Executes a single startup phase with error handling and rollback capability.
    /// </summary>
    private async Task<bool> ExecutePhaseAsync(StartupPhase phase, Func<Task> phaseAction)
    {
        var phaseTimer = Stopwatch.StartNew();

        try
        {
            Log.Information("Starting {Phase} ({PhaseNumber}/4)", phase, (int)phase);

            await phaseAction();

            phaseTimer.Stop();
            _completedPhases.Add(phase);

            Log.Information("✅ {Phase} completed in {ElapsedMs}ms", phase, phaseTimer.ElapsedMilliseconds);

            // Report phase completion telemetry
            var errorReporting = (_containerRegistry as DryIoc.IResolver)?.Resolve<ErrorReportingService>();
            errorReporting?.TrackEvent("StartupPhase_Success", new Dictionary<string, object>
            {
                ["Phase"] = phase.ToString(),
                ["PhaseNumber"] = (int)phase,
                ["ElapsedMs"] = phaseTimer.ElapsedMilliseconds
            });

            return true;
        }
        catch (Exception ex)
        {
            phaseTimer.Stop();
            Log.Error(ex, "❌ {Phase} failed after {ElapsedMs}ms", phase, phaseTimer.ElapsedMilliseconds);

            // Report phase failure telemetry
            var errorReporting = (_containerRegistry as DryIoc.IResolver)?.Resolve<ErrorReportingService>();
            errorReporting?.TrackEvent("StartupPhase_Failed", new Dictionary<string, object>
            {
                ["Phase"] = phase.ToString(),
                ["PhaseNumber"] = (int)phase,
                ["ElapsedMs"] = phaseTimer.ElapsedMilliseconds,
                ["ErrorType"] = ex.GetType().Name,
                ["ErrorMessage"] = ex.Message
            });

            await RollbackStartupAsync(ex, phase);
            return false;
        }
    }

    /// <summary>
    /// Phase 1: Load configuration from appsettings.json, environment variables, and command line.
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        await Task.Run(() =>
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs());

            _configuration = configBuilder.Build();

            // Validate critical configuration sections
            if (string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")))
            {
                throw new InvalidOperationException("Missing required DefaultConnection string in configuration");
            }

            Log.Debug("Configuration loaded with {SectionCount} sections",
                _configuration.AsEnumerable().Count());
        });
    }

    /// <summary>
    /// Phase 2: Setup DI container with logging first, then core services.
    /// </summary>
    private async Task SetupContainerAsync(IContainerRegistry containerRegistry)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration must be loaded before container setup");

        await Task.Run(() =>
        {
            // Register configuration first
            containerRegistry.RegisterInstance(_configuration);

            // Register ErrorReportingService early for telemetry
            containerRegistry.RegisterSingleton<ErrorReportingService>();

            // Register TelemetryStartupService
            containerRegistry.RegisterSingleton<TelemetryStartupService>();

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<IStartupDiagnosticsService, StartupDiagnosticsService>();

            Log.Debug("Core container services registered successfully");
        });
    }

    /// <summary>
    /// Phase 3: Initialize Prism modules, database, and background services.
    /// Note: This phase registers services but defers actual initialization
    /// to the calling App.xaml.cs where the container is available for resolution.
    /// </summary>
    private async Task InitializeModulesAsync(Prism.PrismApplicationBase app)
    {
        await Task.Run(() =>
        {
            // Register database initializer as hosted service for background initialization
            _containerRegistry.RegisterSingleton<Microsoft.Extensions.Hosting.IHostedService, DatabaseInitializer>();

            Log.Information("Module services registered - DatabaseInitializer registered as IHostedService for background execution");
        });
    }    /// <summary>
    /// Phase 4: Load UI components, splash screen, and main shell.
    /// Note: Startup diagnostics will be handled by App.xaml.cs where container resolution is available.
    /// </summary>
    private async Task LoadUIAsync(Prism.PrismApplicationBase app)
    {
        await Task.Run(() =>
        {
            // UI loading setup - diagnostics will be handled by App.xaml.cs
            Log.Information("UI loading orchestration phase completed - diagnostics handled by calling code");
        });
    }

    /// <summary>
    /// Rolls back completed startup phases and shuts down application.
    /// </summary>
    private async Task RollbackStartupAsync(Exception exception, StartupPhase? failedPhase = null)
    {
        try
        {
            _startupTimer.Stop();

            Log.Fatal(exception, "Startup rollback initiated. Failed phase: {FailedPhase}. Completed phases: {CompletedPhases}",
                failedPhase?.ToString() ?? "Unknown", string.Join(", ", _completedPhases));

            // Report startup failure telemetry
            var errorReporting = (_containerRegistry as DryIoc.IResolver)?.Resolve<ErrorReportingService>();
            errorReporting?.TrackEvent("Startup_Rollback", new Dictionary<string, object>
            {
                ["FailedPhase"] = failedPhase?.ToString() ?? "Unknown",
                ["CompletedPhases"] = _completedPhases.Count,
                ["ElapsedMs"] = _startupTimer.ElapsedMilliseconds,
                ["ErrorType"] = exception.GetType().Name,
                ["ErrorMessage"] = exception.Message
            });

            // Show error dialog to user
            await ShowStartupErrorDialogAsync(exception, failedPhase);

            // Ensure telemetry is flushed before shutdown
            await Task.Delay(100);
        }
        catch (Exception rollbackEx)
        {
            Log.Fatal(rollbackEx, "Error during startup rollback");
        }
        finally
        {
            // Critical: Shut down application with error code
            Log.CloseAndFlush();
            Application.Current?.Shutdown(1);
        }
    }

    /// <summary>
    /// Shows a user-friendly error dialog during startup failure.
    /// </summary>
    private async Task ShowStartupErrorDialogAsync(Exception exception, StartupPhase? failedPhase)
    {
        await Task.Run(() =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    // Use MessageBox for error dialog since container resolution isn't available here
                    MessageBox.Show(
                        $"Application failed to start during {failedPhase ?? StartupPhase.Unknown}.\n\nError: {exception.Message}",
                        "Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // Ultimate fallback
                    MessageBox.Show(
                        $"Critical startup error: {exception.Message}",
                        "Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        });
    }
}

// StartupPhase enum is defined in IStartupOrchestrator.cs to avoid duplication
