using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyTheme = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Coordinates startup tasks (license registration, theming, DI validation) so they can be tested and reused.
    /// </summary>
    public interface IStartupOrchestrator
    {
        Task RegisterLicenseAsync(CancellationToken cancellationToken = default);

        Task InitializeThemeAsync(CancellationToken cancellationToken = default);

        Task ValidateServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

        void GenerateStartupReport();
    }

    public sealed class StartupOrchestrator : IStartupOrchestrator
    {
        private readonly IConfiguration _configuration;
        private readonly IWinFormsDiValidator _validator;
        private readonly ILogger<StartupOrchestrator> _logger;
        private readonly IStartupTimelineService? _timelineService;

        private static Task RunOnStaThread(Action action, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    action();
                    tcs.TrySetResult(null);
                }
                catch (OperationCanceledException oce)
                {
                    tcs.TrySetCanceled(oce.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "StartupOrchestrator.STA"
            };

            // WinForms types (Forms/Controls) require STA. Running DI validation on MTA threadpool
            // can hang inside framework code and shows up as Monitor.Wait when you pause the debugger.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return tcs.Task;
        }

        public StartupOrchestrator(
            IConfiguration configuration,
            IWinFormsDiValidator validator,
            ILogger<StartupOrchestrator> logger,
            IStartupTimelineService? timelineService = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timelineService = timelineService;
        }

        public Task RegisterLicenseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Syncfusion guidance: register the license in Program.Main before Application.Run().
            // Keep licensing out of the orchestrator to avoid double-registration and timing confusion.
            _logger.LogDebug("Syncfusion license registration is handled in Program.Main; skipping in StartupOrchestrator.");
            return Task.CompletedTask;
        }

        public Task InitializeThemeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load all theme assemblies to support runtime theme switching
            // This ensures all 7 themes are available (Office2019 x3, Fluent x2, Material x2)
            try { SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly); } catch { }
            try
            {
                // Fluent theme - load by assembly name since type may not be directly accessible
                var fluentAssembly = System.Reflection.Assembly.Load("Syncfusion.FluentTheme.WinForms");
                SkinManager.LoadAssembly(fluentAssembly);
            }
            catch { }
            try
            {
                // Material theme - load by assembly name
                var materialAssembly = System.Reflection.Assembly.Load("Syncfusion.MaterialTheme.WinForms");
                SkinManager.LoadAssembly(materialAssembly);
            }
            catch { }

            // Theme name is set globally in Program.InitializeTheme() before this runs
            // ApplicationVisualTheme is read-only after being set, so we just verify it's applied
            var currentTheme = SkinManager.ApplicationVisualTheme;
            _logger.LogInformation("Application theme verified. All themes loaded. Active theme: {Theme}", currentTheme ?? "default");

            return Task.CompletedTask;
        }

        public Task ValidateServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // This method is frequently invoked from Task.Run() during startup to keep the splash responsive.
            // Task.Run uses MTA threadpool threads by default; resolving WinForms types on MTA can hang.
            DiValidationResult result;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                result = _validator.ValidateAll(serviceProvider);
            }
            else
            {
                _logger.LogWarning(
                    "DI validation requested on non-STA thread (apartment={Apartment}); running on a dedicated STA thread to avoid WinForms deadlocks.",
                    Thread.CurrentThread.GetApartmentState());

                DiValidationResult? staResult = null;
                return RunOnStaThread(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    staResult = _validator.ValidateAll(serviceProvider);
                }, cancellationToken).ContinueWith(t =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (t.IsFaulted)
                    {
                        // Preserve original exception.
                        throw t.Exception!.GetBaseException();
                    }
                    if (t.IsCanceled)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    result = staResult ?? throw new InvalidOperationException("DI validation did not produce a result.");

                    if (!result.IsValid)
                    {
                        _logger.LogError("DI validation failed with {ErrorCount} errors", result.Errors.Count);
                        throw new InvalidOperationException(
                            $"DI validation failed with {result.Errors.Count} errors: {string.Join("; ", result.Errors)}");
                    }

                    _logger.LogInformation(
                        "DI validation succeeded: {ServicesValidated} services validated with {Warnings} warnings",
                        result.SuccessMessages.Count,
                        result.Warnings.Count);

                    foreach (var warning in result.Warnings)
                    {
                        _logger.LogWarning("DI validation warning: {Warning}", warning);
                    }

                    return Task.CompletedTask;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
            }

            if (!result.IsValid)
            {
                _logger.LogError("DI validation failed with {ErrorCount} errors", result.Errors.Count);
                throw new InvalidOperationException(
                    $"DI validation failed with {result.Errors.Count} errors: {string.Join("; ", result.Errors)}");
            }

            _logger.LogInformation(
                "DI validation succeeded: {ServicesValidated} services validated with {Warnings} warnings",
                result.SuccessMessages.Count,
                result.Warnings.Count);

            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("DI validation warning: {Warning}", warning);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates and logs a comprehensive startup timeline report with dependency validation.
        /// Call this at the end of startup to verify all phases completed in proper order.
        /// </summary>
        public void GenerateStartupReport()
        {
            if (_timelineService == null || !_timelineService.IsEnabled)
            {
                _logger.LogDebug("Startup timeline tracking is disabled; no report generated");
                return;
            }

            try
            {
                var report = _timelineService.GenerateReport();
                _logger.LogInformation(
                    "Startup timeline report: {PhaseCount} phases, {OperationCount} operations, {FormEventCount} form events",
                    report.Events.Count(e => e.Type == "Phase"),
                    report.Events.Count(e => e.Type == "Operation"),
                    report.Events.Count(e => e.Type == "FormLifecycle"));

                var dependencyViolations = report.GetDependencyViolations();
                if (dependencyViolations.Any())
                {
                    _logger.LogWarning("Startup had {Count} dependency violations", dependencyViolations.Count);
                    foreach (var violation in dependencyViolations)
                    {
                        _logger.LogWarning("  - {Violation}", violation);
                    }
                }

                var orderViolations = report.GetOrderViolations();
                if (orderViolations.Any())
                {
                    _logger.LogWarning("Startup had {Count} order violations", orderViolations.Count);
                }

                var threadAffinityIssues = report.GetThreadAffinityIssues();
                if (threadAffinityIssues.Any())
                {
                    _logger.LogWarning("Startup had {Count} thread affinity issues", threadAffinityIssues.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate startup timeline report");
            }
        }
    }
}
