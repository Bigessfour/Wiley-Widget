// CLEAN REPLACEMENT IMPLEMENTATION WITH DIAGNOSTICS
#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Configuration;

namespace WileyWidget.Configuration;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var provider = (configuration["Database:Provider"] ?? "SQLite").Trim();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

            Log.Information("🔧 Configuring DbContext Provider={Provider} ConnLen={Len}", provider, connectionString.Length);
            switch (provider.ToUpperInvariant())
            {
                case "SQLITE":
                    // AZ DATABASE REFERENCE: SQLite for development/testing
                    // - File-based database, no server required
                    // - Cross-platform compatibility
                    // - Suitable for single-user applications
                    // - Connection: "Data Source=WileyWidget.db"
                    options.UseSqlite(connectionString, o => o.CommandTimeout(30));
                    break;
                case "LOCALDB":
                    // AZ DATABASE REFERENCE: SQL Server LocalDB
                    // - Development database, no full SQL Server installation
                    // - Automatic instance management
                    // - Connection: "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;"
                    // - Requires: SqlLocalDB.msi installation
                    // - DEPRECATED: Consider migrating to Azure SQL Database with Managed Identity for production
                    Log.Warning("⚠️ LocalDB is deprecated for production use. Consider migrating to Azure SQL Database with Managed Identity authentication.");
                    options.UseSqlServer(connectionString, o =>
                    {
                        o.CommandTimeout(30);
                        o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(15), null);
                    });
                    break;
                case "SQLSERVER":
                case "MSSQL":
                    // AZ DATABASE REFERENCE: Azure SQL Database / SQL Server
                    // - Production-ready cloud database
                    // - RECOMMENDED: Azure Managed Identity authentication (secure, automatic token rotation)
                    // - Connection: "Server=tcp:{server}.database.windows.net;Database={db};Authentication=Active Directory Managed Identity;"
                    // - Alternative (deprecated): "Server={server};Database={db};User Id={user};Password={pwd};"
                    // - Features: Automatic backups, high availability, scaling

                    // Check if Azure Managed Identity is configured
                    var azureConfig = configuration.GetSection("Azure");
                    var sqlServer = azureConfig["SqlServer"];
                    var database = azureConfig["Database"];

                    if (!string.IsNullOrWhiteSpace(sqlServer) && !string.IsNullOrWhiteSpace(database))
                    {
                        // Use Azure Managed Identity (RECOMMENDED)
                        var azureConnectionString = $"Server=tcp:{sqlServer}.database.windows.net,1433;Database={database};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                        Log.Information("🔐 Using Azure Managed Identity for database authentication (RECOMMENDED)");
                        options.UseSqlServer(azureConnectionString, o =>
                        {
                            o.CommandTimeout(30);
                            o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(15), null);
                        });
                    }
                    else
                    {
                        // Fallback to connection string (may use password auth - DEPRECATED)
                        Log.Warning("⚠️ Azure Managed Identity not configured, falling back to connection string authentication (DEPRECATED - consider migrating to Azure Managed Identity)");
                        options.UseSqlServer(connectionString, o =>
                        {
                            o.CommandTimeout(30);
                            o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(15), null);
                        });
                    }
                    break;
                default:
                    Log.Warning("Unknown database provider '{Provider}', defaulting to SQLite", provider);
                    options.UseSqlite(connectionString, o => o.CommandTimeout(30));
                    break;
            }
#if DEBUG
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
#endif
        });

        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<GrokDatabaseService>();

        services.AddTransient<EnterpriseViewModel>();

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection("Database"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseSettings>, ServiceValidation.DatabaseSettingsValidation>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("Database", tags: new[] { "database" })
            .AddCheck<ExternalApiHealthCheck>("xAI API", tags: new[] { "external", "api" });
        services.AddSingleton<ExternalApiHealthCheck>();
        services.AddSingleton<WpfMiddlewareService>();
        services.AddSingleton<HealthMonitoringService>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // This method has been moved to ServiceCollectionExtensions to avoid conflicts
        // Call the correct method from ServiceCollectionExtensions
        return WileyWidget.Infrastructure.Services.ServiceCollectionExtensions.AddApplicationServices(services, configuration);
    }

    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider rootProvider, CancellationToken cancellationToken = default)
    {
        using var scope = rootProvider.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        if (ShouldSkipInitialization(cfg))
        {
            Log.Warning("🛑 Database initialization skipped (flag)");
            return;
        }

        var totalSw = Stopwatch.StartNew();
        try
        {
            await ProbeConnectionAsync(ctx, TimeSpan.FromSeconds(3), cancellationToken);
            var connStr = ctx.Database.GetConnectionString();
            var isInMemory = connStr?.Contains(":memory:") == true || connStr?.Contains("DataSource=:memory:") == true;
            Log.Information("🔌 DB Init Start Provider={Provider} InMemory={InMem} ConnLen={Len}", ctx.Database.ProviderName, isInMemory, connStr?.Length);
            if (isInMemory)
            {
                await ctx.Database.EnsureCreatedAsync(cancellationToken);
                Log.Information("✅ In-memory database ensured created");
            }
            else
            {
                var migSw = Stopwatch.StartNew();
                await ctx.Database.MigrateAsync(cancellationToken);
                migSw.Stop();
                Log.Information("✅ Migrations applied in {Ms}ms", migSw.ElapsedMilliseconds);
            }
            var seedSw = Stopwatch.StartNew();
            seeder.Seed();
            seedSw.Stop();
            Log.Information("🌱 Seed complete in {Ms}ms", seedSw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("⏰ Database initialization canceled / timed out");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database initialization failed – continuing without blocking UI");
        }
        finally
        {
            totalSw.Stop();
            Log.Information("⏱️ DB Init total {Ms}ms", totalSw.ElapsedMilliseconds);
        }
    }

    private static bool ShouldSkipInitialization(IConfiguration cfg)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("WILEYWIDGET_SKIP_DB") == "1") return true;
            var v = cfg["Database:SkipMigrations"]; if (!string.IsNullOrWhiteSpace(v) && bool.TryParse(v, out var b) && b) return true;
        }
        catch { }
        return false;
    }

    private static async Task ProbeConnectionAsync(AppDbContext ctx, TimeSpan timeout, CancellationToken externalToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            cts.CancelAfter(timeout);
            var token = cts.Token;
            var conn = ctx.Database.GetDbConnection();
            var sw = Stopwatch.StartNew();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(token);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds);
            var result = await cmd.ExecuteScalarAsync(token);
            sw.Stop();
            Log.Information("🔎 DB Probe success ({Result}) in {Ms}ms", result, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("⚠️ DB probe timeout after {Sec}s", timeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ DB probe failed: {Msg}", ex.Message);
        }
    }
}
