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
using Prism.DryIoc;
using Prism.Container.DryIoc;
using System.IO;
using System.Globalization;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Services;
using DryIoc;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Data.SqlClient;

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
            Log.Information("✓ Bootstrapper: Configuration and logging foundation registered");

            // 2. Register hardened HttpClient infrastructure with Polly resilience
            RegisterHttpClients(containerRegistry, config);
            Log.Information("✓ Bootstrapper: HttpClient infrastructure registered with resilience policies");

            // 3. Register database services with connection pooling and fault tolerance
            var testMode = (Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE") ?? "0") == "1";
            Log.Information("Bootstrapper: About to register database services (TestMode: {TestMode})", testMode);
            RegisterDbContext(containerRegistry, config, testMode);
            Log.Information("✓ Bootstrapper: Database services registration complete (TestMode: {TestMode})", testMode);

            Log.Information("✓✓✓ Bootstrapper.Run COMPLETE - All infrastructure services registered ✓✓✓");
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

            // Register open-generic ILogger<> using DryIoc's Made.Of for proper resolution
            // This ensures ILogger<T> can be resolved for any T when injected into services
            // Cast to DryIocContainerExtension to access Instance property for underlying DryIoc container
            if (containerRegistry is Prism.Container.DryIoc.DryIocContainerExtension dryIocExtension)
            {
                var container = dryIocExtension.Instance;
                container.Register(
                    typeof(Microsoft.Extensions.Logging.ILogger<>),
                    made: Made.Of(
                        r => ServiceInfo.Of<ILoggerFactory>(),
                        f => f.CreateLogger(Arg.Index<Type>(0)),
                        r => r.Parent.ImplementationType),
                    reuse: DryIoc.Reuse.Transient);
                Log.Information("Bootstrapper: Open-generic ILogger<> registered using DryIoc Made.Of factory");

                // Verify ILogger<> registration by resolving a test logger
                try
                {
                    var testLogger = container.Resolve<Microsoft.Extensions.Logging.ILogger<Bootstrapper>>();
                    if (testLogger != null)
                    {
                        Log.Debug("✓ ILogger<> registration verified successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "❌ ILogger<> registration verification failed - services may fail to resolve");
                }
            }
            else
            {
                Log.Warning("Container is not DryIocContainerExtension - falling back to basic registration");
                // Fallback: Try delegate-based registration (may have limited compatibility)
                try
                {
                    containerRegistry.Register(typeof(Microsoft.Extensions.Logging.ILogger<>),
                        factory: (c, t) => loggerFactory.CreateLogger(t.GenericTypeArguments[0]));
                    Log.Information("Bootstrapper: Open-generic ILogger<> registered using fallback delegate factory");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "❌ Failed to register ILogger<> - services requiring ILogger<T> will fail");
                }
            }

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

            Log.Information("✓ Bootstrapper.RegisterTypes complete - Configuration, logging, and caching foundation ready");
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
    /// Registers hardened HttpClient infrastructure using .NET standard resilience handler.
    /// Provides retry, circuit breaker, timeouts, and rate limiting via Microsoft.Extensions.Http.Resilience.
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

                // Build Microsoft DI HttpClientFactory with standard resilience handler
                var services = new ServiceCollection();

                // Default client
                services.AddHttpClient("Default", client =>
                {
                    client.Timeout = defaultTimeout;
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 3,
                    AutomaticDecompression = DecompressionMethods.All,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                })
                .AddStandardResilienceHandler(options =>
                {
                    options.TotalRequestTimeout.Timeout = defaultTimeout;
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds / Math.Max(1, retryCount)));
                    options.Retry.MaxRetryAttempts = retryCount;
                    // Keep circuit breaker conservative unless overridden in config
                    options.CircuitBreaker.FailureRatio = 0.2;
                    options.CircuitBreaker.MinimumThroughput = Math.Max(10, circuitBreakerThreshold * 2);
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                });

                // AI services client
                services.AddHttpClient("AIServices", client =>
                {
                    client.BaseAddress = new Uri(xaiBaseUrl);
                    client.Timeout = aiTimeout;
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 3,
                    AutomaticDecompression = DecompressionMethods.All,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
                })
                .AddStandardResilienceHandler(options =>
                {
                    options.TotalRequestTimeout.Timeout = aiTimeout;
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(Math.Max(5, aiTimeoutSeconds / Math.Max(1, retryCount)));
                    options.Retry.MaxRetryAttempts = retryCount;
                    options.CircuitBreaker.FailureRatio = 0.2;
                    options.CircuitBreaker.MinimumThroughput = Math.Max(10, circuitBreakerThreshold * 2);
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                });

                // Import Microsoft DI registrations into DryIoc/Prism
                if (containerRegistry is IContainerExtension extension)
                {
                    extension.Populate(services);
                    Log.Information("✓ Registered IHttpClientFactory via Microsoft DI with standard resilience handler");
                }
                else
                {
                    using var sp = services.BuildServiceProvider();
                    var factory = sp.GetRequiredService<IHttpClientFactory>();
                    containerRegistry.RegisterInstance<IHttpClientFactory>(factory);
                    Log.Information("✓ Registered IHttpClientFactory instance via fallback ServiceProvider (consider using IContainerExtension.Populate)");
                }

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
        /// Configures SQL Server for production scenarios and an in-memory provider for test mode.
        /// </summary>
        private void RegisterDbContext(IContainerRegistry containerRegistry, IConfiguration configuration, bool testMode)
        {
            Log.Information("=== Registering Database Services (TestMode: {TestMode}) ===", testMode);

            try
            {
                DbContextOptions<AppDbContext> options;

                if (testMode)
                {
                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseInMemoryDatabase("WileyWidget_TestDb");
                    optionsBuilder.EnableSensitiveDataLogging();
                    optionsBuilder.EnableDetailedErrors();
                    options = optionsBuilder.Options;

                    Log.Information("✓ Registered in-memory database for test mode");
                }
                else
                {
                    var connectionString = configuration.GetConnectionString("DefaultConnection");

                    if (!string.IsNullOrWhiteSpace(connectionString))
                    {
                        connectionString = Environment.ExpandEnvironmentVariables(connectionString);
                    }

                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                        Log.Warning("DefaultConnection missing; using fallback SQL Server connection string to .\\SQLEXPRESS");
                    }

                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly("WileyWidget.Data");
                        sqlOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: configuration.GetValue<int>("Database:MaxRetryCount", 3),
                            maxRetryDelay: TimeSpan.FromSeconds(configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10)),
                            errorNumbersToAdd: null);
                    });

                    optionsBuilder.EnableDetailedErrors();
                    optionsBuilder.EnableSensitiveDataLogging(configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));
                    optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
                    options = optionsBuilder.Options;

                    Log.Information("✓ Registered SQL Server database for production");
                    Log.Information("  - Connection string: {ConnectionString}", MaskSensitiveConnectionString(connectionString));
                }

                // Register core EF services using centralized helper
                RegisterDbContextCore(containerRegistry, options, testMode);

                // Defer database initialization to a background initializer to keep UI responsive
                if (!testMode)
                {
                    try
                    {
                        containerRegistry.RegisterSingleton<WileyWidget.Startup.DatabaseInitializer>();
                        Log.Information("✓ Registered DatabaseInitializer for background database migration");
                    }
                    catch (Exception regEx)
                    {
                        Log.Warning(regEx, "Failed to register DatabaseInitializer (non-fatal)");
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
        /// Initializes the database: ensures it's created, applies migrations, and runs hotfixes.
        /// </summary>
        private void InitializeDatabase(IContainerRegistry containerRegistry, DbContextOptions<AppDbContext> options, IConfiguration configuration)
        {
            Log.Information("Initializing database...");

            try
            {
                using var context = new AppDbContext(options);

                // Backup database if configured
                var backupOnStartup = configuration.GetValue<bool>("Database:BackupOnStartup", false);
                if (backupOnStartup)
                {
                    try
                    {
                        BackupDatabase(configuration);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Database backup failed (non-fatal)");
                    }
                }

                // Auto-migrate if configured
                var autoMigrate = configuration.GetValue<bool>("Database:AutoMigrate", true);
                if (autoMigrate)
                {
                    try
                    {
                        Log.Debug("Applying pending migrations...");
                        context.Database.Migrate();
                        Log.Information("✓ Database migrations applied successfully");
                    }
                    catch (Exception ex)
                    {
                        // If migrations are unavailable (e.g., first run), fall back to EnsureCreated
                        Log.Debug(ex, "Migration failed, attempting EnsureCreated instead");
                        context.Database.EnsureCreated();
                        Log.Information("✓ Database created successfully");
                    }
                }
                else
                {
                    // Just ensure the database exists
                    context.Database.EnsureCreated();
                    Log.Information("✓ Database existence verified");
                }

                // Test connection
                var canConnect = context.Database.CanConnect();
                if (canConnect)
                {
                    Log.Information("✓ Database connection verified");
                }
                else
                {
                    Log.Warning("Database connection test failed");
                }

                // Apply schema hotfixes
                ApplyAdditiveSchemaHotfixes(options);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Creates a backup of the SQL Server database if a backup directory is configured.
        /// </summary>
        private void BackupDatabase(IConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return;

                connectionString = Environment.ExpandEnvironmentVariables(connectionString);
                var backupDirectory = configuration.GetValue<string>("Database:BackupDirectory");

                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    Log.Debug("Backup directory not configured; skipping SQL Server backup");
                    return;
                }

                Directory.CreateDirectory(backupDirectory);

                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var databaseName = connection.Database;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var backupPath = Path.Combine(backupDirectory, $"{databaseName}_backup_{timestamp}.bak");

                var commandText = $"BACKUP DATABASE [{databaseName}] TO DISK = @backupPath WITH INIT, CHECKSUM";
                using var command = new SqlCommand(commandText, connection)
                {
                    CommandTimeout = configuration.GetValue<int>("Database:BackupCommandTimeoutSeconds", 600)
                };

                command.Parameters.AddWithValue("@backupPath", backupPath);
                command.ExecuteNonQuery();

                Log.Information("✓ Database backed up to: {BackupPath}", backupPath);

                var retentionDays = configuration.GetValue<int>("Database:BackupRetentionDays", 30);
                CleanOldBackups(backupDirectory, retentionDays, databaseName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to backup database");
            }
        }

        /// <summary>
        /// Removes backup files older than the specified retention period.
        /// </summary>
        private void CleanOldBackups(string backupDir, int retentionDays, string databaseName)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var searchPattern = $"{databaseName}_backup_*.bak";
                var backupFiles = Directory.GetFiles(backupDir, searchPattern);

                var deletedCount = 0;
                foreach (var file in backupFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                if (deletedCount > 0)
                {
                    Log.Information("✓ Cleaned {Count} old backup(s) (older than {Days} days)", deletedCount, retentionDays);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to clean old backups");
            }
        }

        /// <summary>
        /// Central helper to register EF Core DbContext options, factory and DbContext
        /// to avoid duplicated registration logic across production and test paths.
        /// Uses AddDbContextFactory for proper Microsoft.Extensions.DependencyInjection integration.
        /// </summary>
        private void RegisterDbContextCore(IContainerRegistry containerRegistry, DbContextOptions<AppDbContext> options, bool testMode)
        {
            Log.Debug("Centralizing EF Core registrations (options, factory, AppDbContext)");

            // Capture options for downstream consumers (DatabaseInitializer, context factory, migrations)
            containerRegistry.RegisterInstance(options);

            // Use AddDbContextFactory for proper registration through Microsoft.Extensions.DependencyInjection
            // This avoids warnings about early registrations and ensures proper factory lifecycle
            var services = new ServiceCollection();

            // Register IConfiguration if available for factory resilience
            if (containerRegistry.IsRegistered<IConfiguration>())
            {
                try
                {
                    var config = containerRegistry.Resolve<IConfiguration>();
                    services.AddSingleton(config);
                    Log.Debug("Registered IConfiguration in ServiceCollection for DbContextFactory resilience");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not resolve IConfiguration for ServiceCollection (non-fatal)");
                }
            }

            // Register the factory using AddDbContextFactory - this is the proper way per EF Core docs
            services.AddDbContextFactory<AppDbContext>(optionsBuilder =>
            {
                // Transfer the pre-configured options to the factory builder
                // This preserves all the SQL Server configuration including resilience policies
                if (options.Extensions.Any())
                {
                    foreach (var extension in options.Extensions)
                    {
                        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
                    }
                }
            }, ServiceLifetime.Singleton);

            // Bridge the Microsoft.Extensions.DependencyInjection registrations into DryIoc
            try
            {
                var dryContainer = containerRegistry.GetContainer();

                // Import service collection into DryIoc
                dryContainer.Populate(services);

                Log.Information("✓ Registered IDbContextFactory<AppDbContext> via AddDbContextFactory (proper EF Core integration)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to populate ServiceCollection into DryIoc; falling back to direct factory registration");

                // Fallback: Register factory instance directly
                var factory = new WileyWidget.Data.AppDbContextFactory(options);
                containerRegistry.RegisterInstance<IDbContextFactory<AppDbContext>>(factory);
                Log.Information("✓ Registered IDbContextFactory<AppDbContext> via direct instance (fallback)");
            }

            try
            {
                // Use DryIoc delegate registration so DbContext instances created via Prism resolve
                // respect disposal semantics. Track disposable transients to avoid DryIoc warnings while
                // still flowing factory-created contexts to consumers that request AppDbContext directly.
                var dryContainer = containerRegistry.GetContainer();
                dryContainer.RegisterDelegate<AppDbContext>(
                    r => r.Resolve<IDbContextFactory<AppDbContext>>().CreateDbContext(),
                    reuse: testMode ? Reuse.Singleton : Reuse.Transient,
                    setup: Setup.With(trackDisposableTransient: true, allowDisposableTransient: true));
            }
            catch (Exception ex)
            {
                // Non-fatal: applications can still resolve IDbContextFactory directly if delegate registration fails
                Log.Warning(ex, "Unable to register AppDbContext delegate with DryIoc; IDbContextFactory<AppDbContext> remains available");
            }

            Log.Information("✓ Central EF Core registrations complete (DbContextOptions, IDbContextFactory, AppDbContext)");
        }

        /// <summary>
        /// Applies additive schema hotfixes to handle minor schema drift without full migrations.
        /// Safe to run multiple times. Adds missing columns used by runtime queries.
        /// SQL Server implementation.
        /// </summary>
    private void ApplyAdditiveSchemaHotfixes(DbContextOptions<AppDbContext> options)
        {
            Log.Debug("Applying additive schema hotfixes for SQL Server...");

            try
            {
                using var context = new AppDbContext(options);

                bool ColumnExists(string tableName, string columnName)
                {
                    var sql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";
                    var connection = context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    using var command = connection.CreateCommand();
                    command.CommandText = sql;

                    var tableParam = command.CreateParameter();
                    tableParam.ParameterName = "@table";
                    tableParam.Value = tableName;
                    command.Parameters.Add(tableParam);

                    var columnParam = command.CreateParameter();
                    columnParam.ParameterName = "@column";
                    columnParam.Value = columnName;
                    command.Parameters.Add(columnParam);

                    var result = command.ExecuteScalar();
                    return Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
                }

                // Add QboClientId if not exists
                if (!ColumnExists("AppSettings", "QboClientId"))
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw("ALTER TABLE AppSettings ADD QboClientId NVARCHAR(450) NULL");
                        Log.Debug("Added QboClientId column to AppSettings");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "QboClientId column may already exist");
                    }
                }

                // Add QboClientSecret if not exists
                if (!ColumnExists("AppSettings", "QboClientSecret"))
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw("ALTER TABLE AppSettings ADD QboClientSecret NVARCHAR(450) NULL");
                        Log.Debug("Added QboClientSecret column to AppSettings");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "QboClientSecret column may already exist");
                    }
                }

                Log.Information("✓ SQL Server schema hotfixes applied successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply additive schema hotfixes (non-fatal, continuing)");
            }
        }

        /// <summary>
        /// Masks sensitive information in connection strings for logging purposes.
        /// </summary>
        private static string MaskSensitiveConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return "[empty]";

            // Mask sensitive information such as passwords or access tokens before logging
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var maskedParts = new System.Text.StringBuilder();

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (trimmedPart.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                    trimmedPart.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
                {
                    maskedParts.Append("Password=****");
                }
                else
                {
                    maskedParts.Append(trimmedPart);
                }
                maskedParts.Append("; ");
            }

            return maskedParts.ToString().TrimEnd(';', ' ');
        }
    }
}
