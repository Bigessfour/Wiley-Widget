using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Data;

namespace WileyWidget.WinForms.Tests.ServiceTests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void ConfigureServices_forceInMemory_creates_inmemory_db_and_seeder_runs()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var services = DependencyInjection.ConfigureServices(config, forceInMemory: true);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;

            Assert.NotNull(db);
            Assert.True(db!.Database.IsInMemory(), "Expected in-memory provider when forceInMemory true");

            // Ensure demo seeder can populate data
            if (!db.MunicipalAccounts.Any())
            {
                DemoDataSeeder.SeedDemoData(db);
            }

            Assert.True(db.MunicipalAccounts.Any(), "Demo data should have created municipal accounts in the in-memory DB");
        }
    }
}
