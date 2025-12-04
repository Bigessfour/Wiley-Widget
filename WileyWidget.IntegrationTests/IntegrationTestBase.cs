using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.IntegrationTests.TestDoubles;
using Serilog;
using System.Globalization;

namespace WileyWidget.IntegrationTests;

/// <summary>
/// Base class for integration tests with minimal service container setup.
/// Focuses on testability over full production parity.
/// Use this for testing individual services or small service graphs.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected IServiceProvider ServiceProvider { get; }
    protected IServiceScope Scope { get; }

    protected IntegrationTestBase()
    {
        // Configure Serilog for testing (console output)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Configure logging with Serilog
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Configure EF Core with In-Memory database for testing
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase($"WileyWidgetTest_{Guid.NewGuid()}");
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }, ServiceLifetime.Scoped);

        // Register test doubles for expensive external services
        services.AddSingleton<IAIService, TestDoubles.NullAIServiceDouble>();
        services.AddSingleton<IGrokSupercomputer, TestDoubles.NullGrokSupercomputerDouble>();
        services.AddSingleton<ITelemetryService, NullTelemetryService>();

        // Allow derived classes to add additional services
        ConfigureServices(services, configuration);

        ServiceProvider = services.BuildServiceProvider();
        Scope = ServiceProvider.CreateScope();

        // Initialize database schema
        InitializeDatabase();
    }

    /// <summary>
    /// Override this in derived test classes to register additional services
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Default: no additional services
        // Derived classes can override to add service-specific dependencies
    }

    /// <summary>
    /// Initialize the in-memory database schema
    /// </summary>
    protected virtual void InitializeDatabase()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);
        dbContext.Database.EnsureCreated();
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Scope?.Dispose();
            (ServiceProvider as IDisposable)?.Dispose();
            Log.CloseAndFlush();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected T GetService<T>() where T : notnull
        => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<T>(Scope.ServiceProvider);

    protected T? GetOptionalService<T>() where T : class
        => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<T>(Scope.ServiceProvider);

    /// <summary>
    /// Get the test database context
    /// </summary>
    protected AppDbContext GetDbContext()
        => GetService<AppDbContext>();
}
