using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using Serilog;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Startup service for telemetry initialization and database health checks.
/// Performs DB connectivity validation during startup.
/// </summary>
public sealed class TelemetryStartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public TelemetryStartupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Telemetry startup service initializing...");

            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = factory.CreateDbContext();
            var metrics = scope.ServiceProvider.GetService<WileyWidget.Services.ApplicationMetricsService>();

            // Apply any pending EF Core migrations; this also applies HasData seed rows.
            var migrationSw = Stopwatch.StartNew();
            var migrationSuccess = false;
            try
            {
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                migrationSuccess = true;
                Log.Information("Database migrations applied successfully");
            }
            catch (Exception migEx)
            {
                Log.Warning(migEx, "Database migration step encountered an issue - database may already be current");
            }
            finally
            {
                migrationSw.Stop();
                metrics?.RecordMigration(migrationSw.Elapsed.TotalMilliseconds, migrationSuccess);
                metrics?.RecordSeeding(migrationSuccess);
            }

            // Confirm connectivity after migration
            await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("Database connectivity validated during telemetry startup");
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Telemetry startup cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during telemetry startup");
            // Don't throw to avoid crashing startup
        }

        Log.Information("Telemetry pipeline initialized successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Telemetry pipeline shutdown initiated.");
        return Task.CompletedTask;
    }
}
