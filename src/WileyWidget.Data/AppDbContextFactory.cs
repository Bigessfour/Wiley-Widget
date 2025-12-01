#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WileyWidget.Data
{
    /// <summary>
    /// Application DbContext factory. Provides IDbContextFactory<AppDbContext> using
    /// configured DbContextOptions with resilience logic and a degraded-mode fallback.
    /// </summary>
    public sealed class AppDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext>? _options;
        private readonly IConfiguration? _configuration;
        private readonly Lazy<DbContextOptions<AppDbContext>> _lazyOptions;

        /// <summary>
        /// Constructor for pre-configured options (preferred path).
        /// </summary>
        public AppDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(() => _options);
        }

        /// <summary>
        /// Constructor with IConfiguration fallback for early registration scenarios.
        /// </summary>
        public AppDbContextFactory(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(BuildOptionsFromConfiguration);
        }

        /// <summary>
        /// Constructor with both options and configuration for maximum resilience.
        /// Prefers options, falls back to configuration if options are not provided.
        /// </summary>
        public AppDbContextFactory(DbContextOptions<AppDbContext>? options, IConfiguration? configuration)
        {
            if (options == null && configuration == null)
            {
                throw new ArgumentException("At least one of options or configuration must be provided.");
            }

            _options = options;
            _configuration = configuration;
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(() => _options ?? BuildOptionsFromConfiguration());
        }

        private DbContextOptions<AppDbContext> BuildOptionsFromConfiguration()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Cannot build DbContextOptions without IConfiguration.");
            }

            try
            {
                // Global degraded-mode enforcement
                if (AppDbStartupState.IsDegradedMode)
                {
                    var degradedDbName = _configuration.GetValue<string>("Database:DegradedModeName") ?? "WileyWidget_Degraded";
                    Log.Warning("[DB_FACTORY] Degraded mode active - using InMemory provider (db='{DbName}')", degradedDbName);
                    return new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(degradedDbName)
                        .Options;
                }

                var connectionString = _configuration.GetConnectionString("WileyWidgetDb");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
                    Log.Warning("WileyWidgetDb connection string missing; using fallback SQL Server connection string");
                }

                // Expand environment variables
                connectionString = Environment.ExpandEnvironmentVariables(connectionString);

                var builder = new DbContextOptionsBuilder<AppDbContext>();
                builder.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("WileyWidget.Data");
                    sql.CommandTimeout(_configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                    sql.EnableRetryOnFailure(
                        maxRetryCount: _configuration.GetValue<int>("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(_configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                builder.EnableDetailedErrors();
                builder.EnableSensitiveDataLogging(_configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));

                Log.Information("✓ Built DbContextOptions from IConfiguration");
                return builder.Options;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to build DbContextOptions from configuration");

                // Optional in-memory fallback on configuration failure
                if (_configuration.GetValue<bool>("Database:EnableInMemoryFallback", false))
                {
                    AppDbStartupState.ActivateFallback("Factory configuration failure");
                    var degradedDbName = _configuration.GetValue<string>("Database:DegradedModeName") ?? "WileyWidget_Degraded";
                    Log.Warning("[DB_FACTORY] Activating InMemory fallback after configuration failure (db='{DbName}')", degradedDbName);
                    return new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(degradedDbName)
                        .Options;
                }

                // Minimal SQL Server fallback
                var fallback = new DbContextOptionsBuilder<AppDbContext>();
                fallback.UseSqlServer(
                    "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;",
                    sql =>
                    {
                        sql.MigrationsAssembly("WileyWidget.Data");
                        sql.EnableRetryOnFailure();
                    });
                return fallback.Options;
            }
        }

        public AppDbContext CreateDbContext()
        {
#pragma warning disable CA2000
            return new AppDbContext(_lazyOptions.Value);
#pragma warning restore CA2000
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000
            return new ValueTask<AppDbContext>(new AppDbContext(_lazyOptions.Value));
#pragma warning restore CA2000
        }
    }
}
