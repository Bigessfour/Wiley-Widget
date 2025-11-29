using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Tests.Data
{
    public class AppDbContextStartupTests
    {
        [Fact]
        public void ConfigureServices_Uses_InMemory_Db_When_NoConnectionString()
        {
            // Arrange: no connection string
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetService(typeof(WileyWidget.Data.AppDbContext)) as WileyWidget.Data.AppDbContext;

            Assert.NotNull(db);

            // Verify provider name contains InMemory
            var providerName = db.Database.ProviderName ?? string.Empty;
            Assert.Contains("InMemory", providerName);
        }
    }
}
