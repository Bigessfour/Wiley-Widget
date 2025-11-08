#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace WileyWidget.Data
{
    /// <summary>
    /// Application DbContext factory. Provides IDbContextFactory&lt;AppDbContext&gt; using
    /// configured DbContextOptions with resilience logic to handle early registration issues.
    /// Supports both pre-configured options and on-demand configuration via IConfiguration.
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
        /// Constructor with IConfiguration fallback for early registration scenarios
        /// where DbContextOptions may not be available yet. This enables the factory
        /// to be registered before full DI container initialization.
        /// </summary>
        public AppDbContextFactory(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(BuildOptionsFromConfiguration);
        }

        /// <summary>
        /// Constructor with both options and configuration for maximum resilience.
        /// Prefers options, falls back to configuration if options are invalid.
        /// </summary>
        public AppDbContextFactory(DbContextOptions<AppDbContext>? options, IConfiguration? configuration)
        {
            if (options == null && configuration == null)
            {
                throw new ArgumentException("At least one of options or configuration must be provided.");
            }

            _options = options;
            _configuration = configuration;
            _lazyOptions = new Lazy<DbContextOptions<AppDbContext>>(() =>
                _options ?? BuildOptionsFromConfiguration());
        }

        private DbContextOptions<AppDbContext> BuildOptionsFromConfiguration()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException(
                    "Cannot build DbContextOptions: no configuration available. " +
                    "Ensure IConfiguration is registered before resolving AppDbContextFactory.");
            }

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    Log.Warning("DefaultConnection missing; using fallback SQL Server connection string");
                }

                // Expand environment variables (e.g., %APPDATA%)
                connectionString = Environment.ExpandEnvironmentVariables(connectionString);

                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                    sqlOptions.CommandTimeout(_configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: _configuration.GetValue<int>("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(_configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.EnableSensitiveDataLogging(_configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));

                Log.Information("✓ Built DbContextOptions from IConfiguration (resilient factory mode)");
                return optionsBuilder.Options;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to build DbContextOptions from configuration; using minimal fallback");

                // Ultimate fallback: minimal working configuration
                var fallbackBuilder = new DbContextOptionsBuilder<AppDbContext>();
                fallbackBuilder.UseSqlServer(
                    "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;",
                    sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly("WileyWidget.Data");
                        sqlOptions.EnableRetryOnFailure();
                    });

                return fallbackBuilder.Options;
            }
        }

        public AppDbContext CreateDbContext()
        {
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new AppDbContext(_lazyOptions.Value);
#pragma warning restore CA2000
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new ValueTask<AppDbContext>(new AppDbContext(_lazyOptions.Value));
#pragma warning restore CA2000
        }
    }
}
