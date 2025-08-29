#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // Register DbContext with SQL Server
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' is not configured. " +
                    "Please check your appsettings.json or user secrets.");
            }

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // Configure SQL Server options
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                sqlOptions.CommandTimeout(30);

                // Enable query splitting for better performance with includes
                sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            // Configure logging and other options
            ConfigureDbContextOptions(options);
        });

        // Register repository
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();

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

        try
        {
            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            await context.Database.MigrateAsync();
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
