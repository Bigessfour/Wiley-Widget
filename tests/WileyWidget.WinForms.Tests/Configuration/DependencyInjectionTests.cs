using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Tests.Configuration
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void ConfigureServices_Uses_SqlServer_Db_When_ConnectionStringSpecified()
        {
            // Arrange
            var inMemory = new Dictionary<string, string?>
            {
                { "ConnectionStrings:WileyWidgetDb", "Server=(localdb)\\mssqllocaldb;Database=WileyWidget.Test;Trusted_Connection=True;MultipleActiveResultSets=True" }
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

            // Act
            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetService(typeof(WileyWidget.Data.AppDbContext)) as WileyWidget.Data.AppDbContext;

            // Assert
            Assert.NotNull(db);

            var providerName = db.Database.ProviderName ?? string.Empty;
            Assert.Contains("SqlServer", providerName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}