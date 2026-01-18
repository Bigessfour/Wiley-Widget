using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Background service that performs asynchronous startup orchestration after the main form is shown.
    /// This prevents blocking the UI thread during license validation, theme initialization, and DI validation.
    /// Follows async-initialization-pattern: synchronous startup → deferred async initialization.
    /// </summary>
    public class StartupHostedService : BackgroundService
    {
        private readonly IStartupOrchestrator _orchestrator;
        private readonly ILogger<StartupHostedService> _logger;

        public StartupHostedService(
            IStartupOrchestrator orchestrator,
            ILogger<StartupHostedService> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the startup orchestration asynchronously without blocking the main UI thread.
        /// Runs sequentially after MainForm is shown.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting deferred async initialization via StartupHostedService");

                // Call orchestrator async methods (non-blocking)
                await _orchestrator.InitializeAsync();
                _logger.LogInformation("✓ Startup initialization completed");

                _logger.LogInformation("Async startup orchestration completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Startup orchestration was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during async startup orchestration: {Message}", ex.Message);
                // Do not rethrow - allow app to continue running but log failure
            }
        }
    }
}
