using WileyWidget.ViewModels;
#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WileyWidget.Configuration;

/// <summary>
/// Configuration class for database setup and dependency injection
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Adds database services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with appropriate provider based on configuration
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var databaseProvider = configuration["DatabaseProvider"] ?? 
                                 configuration["Database:Provider"] ?? 
                                 "SQLite";
            
            // Log the connection string and provider for debugging
            Serilog.Log.Information("Database connection string: {ConnectionString}", connectionString);
            Serilog.Log.Information("Database provider: {Provider}", databaseProvider);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' is not configured. " +
                    "Please check your appsettings.json or user secrets.");
            }

            // Configure the appropriate database provider
            switch (databaseProvider.ToUpperInvariant())
            {
                case "SQLITE":
                    options.UseSqlite(connectionString, sqliteOptions =>
                    {
                        // Configure SQLite options
                        sqliteOptions.CommandTimeout(30);
                    });
                    break;
                    
                case "LOCALDB":
                case "SQLSERVER":
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        // Configure SQL Server options
                        sqlOptions.CommandTimeout(30);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
                    break;
                    
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {databaseProvider}. " +
                        "Supported providers: SQLite, LocalDB, SQLServer");
            }

            // Configure logging and other options
            ConfigureDbContextOptions(options);
        });

        // Register repository
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();

        // Register database seeder
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>
    /// Adds all application services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add database services first
        services.AddDatabaseServices(configuration);

        // Add logging
        services.AddLogging(logging =>
        {
            logging.AddSerilog();
        });

        // Add HTTP client for external API calls
        services.AddHttpClient();

        // Register application services
        services.AddScoped<GrokSupercomputer>();
        services.AddScoped<QuickBooksService>();
        services.AddScoped<SettingsService>();

        // Register ViewModels with their dependencies
        services.AddTransient<MainViewModel>();
        services.AddTransient<EnterpriseViewModel>();

        // Add configuration options with validation
        services.AddOptions<XAiSettings>()
            .Bind(configuration.GetSection("xAI"))
            .ValidateOnStart();

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection("Database"))
            .ValidateOnStart();

        // Register custom validators
        services.AddSingleton<IValidateOptions<XAiSettings>, ServiceValidation.XAiSettingsValidation>();
        services.AddSingleton<IValidateOptions<DatabaseSettings>, ServiceValidation.DatabaseSettingsValidation>();

        // Add health checks
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("Database", tags: new[] { "database", "sql" })
            .AddCheck<ExternalApiHealthCheck>("xAI API", tags: new[] { "external", "api" });

        // Register health check services
        services.AddSingleton<ExternalApiHealthCheck>();

        // Register WPF middleware service
        services.AddSingleton<WpfMiddlewareService>();

        // Register health monitoring service
        services.AddSingleton<HealthMonitoringService>();

        // Register Grok database service
        services.AddScoped<GrokDatabaseService>();

        return services;
    }

    /// <summary>
    /// Configures additional DbContext options
    /// </summary>
    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options)
    {
        // Add any additional configuration here
        // This method can be extended for different environments

#if DEBUG
        // Enable detailed errors in development
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
#endif
    }

    /// <summary>
    /// Ensures the database is created and migrated
    /// Call this during application startup
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        try
        {
            // For SQLite in-memory databases, skip migrations to avoid conflicts
            var connectionString = context.Database.GetConnectionString();
            var isInMemory = connectionString?.Contains(":memory:") == true ||
                           connectionString?.Contains("DataSource=:memory:") == true;

            if (isInMemory)
            {
                // For in-memory databases, just ensure created (no migrations)
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                // For file-based databases, use migrations
                await context.Database.MigrateAsync();
            }

            // Seed the database with sample data using the same context
            seeder.Seed();
        }
        catch (Exception ex)
        {
            // Log the error - in a real app, you'd use a proper logging framework
            Console.WriteLine($"Database initialization failed: {ex.Message}");

            // In production, you might want to throw or handle this differently
            throw new InvalidOperationException(
                "Failed to initialize database. Please check your connection string and database permissions.",
                ex);
        }
        finally
        {
            // Dispose the scope after seeding is complete
            scope.Dispose();
        }
    }
}
