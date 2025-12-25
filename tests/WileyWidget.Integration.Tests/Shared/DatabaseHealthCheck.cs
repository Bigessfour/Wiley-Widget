using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;

namespace WileyWidget.Integration.Tests
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public DatabaseHealthCheck(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync(cancellationToken);
                // Perform a lightweight query to validate DB connectivity
                var count = await db.BudgetEntries.CountAsync(cancellationToken);
                return HealthCheckResult.Healthy($"Database reachable, entries={count}");
            }
            catch (OperationCanceledException)
            {
                // propagate cancellation so callers can observe TaskCanceledException
                throw;
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database check failed", ex);
            }
        }
    }
}
