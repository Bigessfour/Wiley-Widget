using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// No-op hosted service retained for backward compatibility after removing Azure Application Insights.
/// Emits structured logging so startup/shutdown events remain observable.
/// </summary>
public sealed class TelemetryStartupService : IHostedService
{
    private readonly ILogger<TelemetryStartupService> _logger;

    public TelemetryStartupService(ILogger<TelemetryStartupService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telemetry pipeline disabled; startup event logged for diagnostics only.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telemetry pipeline disabled; shutdown event logged for diagnostics only.");
        return Task.CompletedTask;
    }
}
