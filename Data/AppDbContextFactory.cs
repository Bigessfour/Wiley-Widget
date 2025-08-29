#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace WileyWidget.Data;

/// <summary>
/// Design-time factory for AppDbContext to support EF Core migrations and tools
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates a new instance of AppDbContext for design-time operations
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback to SQLite for development
            connectionString = "Data Source=WileyWidget.db";
        }

        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.CommandTimeout(30);
            sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        // Enable sensitive data logging in development
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();

        return new AppDbContext(optionsBuilder.Options);
    }
}
