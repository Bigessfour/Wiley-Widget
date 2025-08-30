#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.Data;

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
        // Register DbContext with SQLite
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            
            // Log the connection string for debugging
            Serilog.Log.Information("Database connection string: {ConnectionString}", connectionString);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' is not configured. " +
                    "Please check your appsettings.json or user secrets.");
            }

            options.UseSqlite(connectionString, sqliteOptions =>
            {
                // Configure SQLite options
                sqliteOptions.CommandTimeout(30);
            });

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
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        try
        {
            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            await context.Database.MigrateAsync();

            // Seed the database with sample data
            await seeder.SeedAsync();
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
    }
}
