using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Initialization;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Services
{
    public interface IStartupOrchestrator
    {
        void Initialize();
        Task InitializeAsync();
        Task ValidateServicesAsync(IServiceProvider serviceProvider);
        void RunApplication(IServiceProvider serviceProvider);
        Task RunApplicationAsync(IServiceProvider serviceProvider);
    }

    public class StartupOrchestrator : IStartupOrchestrator
    {
        private readonly IWinFormsDiValidator _validator;
        private readonly ILogger<StartupOrchestrator> _logger;
        private readonly IThemeService _themeService;
        private readonly StartupOptions _startupOptions;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly List<string> _executedPhases = new();

        public StartupOrchestrator(
            IWinFormsDiValidator validator,
            ILogger<StartupOrchestrator> logger,
            IThemeService themeService,
            IOptions<StartupOptions> startupOptions,
            IServiceScopeFactory scopeFactory)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _startupOptions = startupOptions?.Value ?? throw new ArgumentNullException(nameof(startupOptions));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public void Initialize()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(_startupOptions.TotalPhaseBudgetMs);

            try
            {
                _logger.LogInformation("Orchestrating startup initialization with overall timeout: {Timeout}ms", _startupOptions.TotalPhaseBudgetMs);

                // Phase 1: Pre-UI
                Phase1_PreUI();
                EnforcePhaseOrder("Phase1_PreUI");

                // Check for cancellation
                cts.Token.ThrowIfCancellationRequested();

                _logger.LogInformation("Startup initialization completed");
            }
            catch (OperationCanceledException)
            {
                HandleInitializationTimeout();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during startup initialization");
                throw;
            }
            finally
            {
                cts.Dispose();
            }
        }

        public Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

        private void Phase1_PreUI()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Phase 1 (Pre-UI): starting theme validation (license already registered in Program.Main)");

                // NOTE: Syncfusion license is now registered in Program.Main BEFORE any Syncfusion code runs
                // This phase only validates theme settings, not license registration
                if (_startupOptions.EnableLicenseValidation)
                {
                    _logger.LogInformation("License validation enabled - license was registered earlier in Program.Main before theme initialization");
                }
                else
                {
                    _logger.LogInformation("License validation is disabled via StartupOptions.");
                }

                if (_startupOptions.EnableThemeValidation)
                {
                    var themeName = _themeService.CurrentTheme;
                    if (string.IsNullOrWhiteSpace(themeName))
                    {
                        themeName = AppThemeColors.DefaultTheme;
                    }

                    themeName = AppThemeColors.ValidateTheme(themeName, _logger);

                    try
                    {
                        AppThemeColors.EnsureThemeAssemblyLoadedForTheme(themeName, _logger);
                        SfSkinManager.ApplicationVisualTheme = themeName;
                        _logger.LogInformation("Pre-UI theme applied: {ThemeName}", themeName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Pre-UI theme application failed for {ThemeName}. Falling back to Default.", themeName);
                        AppThemeColors.EnsureThemeAssemblyLoadedForTheme(AppThemeColors.DefaultTheme, _logger);
                        SfSkinManager.ApplicationVisualTheme = AppThemeColors.DefaultTheme;
                    }
                }
                else
                {
                    _logger.LogInformation("Startup theme validation is disabled via StartupOptions.");
                }
            }
            finally
            {
                stopwatch.Stop();
                WileyWidget.WinForms.Diagnostics.StartupInstrumentation.RecordPhaseTime("Phase 1 - Pre-UI (License + Theme)", stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task ValidateServicesAsync(IServiceProvider serviceProvider)
        {
#if !DEBUG
            // Full 87-service DI scan is expensive and targets developer feedback only.
            // Skip entirely in Release builds; run only on Debug or CI.
            _logger.LogDebug("Skipping full DI validation in Release build.");
            return;
#endif
            if (!_startupOptions.EnableDiValidation)
            {
                _logger.LogInformation("Startup DI validation is disabled via StartupOptions.");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting service validation in background thread...");

            try
            {
                _ = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);

                var timeoutSeconds = Math.Max(1, _startupOptions.TimeoutSeconds);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var validationTask = Task.Run(() => _validator.ValidateAll(serviceProvider), cts.Token);
                var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);
                var completedTask = await Task.WhenAny(validationTask, delayTask).ConfigureAwait(false);

                if (completedTask == delayTask)
                {
                    HandleDiValidationFailure(new TimeoutException($"DI validation exceeded {timeoutSeconds}s timeout."));
                    return;
                }

                cts.Cancel();
                var result = await validationTask.ConfigureAwait(false);

                if (!result.IsValid)
                {
                    HandleDiValidationFailure(new InvalidOperationException("DI validation completed with errors."), result);
                }

                // [PERF] Initialize all IAsyncInitializable services in background to avoid UI blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Allow structures time to develop before initializing services
                        await Task.Delay(50).ConfigureAwait(false);
                        _logger.LogInformation("Initializing IAsyncInitializable services in background...");

                        // [FIX] Wait for the main form handle + Shown event before starting background warmup.
                        await Task.Delay(300);

                        // [FIX] Use IServiceScopeFactory consistently to ensure background threads have a stable scope
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var asyncInitializables = scope.ServiceProvider.GetServices<WileyWidget.Abstractions.IAsyncInitializable>();

                            // Materialize the list immediately to avoid late-binding issues
                            var initializablesList = asyncInitializables.ToList();
                            _logger.LogInformation("Discovered {Count} IAsyncInitializable services for background warmup", initializablesList.Count);

                            foreach (var service in initializablesList)
                            {
                                try
                                {
                                    _logger.LogDebug("Background warmup: Initializing {ServiceType}...", service.GetType().Name);

                                    // Use a dedicated timeout per service to prevent deadlocks from blocking entire chain
                                    using var serviceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    await service.InitializeAsync(serviceCts.Token).ConfigureAwait(false);

                                    _logger.LogDebug("Background warmup: {ServiceType} initialized successfully", service.GetType().Name);
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.LogWarning("Background warmup: Initialization of {ServiceType} timed out (30s limit)", service.GetType().Name);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Background warmup: Failed to initialize {ServiceType}", service.GetType().Name);
                                }
                            }
                        }
                        _logger.LogInformation("All IAsyncInitializable services background initialization sequence completed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to initialize IAsyncInitializable services");
                    }
                });
            }
            catch (OperationCanceledException oce)
            {
                HandleDiValidationFailure(oce);
            }
            catch (TimeoutException tex)
            {
                HandleDiValidationFailure(tex);
            }
            catch (Exception ex)
            {
                HandleDiValidationFailure(ex);
            }
            finally
            {
                stopwatch.Stop();
                WileyWidget.WinForms.Diagnostics.StartupInstrumentation.RecordPhaseTime("DI Validation", stopwatch.ElapsedMilliseconds);
            }
        }

        private void HandleDiValidationFailure(Exception exception, WileyWidget.Services.Abstractions.DiValidationResult? result = null)
        {
            if (result == null)
            {
                _logger.LogWarning(exception, "DI validation encountered an issue; continuing startup.");
                return;
            }

            _logger.LogWarning(exception, "DI validation completed with errors; continuing startup.");
            _logger.LogWarning("DI validation summary: {Summary}", result.GetSummary());
        }

        public void RunApplication(IServiceProvider serviceProvider)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator.RunApplicationAsync ENTRY");
            _logger.LogInformation("Starting WinForms application main loop...");

            // Create a scope to resolve scoped services like MainForm
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            using var scope = scopeFactory.CreateScope();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Resolving MainForm from DI...");
            var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(scope.ServiceProvider);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: MainForm resolved successfully");
            _logger.LogInformation("[STARTUP-DIAG] MainForm resolved (HashCode={HashCode}, IsHandleCreated={IsHandleCreated}, ThreadId={ThreadId})",
                mainForm.GetHashCode(),
                mainForm.IsHandleCreated,
                Thread.CurrentThread.ManagedThreadId);

            // Store reference to MainFormInstance for programmatic access
            Program.MainFormInstance = mainForm;

            mainForm.Shown += (_, __) =>
            {
                _logger.LogDebug("[STARTUP-DIAG] MainForm.Shown fired - completing splash");
                Program.CompleteSplash("Ready");
            };

            // MainForm owns deferred async initialization in OnShown.
            // Kick off DI validation after the window is shown, but do not invoke
            // MainForm.InitializeAsync() here to avoid duplicate startup paths.
            mainForm.Shown += (_, __) =>
            {
                _logger.LogInformation("[STARTUP-DIAG] MainForm.Shown fired - kicking off ValidateServicesAsync");
                _ = Task.Run(() => ValidateServicesAsync(serviceProvider));
            };

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Calling Application.Run(mainForm) - entering message loop");
            _logger.LogInformation("Entering WinForms message loop with Application.Run()");
            System.Windows.Forms.Application.Run(mainForm);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Application.Run() returned - message loop exited");
            _logger.LogInformation("Application.Run() returned - application exiting");
        }

        public Task RunApplicationAsync(IServiceProvider serviceProvider)
        {
            RunApplication(serviceProvider);
            return Task.CompletedTask;
        }

        private Task RunOnStaThread(Action action)
        {
            var tcs = new TaskCompletionSource();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        /// <summary>
        /// Phase 6: Data Load and Activation Phase (Async Post-Shown)
        /// Async load data (e.g., DB health check, RefreshDashboardAsync).
        /// Apply fixed docking layout and activate default content panel.
        /// Guard: Timeout StartupOptions.PhaseTimeouts.DataLoadMs.
        /// Configuration: ActivateControl(centralPanel), Refresh form.
        /// On timeout, log HandleInitializationTimeout.
        /// Add final metrics report via StartupMetrics.GetReport/LogMetrics.
        /// </summary>
        private async Task Phase6_LoadAndActivateLayout(MainForm mainForm)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Phase 6 (Load and Activate Layout): starting async data load and fixed layout activation");

                var timeoutMs = _startupOptions.PhaseTimeouts?.DataLoadMs ?? 30000; // Default 30s
                using var cts = new CancellationTokenSource(timeoutMs);

                try
                {
                    // Async load data (e.g., DB health check, RefreshDashboardAsync)
                    var dataLoadTasks = new List<Task>();
                    using var scope = _scopeFactory.CreateScope();

                    var healthCheckService = scope.ServiceProvider.GetService(typeof(WileyWidget.Services.HealthCheckService)) as WileyWidget.Services.HealthCheckService;
                    if (healthCheckService != null)
                    {
                        dataLoadTasks.Add(RunStartupHealthCheckAsync(healthCheckService, cts.Token));
                    }
                    else
                    {
                        _logger.LogInformation("HealthCheckService not registered; skipping startup health check task.");
                    }

                    await Task.WhenAll(dataLoadTasks).ConfigureAwait(false);

                    mainForm.Refresh();

                    _logger.LogInformation("Phase 6 completed successfully");
                }
                catch (OperationCanceledException)
                {
                    HandleInitializationTimeout();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Phase 6 failed");
                }

                // Add final metrics report via StartupMetrics.GetReport/LogMetrics
                var report = WileyWidget.WinForms.Diagnostics.StartupInstrumentation.GetFormattedMetrics();
                WileyWidget.WinForms.Diagnostics.StartupInstrumentation.LogInitializationState(_logger);
            }
            finally
            {
                stopwatch.Stop();
                WileyWidget.WinForms.Diagnostics.StartupInstrumentation.RecordPhaseTime("Phase 6 - Load and Activate Layout", stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task RunStartupHealthCheckAsync(WileyWidget.Services.HealthCheckService healthCheckService, CancellationToken cancellationToken)
        {
            var report = await healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Startup health check completed with status {Status}", report.OverallStatus);
        }

        private void HandleInitializationTimeout()
        {
            _logger.LogWarning("Phase 6 initialization timeout");
        }

        private void EnforcePhaseOrder(string currentPhase)
        {
            lock (_executedPhases)
            {
                _executedPhases.Add(currentPhase);

                // Check dependencies
                if (currentPhase == "Phase1_PreUI")
                {
                    // No dependencies
                }
                else if (currentPhase == "Phase6_LoadAndActivateLayout")
                {
                    if (!_executedPhases.Contains("Phase1_PreUI"))
                    {
                        _logger.LogWarning("Phase order violation: {CurrentPhase} executed before Phase1_PreUI", currentPhase);
                    }
                }
                // Add more checks as needed
            }
        }
    }
}
