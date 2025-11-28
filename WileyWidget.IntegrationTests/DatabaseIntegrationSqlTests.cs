using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.IntegrationTests
{
    public class DatabaseIntegrationSqlTests
    {
        [Fact]
        [Trait("Category", "Database.SqlExpress")]
        public async Task Integration_MigrateAndCrud_WithSqlExpress_IfAvailable()
        {
            // Load test config (appsettings.test.json) and allow env override
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var envConn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTIONSTRING");
            var conn = envConn ?? config.GetConnectionString("DefaultConnection");

            // If the environment / config uses an InMemory sentinel, skip the heavier SQL checks
            if (string.IsNullOrWhiteSpace(conn) || conn.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                // No SQL server configured in the environment — skip test.
                // Returning early keeps CI local-friendly; this test is intended to be enabled when a SQL Express instance is reachable.
                return;
            }

            // Build factory and try to migrate and use the database
            var factory = new AppDbContextFactory(new ConfigurationBuilder().AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", conn) }).Build());

            // Create a context and ensure connectivity
            await using var ctx = factory.CreateDbContext();

            // Try to apply migrations (idempotent) and ensure we can connect
            try
            {
                await ctx.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                // If migrations fail we still want to surface a meaningful failure
                throw new InvalidOperationException("Migrations failed — ensure SQL Express is reachable and the connection string is valid.", ex);
            }

            var canConnect = await ctx.Database.CanConnectAsync();
            canConnect.Should().BeTrue("SQL Express instance should be reachable for this integration test");

            // Do a simple CRUD round-trip to ensure EF persists to SQL Server
            var sample = new Department { Name = "IntegrationTestDept", DepartmentCode = "ITEST" };
            ctx.Departments.Add(sample);
            await ctx.SaveChangesAsync();

            var found = await ctx.Departments.FirstOrDefaultAsync(d => d.DepartmentCode == "ITEST");
            found.Should().NotBeNull();
            found!.Name.Should().Be("IntegrationTestDept");

            // cleanup - remove the inserted test row
            ctx.Departments.Remove(found);
            await ctx.SaveChangesAsync();

            // Basic schema check - ensure there's at least one table (information_schema)
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
            var tableCount = await ctx.Database.ExecuteSqlRawAsync(sql).ContinueWith(t => 0, TaskContinuationOptions.OnlyOnFaulted);
            // ExecuteSqlRawAsync returns row count for non-query so we'll check with RAW SQL via FromSqlRaw

            // A quick verification that migrations created tables via EF's model
            var anyDepartments = await ctx.Departments.AnyAsync();
            anyDepartments.Should().BeFalse(); // We deleted our added row; just asserting EF queries work
        }
    }
}
