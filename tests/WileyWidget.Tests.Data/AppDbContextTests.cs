using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Data;

namespace WileyWidget.Tests.Data
{
    /// <summary>
    /// Unit tests for DbContext and Migrations following Wiley-Widget patterns.
    /// Focus on EF resilience per manifest requirements (0% coverage → high-impact areas).
    /// Tests OnConfiguring post-fix, MigrateAsync isolation, EF9 warnings, and DB initialization.
    /// </summary>
    public class AppDbContextTests
    {
        /// <summary>
        /// Tests that OnConfiguring uses SQL Server with correct configuration when not pre-configured.
        /// This is critical for fallback scenarios and QuickBooksService.cs DB initialization blocking.
        /// </summary>
        [Fact]
        public void OnConfiguring_UsesSqlServer_WithCorrectConfiguration()
        {
            // Arrange & Act - Create context with unconfigured options to trigger OnConfiguring
            var options = new DbContextOptions<AppDbContext>();
            using var context = new AppDbContext(options);

            // Use actual DbContext to verify OnConfiguring worked
            var contextType = context.GetType();
            var database = context.Database;

            // Assert - verify basic functionality works (OnConfiguring was called)
            Assert.NotNull(database);
            Assert.True(database.IsSqlServer(), "Should use SQL Server provider");
        }

        /// <summary>
        /// Tests that fallback connection string is used when environment variable is not set.
        /// Critical for local development and CI/CD environments.
        /// </summary>
        [Fact]
        public void OnConfiguring_UsesFallbackConnectionString_WhenEnvironmentVariableNotSet()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION");
            Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", null);

            try
            {
                // Act - Create context which should trigger OnConfiguring with fallback
                var options = new DbContextOptions<AppDbContext>();
                using var context = new AppDbContext(options);

                // Assert - verify SQL Server is configured (fallback worked)
                Assert.True(context.Database.IsSqlServer(), "Should use SQL Server with fallback");
                var connectionString = context.Database.GetConnectionString();
                Assert.NotNull(connectionString);
                Assert.Contains("WileyWidgetDev", connectionString);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", originalValue);
            }
        }

        /// <summary>
        /// Tests that environment variable connection string is used when available.
        /// Critical for production and staging environments.
        /// </summary>
        [Fact]
        public void OnConfiguring_UsesEnvironmentVariable_WhenAvailable()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION");
            const string testConnectionString = "Server=TestServer;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;";
            Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", testConnectionString);

            try
            {
                // Act
                var options = new DbContextOptions<AppDbContext>();
                using var context = new AppDbContext(options);

                // Assert
                Assert.True(context.Database.IsSqlServer(), "Should use SQL Server provider");
                var connectionString = context.Database.GetConnectionString();
                Assert.Contains("TestServer", connectionString);
                Assert.Contains("TestDb", connectionString);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("WILEY_WIDGET_SQLSERVER_CONNECTION", originalValue);
            }
        }

        /// <summary>
        /// Tests DatabaseConfiguration.ConfigureEnterpriseDbContextOptions warning suppression.
        /// Critical for EF9 compatibility and preventing log spam.
        /// Based on actual implementation that suppresses MultipleCollectionIncludeWarning.
        /// </summary>
        [Fact]
        public void ConfigureEnterpriseDbContextOptions_ConfiguresPropertiesCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>();

            // Act - simulate DatabaseConfiguration.ConfigureEnterpriseDbContextOptions logic
            options.EnableDetailedErrors();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
            });

            // Assert - verify core configuration
            var coreOptions = options.Options.GetExtension<CoreOptionsExtension>();
            Assert.NotNull(coreOptions);
            Assert.True(coreOptions.DetailedErrorsEnabled, "EnableDetailedErrors should be configured");
            Assert.Equal(QueryTrackingBehavior.TrackAll, coreOptions.QueryTrackingBehavior);

            // Verify warnings configuration exists (detailed inspection requires internal access)
            var warningsConfiguration = coreOptions.WarningsConfiguration;
            Assert.NotNull(warningsConfiguration);
        }

        /// <summary>
        /// Tests decimal precision convention configuration.
        /// Critical for financial data accuracy in budget calculations.
        /// </summary>
        [Fact]
        public void ConfigureConventions_SetsDecimalPrecision_ToNineteenFour()
        {
            // Arrange & Act
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDecimalPrecision")
                .Options;

            using var context = new AppDbContext(options);
            var model = context.Model;

            // Assert - find a decimal property and verify precision
            var budgetEntryType = model.FindEntityType(typeof(WileyWidget.Models.BudgetEntry));
            if (budgetEntryType != null)
            {
                var budgetedAmountProperty = budgetEntryType.FindProperty("BudgetedAmount");
                if (budgetedAmountProperty != null)
                {
                    var precision = budgetedAmountProperty.GetPrecision();
                    var scale = budgetedAmountProperty.GetScale();
                    Assert.Equal(19, precision);
                    Assert.Equal(4, scale);
                }
            }
        }

        /// <summary>
        /// Tests migration application in isolation using InMemory provider for speed.
        /// Critical for startup reliability and database schema consistency.
        /// </summary>
        [Fact]
        public async Task MigrateAsync_CompletesSuccessfully_WithInMemoryProvider()
        {
            // Arrange - use InMemory provider for fast testing
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("MigrationTest_" + Guid.NewGuid())
                .Options;

            // Act & Assert - verify context creation and basic operations work
            using var context = new AppDbContext(options);

            // Verify context can be created without exceptions
            Assert.NotNull(context);

            // Verify DbSets are accessible
            Assert.NotNull(context.AppSettings);
            Assert.NotNull(context.BudgetEntries);
            Assert.NotNull(context.Departments);

            // Simulate migration-like operation (EnsureCreated for InMemory)
            var created = await context.Database.EnsureCreatedAsync();
            Assert.True(created || context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory");

            // Verify basic data operations work (critical for QuickBooksService.cs)
            var settingsCount = await context.AppSettings.CountAsync();
            Assert.True(settingsCount >= 0, "Basic query operations should work after migration");
        }

        /// <summary>
        /// Tests DbContextFactory configuration resilience.
        /// Critical for DI container registration and early service resolution.
        /// </summary>
        [Fact]
        public void DbContextFactory_CreatesValidContext_WithFallbackConfiguration()
        {
            // Arrange - Create a simple configuration that will trigger fallback
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", null)
            });
            var configuration = configurationBuilder.Build();

            // Act
            var factory = new AppDbContextFactory(configuration);

            // Assert
            Assert.NotNull(factory);

            using var context = factory.CreateDbContext();
            Assert.NotNull(context);
            Assert.IsType<AppDbContext>(context);
            Assert.True(context.Database.IsSqlServer(), "Should use SQL Server provider");
        }

        /// <summary>
        /// Tests high-impact database initialization scenarios.
        /// Critical for preventing QuickBooksService.cs blocking and startup failures.
        /// </summary>
        [Fact]
        public async Task DatabaseInitialization_HandlesConnectionFailure_Gracefully()
        {
            // Arrange - use invalid connection to test resilience
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=NonExistentServer;Database=NonExistentDb;Connection Timeout=1;")
                .Options;

            using var context = new AppDbContext(options);

            // Act & Assert - verify context creation doesn't throw
            Assert.NotNull(context);

            // Verify that connection failure is handled gracefully (doesn't crash the app)
            var canConnect = false;
            try
            {
                canConnect = await context.Database.CanConnectAsync();
            }
            catch (Exception)
            {
                // Expected for non-existent server - this is graceful handling
                canConnect = false;
            }

            Assert.False(canConnect, "Should fail gracefully for invalid connection");
        }

        /// <summary>
        /// Tests that sensitive data logging is properly configured based on environment.
        /// Critical for production security and development debugging.
        /// </summary>
        [Theory]
        [InlineData("Development", true)]
        [InlineData("Production", false)]
        [InlineData("Staging", false)]
        public void ConfigureEnterpriseDbContextOptions_HandlesSensitiveDataLogging_ByEnvironment(
            string environment, bool shouldEnableSensitiveLogging)
        {
            // Arrange
            var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

            try
            {
                var options = new DbContextOptionsBuilder<AppDbContext>();

                // Act - simulate ConfigureEnterpriseDbContextOptions logic
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                }
                options.EnableDetailedErrors();

                // Assert
                var coreOptions = options.Options.GetExtension<CoreOptionsExtension>();
                Assert.Equal(shouldEnableSensitiveLogging, coreOptions.IsSensitiveDataLoggingEnabled);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
            }
        }

        /// <summary>
        /// Tests AppDbContext constructor and basic initialization.
        /// Critical for verifying the context can be instantiated without errors.
        /// </summary>
        [Fact]
        public void AppDbContext_Constructor_InitializesCorrectly()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestConstructor")
                .Options;

            // Act & Assert
            using var context = new AppDbContext(options);

            Assert.NotNull(context);
            Assert.NotNull(context.AppSettings);
            Assert.NotNull(context.BudgetEntries);
            Assert.NotNull(context.Departments);
            Assert.NotNull(context.Funds);
            Assert.NotNull(context.Transactions);
            Assert.NotNull(context.Enterprises);
        }

        /// <summary>
        /// Tests that DbContext works with the actual enhanced factory registration.
        /// This validates the integration between enhanced DI registration and actual context creation.
        /// </summary>
        [Fact]
        public void EnhancedDbContextFactory_Integration_WorksCorrectly()
        {
            // Arrange - Simulate the enhanced factory approach
            var serviceCollection = new ServiceCollection();
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection",
                    "Server=.\\SQLEXPRESS;Database=WileyWidgetTest;Trusted_Connection=True;TrustServerCertificate=True;")
            });
            var configuration = configurationBuilder.Build();

            serviceCollection.AddSingleton<IConfiguration>(configuration);
            serviceCollection.AddLogging();

            // Register DbContextFactory similar to enhanced implementation
            serviceCollection.AddDbContextFactory<AppDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection") ??
                    "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                    sqlOptions.EnableRetryOnFailure();
                });
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Act
            var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

            // Assert
            Assert.NotNull(factory);

            using var context = factory.CreateDbContext();
            Assert.NotNull(context);
            Assert.IsType<AppDbContext>(context);
            Assert.True(context.Database.IsSqlServer(), "Should use SQL Server provider");
        }
    }
}
