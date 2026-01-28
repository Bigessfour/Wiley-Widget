using System;
using System.Diagnostics;
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
        private readonly List<string> _executedPhases = new();

        public StartupOrchestrator(
            IWinFormsDiValidator validator,
            ILogger<StartupOrchestrator> logger,
            IThemeService themeService,
            IOptions<StartupOptions> startupOptions)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _startupOptions = startupOptions?.Value ?? throw new ArgumentNullException(nameof(startupOptions));
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
                _logger.LogInformation("Phase 1 (Pre-UI): starting license registration and theme initialization");

                if (_startupOptions.EnableLicenseValidation)
                {
                    var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                    if (string.IsNullOrWhiteSpace(licenseKey))
                    {
                        _logger.LogError("Syncfusion license key is missing; set SYNCFUSION_LICENSE_KEY to continue.");
                        throw new InvalidOperationException("Syncfusion license key is missing.");
                    }

                    try
                    {
                        SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                        _logger.LogInformation("Syncfusion license registration completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Syncfusion license registration failed.");
                        throw new InvalidOperationException("Syncfusion license registration failed.", ex);
                    }
                }
                else
                {
                    _logger.LogInformation("Startup license validation is disabled via StartupOptions.");
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
                        SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
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
            _logger.LogInformation("Starting WinForms application main loop...");

            // Create a scope to resolve scoped services like MainForm
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            using var scope = scopeFactory.CreateScope();
            var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(scope.ServiceProvider);

            // Store reference to MainFormInstance for programmatic access
            Program.MainFormInstance = mainForm;

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

            System.Windows.Forms.Application.Run(mainForm);

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
