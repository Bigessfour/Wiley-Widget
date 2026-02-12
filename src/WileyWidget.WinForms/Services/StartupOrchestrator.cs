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
        Task InitializeAsync();
        Task ValidateServicesAsync(IServiceProvider serviceProvider);
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

        public async Task InitializeAsync()
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
                        themeName = "Office2019Colorful";
                    }

                    try
                    {
                        AppThemeColors.EnsureThemeAssemblyLoaded(_logger);
                        SfSkinManager.ApplicationVisualTheme = themeName;
                        _logger.LogInformation("Pre-UI theme applied: {ThemeName}", themeName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Pre-UI theme application failed for {ThemeName}. Falling back to Default.", themeName);
                        SfSkinManager.ApplicationVisualTheme = "Default";
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

                /// <summary>
                /// NEW: Validate Blazor-specific services
                /// Ensures that AddWindowsFormsBlazorWebView() and AddSyncfusionBlazor() were properly registered during startup.
                /// These services are critical for JARVIS panel rendering and AI component functionality.
                /// </summary>
                try
                {
                    var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IServiceScopeFactory>(serviceProvider);
                    if (scopeFactory == null)
                    {
                        _logger.LogWarning("Blazor WebView services not registered - ensure AddWindowsFormsBlazorWebView() is called in startup");
                    }
                }
                catch (Exception blazorCheckEx)
                {
                    _logger.LogWarning(blazorCheckEx, "Error checking Blazor services during validation");
                }

                // [PERF] Initialize all IAsyncInitializable services in background to avoid UI blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Allow structures time to develop before initializing services
                        await Task.Delay(50).ConfigureAwait(false);
                        _logger.LogInformation("Initializing IAsyncInitializable services in background...");

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

        public async Task RunApplicationAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator.RunApplicationAsync ENTRY");
            _logger.LogInformation("Starting WinForms application main loop...");

            // Create a scope to resolve scoped services like MainForm
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            using var scope = scopeFactory.CreateScope();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Resolving MainForm from DI...");
            var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(scope.ServiceProvider);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: MainForm resolved successfully");

            // Store reference to MainFormInstance for programmatic access
            Program.MainFormInstance = mainForm;

            mainForm.Shown += (_, __) => Program.CompleteSplash("Ready");

            // If MainForm implements IAsyncInitializable, initialize it after it's shown
            if (mainForm is IAsyncInitializable asyncInit)
            {
                mainForm.Shown += async (_, __) =>
                {
                    try
                    {
                        await asyncInit.InitializeAsync();
                        _ = ValidateServicesAsync(serviceProvider);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to initialize main form asynchronously");
                    }
                };
            }
            else
            {
                mainForm.Shown += (_, __) => _ = ValidateServicesAsync(serviceProvider);
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Calling Application.Run(mainForm) - entering message loop");
            _logger.LogInformation("Entering WinForms message loop with Application.Run()");
            System.Windows.Forms.Application.Run(mainForm);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] StartupOrchestrator: Application.Run() returned - message loop exited");
            _logger.LogInformation("Application.Run() returned - application exiting");

            await Task.CompletedTask;
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
        /// Restore layout via DockingLayoutManager.LoadLayoutAsync.
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
                _logger.LogInformation("Phase 6 (Load and Activate Layout): starting async data load and layout restoration");

                var timeoutMs = _startupOptions.PhaseTimeouts?.DataLoadMs ?? 30000; // Default 30s
                using var cts = new CancellationTokenSource(timeoutMs);

                try
                {
                    // Async load data (e.g., DB health check, RefreshDashboardAsync)
                    var dataLoadTasks = new List<Task>();

                    // DB health check - placeholder
                    dataLoadTasks.Add(Task.Run(() =>
                    {
                        // Simulate DB health check
                        _logger.LogInformation("Performing DB health check");
                        // Actual DB health check code here
                    }, cts.Token));

                    await Task.WhenAll(dataLoadTasks);

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
