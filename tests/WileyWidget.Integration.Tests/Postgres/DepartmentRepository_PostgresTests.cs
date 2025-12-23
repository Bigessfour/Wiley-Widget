using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class DepartmentRepositoryPostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public DepartmentRepositoryPostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact, Trait("Category", "Integration")]
        public async Task DepartmentRepository_Add_Get_Delete_Workflow()
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
            var logger = provider.GetRequiredService<ILogger<DepartmentRepository>>();

            // Ensure DB schema
            await DatabaseHelpers.ResetDatabaseAsync(connStr);
            await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options))
            {
                await ctx.Database.MigrateAsync();
            }

            var repo = new DepartmentRepository(factory, cache, logger);

            // Add
            var dept = new Department { Name = "Integration Dept", DepartmentCode = "INTG-CRUD" };
            await repo.AddAsync(dept);

            // Get by code
            var fetched = await repo.GetByCodeAsync("INTG-CRUD");
            Assert.NotNull(fetched);
            Assert.Equal("INTG-CRUD", fetched!.DepartmentCode);

            // Update
            fetched.Name = "Integration Dept Updated";
            await repo.UpdateAsync(fetched);
            var updated = await repo.GetByIdAsync(fetched.Id);
            Assert.Equal("Integration Dept Updated", updated!.Name);

            // Delete
            var deleted = await repo.DeleteAsync(fetched.Id);
            Assert.True(deleted);
            var afterDelete = await repo.GetByIdAsync(fetched.Id);
            Assert.Null(afterDelete);
        }
    }
}
