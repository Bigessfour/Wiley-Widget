using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Forms;
using WileyWidget.Abstractions;

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

        public StartupOrchestrator(
            IWinFormsDiValidator validator,
            ILogger<StartupOrchestrator> logger)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Orchestrating startup initialization...");
            await Task.CompletedTask;
        }

        public async Task ValidateServicesAsync(IServiceProvider serviceProvider)
        {
            _logger.LogInformation("Starting service validation in background thread...");

            try
            {
                // Run validation in a background task to avoid blocking the caller (e.g. Program.cs or UI thread)
                var result = await Task.Run(() => _validator.ValidateAll(serviceProvider));

                if (!result.IsValid)
                {
                    _logger.LogWarning("DI Validation completed with errors. See log for details.");
                }
            }
            catch (OperationCanceledException oce)
            {
                // Log timeout/cancellation details without failing the application
                _logger.LogWarning(oce, "Service validation was canceled (likely timeout in telemetry or other service initialization). " +
                    "Application will continue but some validation checks were skipped.");
            }
            catch (TimeoutException tex)
            {
                // Log timeout details without failing the application
                _logger.LogWarning(tex, "Service validation timed out. Application will continue but some services may be unvalidated.");
            }
            catch (Exception ex)
            {
                // Log unexpected errors but don't fail startup
                _logger.LogError(ex, "Service validation failed with an exception: {ExceptionType}: {Message}",
                    ex.GetType().Name, ex.Message);
            }
        }

        public async Task RunApplicationAsync(IServiceProvider serviceProvider)
        {
            _logger.LogInformation("Starting WinForms application main loop...");

            // Create a scope to resolve scoped services like MainForm
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            using var scope = scopeFactory.CreateScope();
            var mainForm = scope.ServiceProvider.GetService(typeof(MainForm)) as MainForm
                ?? throw new InvalidOperationException("MainForm is not registered.");

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
    }
}
