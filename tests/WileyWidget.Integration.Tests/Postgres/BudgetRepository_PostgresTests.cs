using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using WileyWidget.Data;
using WileyWidget.Services;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class BudgetRepositoryPostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public BudgetRepositoryPostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact, Trait("Category", "Integration")]
        public async Task BudgetRepository_GetByFiscalYear_ReturnsSeededEntries()
        {
            if (!_fixture.DockerAvailable) return;

            var connStr = _fixture.ConnectionString;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connStr));
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr));

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetRequiredService<ILogger<BudgetRepository>>();

            // Ensure clean and apply migrations
            await DatabaseHelpers.ResetDatabaseAsync(connStr);
            await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options))
            {
                await ctx.Database.MigrateAsync();
            }

            // Seed data
            await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options))
            {
                var seeder = new DataSeedingService(ctx, NullLogger<DataSeedingService>.Instance);
                var result = await seeder.SeedBudgetDataAsync(force: true);
            }

            var repo = new BudgetRepository(factory, cache, null);

            // Determine fiscal year from DB
            await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options))
            {
                var period = ctx.BudgetPeriods.OrderByDescending(p => p.Year).FirstOrDefault();
                Assert.NotNull(period);

                var entries = (await repo.GetByFiscalYearAsync(period!.Year)).ToList();
                Assert.True(entries.Count > 0, "BudgetRepository should return seeded entries for the fiscal year");
            }
        }
    }
}
