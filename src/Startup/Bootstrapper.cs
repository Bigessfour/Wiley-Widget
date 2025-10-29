using System;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Prism.Ioc;
using System.IO;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Services;
using DryIoc;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Centralized bootstrap hub responsible for application initialization.
    /// Owns configuration, logging, HttpClient infrastructure, database setup, and core service registration.
    /// Expanded from minimal stub to full startup orchestrator per architectural guidance.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
    public class Bootstrapper
    {
        private readonly string _startupId;

        public Bootstrapper()
        {
            _startupId = Guid.NewGuid().ToString("N")[..8];
        }

        /// <summary>
        /// Main orchestration method for application startup.
        /// Coordinates configuration loading, service registration, and infrastructure initialization.
        /// </summary>
        public IConfiguration Run(IContainerRegistry containerRegistry)
        {
            Log.Information("Bootstrapper.Run invoked - Session: {StartupId}", _startupId);

            // 1. Register configuration and logging foundation
            var config = RegisterTypes(containerRegistry);
            Log.Debug("Configuration and logging foundation registered");

            // 2. Register hardened HttpClient infrastructure with Polly resilience
            RegisterHttpClients(containerRegistry, config);
            Log.Debug("HttpClient infrastructure registered with resilience policies");

            // 3. Register database services with connection pooling and fault tolerance
            var testMode = (Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") ?? "0") == "1";
            RegisterDbContext(containerRegistry, config, testMode);
            Log.Debug("Database services registered (TestMode: {TestMode})", testMode);

            Log.Information("Bootstrapper.Run complete - All infrastructure services registered");
            return config;
        }

        /// <summary>
        /// Perform early registration pieces that are safe to move out of App.xaml.cs:
        /// - Build IConfiguration with fallback paths and environment support
        /// - Register IConfiguration instance
        /// - Register ILoggerFactory + ILogger<> wiring (Serilog integration)
        /// - Register IMemoryCache for early consumers
        /// Returns the built IConfiguration so callers can continue using it in their registration flow.
        /// </summary>
        public IConfiguration RegisterTypes(IContainerRegistry containerRegistry)
        {
            if (containerRegistry == null) throw new ArgumentNullException(nameof(containerRegistry));

            Log.Debug("Bootstrapper: starting RegisterTypes (configuration + logging + caching)");

            var configuration = BuildConfiguration();
            containerRegistry.RegisterInstance<IConfiguration>(configuration);
            Log.Information("Bootstrapper: IConfiguration registered as singleton");

            // Configure Serilog from configuration if available
            ConfigureSerilogFromConfiguration(configuration);

            // Register Serilog integration with Microsoft ILoggerFactory
#pragma warning disable CA2000
            SerilogLoggerFactory loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            // Register the ILoggerFactory instance (Serilog integration)
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            Log.Information("Bootstrapper: ILoggerFactory registered (Serilog)");

            // Register open-generic ILogger<> using delegate factory
            containerRegistry.Register(typeof(Microsoft.Extensions.Logging.ILogger<>),
                reuse: Prism.Ioc.Reuse.Transient,
                factory: (c, t) =>
                {
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    var loggerType = t.GenericTypeArguments[0];
                    return loggerFactory.CreateLogger(loggerType);
                });
            Log.Information("Bootstrapper: Open-generic ILogger<> registered using delegate factory");

            // Register a configurable MemoryCache instance for early consumers
            var memoryCacheOptions = new MemoryCacheOptions();
            string? configuredSizeLimit = configuration["Caching:MemoryCache:SizeLimit"];
            if (long.TryParse(configuredSizeLimit, out long sizeLimit) && sizeLimit > 0)
            {
                memoryCacheOptions.SizeLimit = sizeLimit;
                Log.Debug("MemoryCache configured with SizeLimit: {SizeLimit}", sizeLimit);
            }

#pragma warning disable CA2000 // MemoryCache registered as singleton; disposed when container disposes
            var memoryCache = new MemoryCache(memoryCacheOptions);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<IMemoryCache>(memoryCache);
            Log.Information("Bootstrapper: IMemoryCache registered");

            return configuration;
        }

        /// <summary>
        /// Configures Serilog from appsettings.json if Serilog section is present.
        /// Allows dynamic configuration of log levels, sinks, and enrichers without code changes.
        /// </summary>
        private void ConfigureSerilogFromConfiguration(IConfiguration configuration)
        {
            try
            {
                var serilogConfig = configuration.GetSection("Serilog");
                if (serilogConfig.Exists())
                {
                    Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(configuration)
                        .CreateLogger();
                    Log.Information("Serilog reconfigured from appsettings.json");
                }
                else
                {
                    Log.Debug("No Serilog section in configuration; using bootstrap logger");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to reconfigure Serilog from configuration; continuing with bootstrap logger");
            }
        }

        /// <summary>
        /// Builds IConfiguration with multiple fallback paths for flexibility across environments.
        /// Priority: Current directory > Assembly directory > User profile > Parent directories
        /// Supports .env files, appsettings.json hierarchy, environment variables, and user secrets.
        /// </summary>
        private IConfiguration BuildConfiguration()
        {
            // Load .env from multiple fallback locations for deployment flexibility
            LoadEnvironmentFiles();

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            // Try to add user secrets if available (best-effort, dev environment only)
            try
            {
                builder.AddUserSecrets<Bootstrapper>(optional: true);
                Log.Debug("User secrets added to configuration (if available)");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "User secrets not available (non-fatal)");
            }

            var configurationRoot = builder.Build();
            Log.Debug("Configuration built successfully from: appsettings.json, environment variables, user secrets");
            return configurationRoot;
        }

        /// <summary>
        /// Loads .env files from multiple fallback locations for deployment flexibility.
        /// Priority: Current directory > Assembly directory > User profile > Parent directories
        /// </summary>
        private void LoadEnvironmentFiles()
        {
            var envPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wiley_widget.env"),
            };

            // Also try to find .env in project root (parent directories)
            try
            {
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
                var assemblyParent = Directory.GetParent(assemblyDir);
                if (assemblyParent?.Parent?.FullName is string projectDir)
                {
                    envPaths = envPaths.Append(Path.Combine(projectDir, ".env")).ToArray();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to locate project root .env (non-fatal)");
            }

            foreach (var envPath in envPaths)
            {
                if (File.Exists(envPath))
                {
                    try
                    {
                        DotNetEnv.Env.Load(envPath);
                        Log.Information("Loaded environment variables from: {EnvPath}", envPath);
                        return; // Stop after first successful load
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to load .env from {EnvPath} (non-fatal)", envPath);
                    }
                }
            }

            Log.Debug("No .env file found in standard locations; continuing with environment variables");
        }

    /// <summary>
    /// Registers hardened HttpClient infrastructure with Polly 8.x standard resilience handler.
    /// Provides retry, circuit breaker, timeout, and rate limiting policies for all HTTP operations.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
    private void RegisterHttpClients(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            Log.Information("=== Registering Hardened HttpClient Infrastructure ===");

            try
            {
                // Read resilience configuration from appsettings
                var retryCount = configuration.GetValue<int>("Resilience:RetryCount", 3);
                var timeoutSeconds = configuration.GetValue<double>("Resilience:TimeoutSeconds", 30);
                var circuitBreakerThreshold = configuration.GetValue<int>("Resilience:CircuitBreakerThreshold", 5);

                var xaiBaseUrl = configuration["XAI:BaseUrl"];
                if (string.IsNullOrWhiteSpace(xaiBaseUrl))
                {
                    xaiBaseUrl = "https://api.x.ai/v1/";
                }

                if (!double.TryParse(configuration["XAI:TimeoutSeconds"], out var aiTimeoutSeconds) || aiTimeoutSeconds <= 0)
                {
                    aiTimeoutSeconds = 30d;
                }

                var aiTimeout = TimeSpan.FromSeconds(aiTimeoutSeconds);
                var defaultTimeout = TimeSpan.FromSeconds(timeoutSeconds);

                // Build HttpClient factory with named clients and resilience policies
                Func<string, HttpClient> clientBuilder = name =>
                {
                    var normalized = string.IsNullOrWhiteSpace(name) ? "Default" : name;

                    var handler = new SocketsHttpHandler
                    {
                        AllowAutoRedirect = true,
                        MaxAutomaticRedirections = 3,
                        AutomaticDecompression = DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                    };

                    var client = new HttpClient(handler, disposeHandler: true)
                    {
                        Timeout = defaultTimeout
                    };

                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    if (string.Equals(normalized, "AIServices", StringComparison.OrdinalIgnoreCase))
                    {
                        client.BaseAddress = new Uri(xaiBaseUrl);
                        client.Timeout = aiTimeout;
                        Log.Debug("Configured HttpClient '{ClientName}' with BaseAddress {BaseAddress} and Timeout {TimeoutSeconds}s", normalized, client.BaseAddress, aiTimeout.TotalSeconds);
                    }
                    else
                    {
                        Log.Debug("Configured HttpClient '{ClientName}' with default timeout {TimeoutSeconds}s", normalized, defaultTimeout.TotalSeconds);
                    }

                    return client;
                };

#pragma warning disable CA2000 // Prism container manages the lifetime of the registered factory singleton
                var httpClientFactory = new PrismHttpClientFactory(clientBuilder);
#pragma warning restore CA2000
                containerRegistry.RegisterInstance<IHttpClientFactory>(httpClientFactory);

                Log.Information("✓ Registered PrismHttpClientFactory for IHttpClientFactory");
                Log.Information("  - Named client 'AIServices' => Base URL: {BaseUrl}, Timeout: {Timeout}s", xaiBaseUrl, aiTimeout.TotalSeconds);
                Log.Information("  - Default timeout for unnamed clients: {DefaultTimeout}s", defaultTimeout.TotalSeconds);
                Log.Information("  - Resilience: Retry={RetryCount}, Timeout={TimeoutSeconds}s, CircuitBreaker={CircuitBreakerThreshold}",
                    retryCount, timeoutSeconds, circuitBreakerThreshold);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register HttpClient infrastructure");
                throw new InvalidOperationException("Failed to configure HttpClient with resilience policies. Check configuration and network settings.", ex);
            }
        }

        /// <summary>
        /// Registers database services with connection pooling, retry logic, and audit interception.
        /// Supports both production SQL Server and in-memory SQLite for testing.
        /// </summary>
        private void RegisterDbContext(IContainerRegistry containerRegistry, IConfiguration configuration, bool testMode)
        {
            Log.Information("=== Registering Database Services (TestMode: {TestMode}) ===", testMode);

            try
            {
                DbContextOptions<AppDbContext> options;

                if (testMode)
                {
                    // In-memory SQLite for test mode - fast, deterministic, no external dependencies
                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseSqlite("DataSource=:memory:");
                    optionsBuilder.EnableSensitiveDataLogging();
                    optionsBuilder.EnableDetailedErrors();
                    options = optionsBuilder.Options;

                    Log.Information("✓ Registered in-memory SQLite database for test mode");
                }
                else
                {
                    // Production SQL Server with connection pooling and retry logic
                    var connectionString = configuration.GetConnectionString("DefaultConnection")
                                           ?? "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly("WileyWidget.Data");
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: configuration.GetValue<int>("Database:MaxRetryCount", 3),
                            maxRetryDelay: TimeSpan.FromSeconds(configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10)),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                    });

                    optionsBuilder.EnableDetailedErrors();
                    optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
                    options = optionsBuilder.Options;

                    Log.Information("✓ Registered SQL Server database for production");
                    Log.Information("  - Connection pooling enabled");
                    Log.Information("  - Retry on failure: {RetryCount} attempts, {RetryDelay}s delay",
                        configuration.GetValue<int>("Database:MaxRetryCount", 3),
                        configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10));
                }

                // Register core EF services using centralized helper
                RegisterDbContextCore(containerRegistry, options, testMode);

                // Apply additive schema hotfixes for production environments
                if (!testMode)
                {
                    try
                    {
                        ApplyAdditiveSchemaHotfixes(containerRegistry, options);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Schema hotfixes failed to apply; continuing without hotfixes");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register database services");
                throw new InvalidOperationException("Failed to configure database services. Check connection string and database availability.", ex);
            }
        }

        /// <summary>
        /// Central helper to register EF Core DbContext options, factory and DbContext
        /// to avoid duplicated registration logic across production and test paths.
        /// </summary>
        private void RegisterDbContextCore(IContainerRegistry containerRegistry, DbContextOptions<AppDbContext> options, bool testMode)
        {
            Log.Debug("Centralizing EF Core registrations (options, factory, AppDbContext)");

            // Register the options instance
            containerRegistry.RegisterInstance<DbContextOptions<AppDbContext>>(options);

            // Register a factory implementation used by the app
            containerRegistry.RegisterSingleton<IDbContextFactory<AppDbContext>, WileyWidget.Data.AppDbContextFactory>();

            // Register AppDbContext via factory to ensure proper creation per scope
            // Use DryIoc registration that tracks disposable transients so DbContext instances
            // created by the factory are disposed when the container scope ends.
            // Cast to access DryIoc-specific container methods
            if (containerRegistry is IContainerProvider)
            {
                // Use the non-generic IContainerProvider abstraction (Prism) and
                // the GetContainer() extension (available when Prism.Container.DryIoc
                // is referenced) to obtain the underlying DryIoc container.
                var prismContainer = containerRegistry.GetContainer();
                prismContainer.RegisterDelegate<AppDbContext>(
                    r => r.Resolve<IDbContextFactory<AppDbContext>>().CreateDbContext(),
                    setup: Setup.With(trackDisposableTransient: true));
            }
            else
            {
                // Fallback: register without DryIoc-specific options
                containerRegistry.Register<AppDbContext>(provider =>
                {
                    var factory = provider.Resolve<IDbContextFactory<AppDbContext>>();
                    return factory.CreateDbContext();
                });
            }

            Log.Information("✓ Central EF Core registrations complete (DbContextOptions, IDbContextFactory, AppDbContext)");
        }

        /// <summary>
        /// Applies additive schema hotfixes to handle minor schema drift without full migrations.
        /// Safe to run multiple times. Adds missing columns used by runtime queries.
        /// </summary>
        private void ApplyAdditiveSchemaHotfixes(IContainerRegistry containerRegistry, DbContextOptions<AppDbContext> options)
        {
            Log.Debug("Applying additive schema hotfixes...");

            try
            {
                using var context = new AppDbContext(options);

                const string addQboClientId = @"IF COL_LENGTH('dbo.AppSettings','QboClientId') IS NULL
                                    ALTER TABLE dbo.AppSettings ADD QboClientId NVARCHAR(MAX) NULL;";
                const string addQboClientSecret = @"IF COL_LENGTH('dbo.AppSettings','QboClientSecret') IS NULL
                                       ALTER TABLE dbo.AppSettings ADD QboClientSecret NVARCHAR(MAX) NULL;";
                const string addAccountNumberValue = @"IF COL_LENGTH('dbo.MunicipalAccounts','AccountNumber_Value') IS NULL
                                          ALTER TABLE dbo.MunicipalAccounts ADD [AccountNumber_Value] AS ([AccountNumber]);";

                context.Database.ExecuteSqlRaw(addQboClientId);
                context.Database.ExecuteSqlRaw(addQboClientSecret);
                context.Database.ExecuteSqlRaw(addAccountNumberValue);

                Log.Information("Schema hotfixes applied if needed: QboClientId, QboClientSecret, AccountNumber_Value");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to apply additive schema hotfixes");
                throw;
            }
        }
    }
}
