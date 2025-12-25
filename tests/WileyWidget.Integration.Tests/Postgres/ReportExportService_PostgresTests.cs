using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using WileyWidget.Services;
using WileyWidget.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class ReportExportServicePostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public ReportExportServicePostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact, Trait("Category","Integration")]
        public async Task ExportService_ExportsBudgetEntries_ToCsv()
        {
            if (!_fixture.DockerAvailable) return;
            var connStr = _fixture.ConnectionString;

            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options;

            await using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
                var seeder = new DataSeedingService(ctx, NullLogger<DataSeedingService>.Instance);
                await seeder.SeedBudgetDataAsync(force: true);

                var entries = await ctx.BudgetEntries.Take(10).ToListAsync();

                var logger = new LoggerConfiguration().CreateLogger();
                var service = new ReportExportService(logger);

                var path = Path.Combine(Path.GetTempPath(), $"budget_export_{Guid.NewGuid():N}.csv");
                await service.ExportToCsvAsync(entries.Cast<object>(), path);

                Assert.True(File.Exists(path));
                var content = File.ReadAllText(path);
                Assert.NotEmpty(content);

                File.Delete(path);
            }
        }
    }
}
