using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class RunStartupHealthCheckPostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public RunStartupHealthCheckPostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        private class TestDashboardService : IDashboardService
        {
            private readonly AppDbContext _ctx;

            public TestDashboardService(AppDbContext ctx) => _ctx = ctx;

            public Task<IEnumerable<DashboardItem>> GetDashboardDataAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<DashboardItem>());
            public Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<DashboardItem>());
            public Task RefreshDashboardAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;

            public async Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                var total = await _ctx.BudgetEntries.CountAsync(cancellationToken);
                var oldest = await _ctx.BudgetEntries.OrderBy(be => be.CreatedAt).Select(be => (DateTime?)be.CreatedAt).FirstOrDefaultAsync(cancellationToken);
                var newest = await _ctx.BudgetEntries.OrderByDescending(be => be.CreatedAt).Select(be => (DateTime?)be.CreatedAt).FirstOrDefaultAsync(cancellationToken);
                return (total, oldest, newest);
            }
        }

        [Fact, Trait("Category", "Integration")]
        public async Task RunStartupHealthCheck_CompletesWithoutException()
        {
            if (!_fixture.DockerAvailable) return;

            var connStr = _fixture.ConnectionString;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connStr));
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr));

            // Register TestDashboardService and real DataSeedingService
            services.AddScoped<IDashboardService, TestDashboardService>();
            services.AddScoped<DataSeedingService>();

            var provider = services.BuildServiceProvider();

            // Ensure DB and seed some data so GetDataStatisticsAsync has data to analyze
            await DatabaseHelpers.ResetDatabaseAsync(connStr);
            await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options))
            {
                await ctx.Database.MigrateAsync();
                var seeder = new DataSeedingService(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSeedingService>.Instance);
                await seeder.SeedBudgetDataAsync(force: true);
            }

            // Invoke private static Program.RunStartupHealthCheckAsync via reflection
            var programType = typeof(WileyWidget.WinForms.Program);
            var method = programType.GetMethod("RunStartupHealthCheckAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var task = (Task)method!.Invoke(null, new object[] { provider })!;
            await task; // Should not throw
        }
    }
}
