using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests;

/// <summary>
/// Base test fixture that provides proper service initialization and database isolation
/// for all tests that need dependency injection or database access.
/// </summary>
public class TestFixtureBase : IDisposable
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly string DatabaseName;
    private bool _disposed = false;

    public TestFixtureBase()
    {
        // Generate unique database name for each test run
        DatabaseName = $"TestDb_{Guid.NewGuid()}";

        // Build configuration
        var configuration = BuildConfiguration();

        // Build service collection
        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging
        services.AddLogging();

        // Add database services with unique in-memory database
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(DatabaseName));

        // Add other services
        services.AddTransient<IEnterpriseRepository, EnterpriseRepository>();
        services.AddTransient<GrokSupercomputer>();
        services.AddTransient<QuickBooksService>();
        services.AddTransient<WpfMiddlewareService>();
        services.AddTransient<GrokDatabaseService>();
        
        // Add SettingsService as singleton (matches its design)
        services.AddTransient<SettingsService>(sp => SettingsService.Instance);

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        // Initialize ServiceLocator for legacy code that still uses it
        ServiceLocator.Initialize(ServiceProvider);

        // Ensure database is created
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
    }

    private IConfiguration BuildConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();

        // Add in-memory configuration for testing
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("xAI:ApiKey", "test-api-key"),
            new KeyValuePair<string, string>("xAI:BaseUrl", "https://api.x.ai/v1/"),
            new KeyValuePair<string, string>("ConnectionStrings:DefaultConnection", $"Data Source={DatabaseName}.db"),
            new KeyValuePair<string, string>("Logging:LogLevel:Default", "Warning")
        });

        return configBuilder.Build();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up database file if it exists
                var dbFile = $"{DatabaseName}.db";
                if (File.Exists(dbFile))
                {
                    try
                    {
                        File.Delete(dbFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors in tests
                    }
                }

                if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test collection that provides service initialization and database isolation
/// </summary>
[CollectionDefinition("Service Test Collection")]
public class ServiceTestCollection : ICollectionFixture<TestFixtureBase>
{
}
