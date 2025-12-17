using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// No-op hosted service retained for backward compatibility after removing Azure Application Insights.
/// Emits structured logging so startup/shutdown events remain observable.
/// </summary>
public sealed class TelemetryStartupService : IHostedService
{
    public TelemetryStartupService()
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Information("Telemetry pipeline disabled; startup event logged for diagnostics only.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Telemetry pipeline disabled; shutdown event logged for diagnostics only.");
        return Task.CompletedTask;
    }
}
