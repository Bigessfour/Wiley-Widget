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

        // Get connection string and provider
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=WileyWidget.db";
        var databaseProvider = configuration["DatabaseProvider"] ??
                             configuration["Database:Provider"] ??
                             "SQLite";

        // Configure DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Configure the appropriate database provider
        switch (databaseProvider.ToUpperInvariant())
        {
            case "SQLITE":
                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                });
                break;

            case "LOCALDB":
            case "SQLSERVER":
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
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

        // Enable sensitive data logging in development
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();

        return new AppDbContext(optionsBuilder.Options);
    }
}
