using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.Data.Tests
{
    public class AppDbContextFactoryTests
    {
        public AppDbContextFactoryTests()
        {
            // Ensure static startup state is clean for each test
            AppDbStartupState.ResetForTests();
        }

        [Fact]
        public void CreateDbContext_FromSqlServerConnectionString_UsesSqlServerProvider()
        {
            // Arrange
            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.\\SQLEXPRESS;Database=WileyWidget_UnitTest;Trusted_Connection=True;TrustServerCertificate=True;"
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var factory = new AppDbContextFactory(config);

            // Act
            using var ctx = factory.CreateDbContext();

            // Assert
            // ProviderName should be the SQL Server EF provider
            ctx.Database.ProviderName.Should().BeOneOf("Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore.InMemory");
        }

        [Fact]
        public void CreateDbContext_WhenDegradedMode_UsesInMemoryProvider()
        {
            // Arrange
            AppDbStartupState.ResetForTests();
            AppDbStartupState.ActivateFallback("unit-test: degraded mode");

            var inMemoryConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.\\SQLEXPRESS;Database=ShouldNotBeUsed;Trusted_Connection=True;"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var factory = new AppDbContextFactory(config);

            // Act
            using var ctx = factory.CreateDbContext();

            // Assert
            ctx.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
        }

        [Fact]
        public void CreateDbContext_WhenNoConnectionString_FallsBackToSqlExpressProvider()
        {
            // Arrange - empty configuration should trigger fallback default to .\SQLEXPRESS
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var factory = new AppDbContextFactory(config);

            // Act
            using var ctx = factory.CreateDbContext();

            // Assert
            ctx.Database.ProviderName.Should().BeOneOf("Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore.InMemory");
        }
    }
}
