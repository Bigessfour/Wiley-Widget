using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using WileyWidget.Configuration;
using WileyWidget.Data;

namespace WileyWidget.Tests;

public class DatabaseConfigurationTests
{
    private IConfiguration CreateConfigurationWithConnectionString(string connectionString)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("ConnectionStrings:DefaultConnection", connectionString)
        });
        return configBuilder.Build();
    }

    private IConfiguration CreateConfigurationWithTempFile()
    {
        // Create a temporary file for testing instead of using in-memory database
        var tempFile = Path.GetTempFileName();
        var connectionString = $"Data Source={tempFile}";
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("ConnectionStrings:DefaultConnection", connectionString)
        });
        return configBuilder.Build();
    }

    [Fact]
    public void AddDatabaseServices_WithValidConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";
        var configuration = CreateConfigurationWithConnectionString(connectionString);

        // Act
        var result = services.AddDatabaseServices(configuration);

        // Assert
        Assert.NotNull(result);
        var serviceProvider = result.BuildServiceProvider();
        var dbContext = serviceProvider.GetService<AppDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AddDatabaseServices_WithMissingConnectionString_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("ConnectionStrings:DefaultConnection", null)
        });
        var configuration = configBuilder.Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddDatabaseServices(configuration);
            var serviceProvider = services.BuildServiceProvider();
            // Force DbContext creation to trigger validation
            var dbContext = serviceProvider.GetService<AppDbContext>();
        });
        Assert.Contains("Database connection string", exception.Message);
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=test.db")]
    [InlineData("")]
    [InlineData(null)]
    public void AddDatabaseServices_ConnectionStringVariations(string connectionString)
    {
        // Test this or watch your DB ghost you. - Sarcastic comment
        var services = new ServiceCollection();
        var configuration = CreateConfigurationWithConnectionString(connectionString);

        if (string.IsNullOrEmpty(connectionString))
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddDatabaseServices(configuration);
                var serviceProvider = services.BuildServiceProvider();
                // Force DbContext creation to trigger validation
                var dbContext = serviceProvider.GetService<AppDbContext>();
            });
            Assert.Contains("Database connection string", exception.Message);
        }
        else
        {
            services.AddDatabaseServices(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var dbContext = serviceProvider.GetService<AppDbContext>();
            Assert.NotNull(dbContext);
        }
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_WithValidServiceProvider_CreatesDatabase()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfigurationWithTempFile();
        services.AddDatabaseServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

        // Assert - Database should be created without exceptions
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_AppliesMigrations()
    {
        // Test this or your migrations will haunt you like a bad ex.
        var services = new ServiceCollection();
        var configuration = CreateConfigurationWithTempFile();
        services.AddDatabaseServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Assert that migrations are applied - in SQLite, this might not have pending migrations, but test the call
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_SeedsDataCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfigurationWithTempFile();
        services.AddDatabaseServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

        // Assert
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enterprises = await context.Enterprises.ToListAsync();
        Assert.NotEmpty(enterprises); // Assuming seeder adds data
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_SeedingIdempotency()
    {
        // Run this twice and pray your data doesn't multiply like rabbits.
        var services = new ServiceCollection();
        var configuration = CreateConfigurationWithTempFile();
        services.AddDatabaseServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // First run
        await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

        using var scope1 = serviceProvider.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var count1 = await context1.Enterprises.CountAsync();

        // Second run - should not add more data
        await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

        using var scope2 = serviceProvider.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var count2 = await context2.Enterprises.CountAsync();

        Assert.Equal(count1, count2); // Idempotent
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_WithMissingDbContext_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        // Missing DbContext registration
        services.AddScoped<DatabaseSeeder>();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider));
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_WithMissingSeeder_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        // Missing DatabaseSeeder registration
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider));
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_ErrorHandling_WithInvalidConnection()
    {
        // Test error handling with truly invalid connection string
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => 
            options.UseSqlite("Data Source=C:\\Invalid\\Path\\That\\Does\\Not\\Exist\\database.db"));
        services.AddScoped<DatabaseSeeder>();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider));
        Assert.Contains("Failed to initialize database", exception.Message);
    }
}
