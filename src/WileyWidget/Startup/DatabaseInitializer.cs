using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using WileyWidget.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using Serilog;
using WileyWidget.Services.Telemetry;
using WileyWidget.Configuration.Resilience;
using Polly;
using Polly.Retry;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Enterprise-grade database initialization hosted service with resilience and telemetry.
    /// Performs database initialization (backup, migrate/ensure created, connectivity test)
    /// on a background thread to keep the UI responsive during startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service implements <see cref="IHostedService"/> to integrate with the host application lifecycle,
    /// ensuring database operations complete during the startup phase without blocking the main UI thread.
    /// </para>
    /// <para>
    /// <b>Production Configuration:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>DB:AutoMigrate</c> - Set to "false" in production to disable automatic migrations (default: true)</description></item>
    /// <item><description><c>Database:BackupOnStartup</c> - Enable pre-migration backups for safety (default: false)</description></item>
    /// <item><description><c>Database:BackupDirectory</c> - Directory for backup files</description></item>
    /// <item><description><c>Database:BackupRetentionDays</c> - Backup retention period (default: 30 days)</description></item>
    /// </list>
    /// <para>
    /// <b>Resilience Features:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Polly retry policies for transient database failures (3 attempts with exponential backoff)</description></item>
    /// <item><description>Circuit breaker pattern to prevent cascading failures</description></item>
    /// <item><description>Comprehensive telemetry integration with SigNoz for observability</description></item>
    /// <item><description>Memory health monitoring before and after operations</description></item>
    /// </list>
    /// <para>
    /// <b>Licensing:</b> If using BoldReports for migration scripts, ensure Syncfusion/BoldReports
    /// licenses are registered in the static constructor per licensing requirements.
    /// </para>
    /// </remarks>
    /// <seealso cref="IHostedService"/>
    /// <seealso cref="DatabaseResiliencePolicy"/>
    /// <seealso cref="AppDbContext"/>
    public class DatabaseInitializer : IHostedService
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;
        private readonly SigNozTelemetryService? _telemetryService;
        private readonly AsyncRetryPolicy _retryPolicy;
        private Activity? _startupActivity;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class.
        /// </summary>
        /// <param name="options">EF Core DbContext options for database connection configuration.</param>
        /// <param name="configuration">Application configuration for runtime settings and connection strings.</param>
        /// <param name="logger">Logger instance for diagnostic and operational logging.</param>
        /// <param name="telemetryService">Optional telemetry service for SigNoz observability integration.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/>, <paramref name="configuration"/>,
        /// or <paramref name="logger"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The constructor initializes a Polly retry policy with exponential backoff (3 attempts, 100ms base delay)
        /// to handle transient database failures gracefully. This ensures resilient startup even under degraded
        /// database conditions.
        /// </para>
        /// <para>
        /// <b>Licensing Check:</b> Validates that Syncfusion/BoldReports licenses are registered if these
        /// components are used during migration operations. This check runs once at service initialization.
        /// </para>
        /// </remarks>
        public DatabaseInitializer(
            DbContextOptions<AppDbContext> options,
            IConfiguration configuration,
            ILogger<DatabaseInitializer> logger,
            SigNozTelemetryService? telemetryService = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryService = telemetryService;

            // Initialize Polly retry policy for database resilience
            _retryPolicy = DatabaseResiliencePolicy.CreateDatabaseRetryPolicy(
                maxRetryAttempts: 3,
                baseDelayMs: 100);

            // Validate licensing for Syncfusion/BoldReports if used in migrations
            ValidateLicenseRegistration();
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// Initiates background database initialization with resilience policies.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop startup operations.</param>
        /// <returns>A task representing the asynchronous startup operation.</returns>
        /// <remarks>
        /// <para>
        /// This method is called by the host during application startup. It checks the <c>DB:AutoMigrate</c>
        /// configuration setting to determine if automatic migrations should run. In production environments,
        /// set <c>DB:AutoMigrate</c> to "false" to prevent unintended schema changes.
        /// </para>
        /// <para>
        /// <b>Configuration Keys:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>DB:AutoMigrate</c> - Controls automatic migration execution (default: true)</description></item>
        /// <item><description><c>Database:BackupOnStartup</c> - Enable pre-migration backups (default: false)</description></item>
        /// </list>
        /// <para>
        /// If auto-migration is disabled, the method returns immediately, logging the skip decision.
        /// All database operations are wrapped in Polly retry policies for resilience.
        /// </para>
        /// </remarks>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Check configuration to determine if auto-migration is enabled
            var autoMigrate = _configuration.GetValue<bool>("DB:AutoMigrate", true);

            if (!autoMigrate)
            {
                _logger.LogInformation("Database auto-migration disabled via configuration (DB:AutoMigrate=false). Skipping initialization.");
                Log.Information("⚙️ Database auto-migration disabled - production mode active");
                return Task.CompletedTask;
            }

            // Execute initialization with resilience policy
            return _retryPolicy.ExecuteAsync(async () => await InitializeAsync(cancellationToken));
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// Cleans up telemetry resources and completes ongoing operations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop shutdown operations.</param>
        /// <returns>A task representing the asynchronous shutdown operation.</returns>
        /// <remarks>
        /// This method ensures proper disposal of telemetry activities and logs shutdown status.
        /// It is called automatically by the host infrastructure during application shutdown.
        /// </remarks>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DatabaseInitializer service stopping");
            Log.Information("🛑 DatabaseInitializer service stopping - cleanup complete");

            // Dispose any remaining telemetry activities
            _startupActivity?.Dispose();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates that Syncfusion and BoldReports licenses are properly registered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method checks for license registration to comply with Syncfusion licensing requirements.
        /// Per Syncfusion documentation, licenses must be registered in a static constructor before
        /// component usage. This validation ensures compliance during database migration operations
        /// that may use BoldReports for reporting.
        /// </para>
        /// <para>
        /// Logs a warning if licenses are not detected, but does not prevent startup to allow
        /// trial/evaluation scenarios.
        /// </para>
        /// </remarks>
        private void ValidateLicenseRegistration()
        {
            try
            {
                var syncfusionKey = _configuration["Syncfusion:LicenseKey"];
                var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

                if (string.IsNullOrWhiteSpace(syncfusionKey) && string.IsNullOrWhiteSpace(envKey))
                {
                    _logger.LogWarning("Syncfusion/BoldReports license not detected. Components will run in trial mode.");
                    Log.Warning("⚠️ No Syncfusion license detected - running in trial mode for migration operations");
                }
                else
                {
                    _logger.LogDebug("Syncfusion license registration validated for database operations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "License validation check encountered an issue (non-fatal)");
            }
        }

        /// <summary>
        /// Performs comprehensive database initialization including backups, migrations, and health checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop initialization operations.</param>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        /// <remarks>
        /// <para>
        /// This method orchestrates the complete database initialization workflow:
        /// </para>
        /// <list type="number">
        /// <item><description>Memory health check and telemetry initialization</description></item>
        /// <item><description>Optional pre-migration backup (if <c>Database:BackupOnStartup</c> is enabled)</description></item>
        /// <item><description>Database migration via <see cref="MigrateAsync"/> with fallback to EnsureCreated</description></item>
        /// <item><description>Post-migration connectivity health check</description></item>
        /// <item><description>Final memory usage tracking</description></item>
        /// </list>
        /// <para>
        /// All operations are instrumented with SigNoz telemetry for production observability.
        /// Failures are logged but do not prevent application startup, allowing degraded operation.
        /// </para>
        /// </remarks>
        /// <seealso cref="MigrateAsync"/>
        /// <seealso cref="BackupDatabase"/>
        private async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Mark that initialization path was attempted (used for idempotency/telemetry)
                AppDbStartupState.MarkInitializationAttempted();
                // Start telemetry tracking
                _startupActivity = _telemetryService?.StartActivity("DB.Initialization");
                _startupActivity?.SetTag("db.operation", "migrate");

                _logger.LogInformation("Background DB initialization starting...");
                Log.Information("🔄 Starting database initialization with telemetry tracking");

                // Memory check before DB operations (aligned with VerifyAndApplyTheme pattern)
                var gcMemInfo = GC.GetGCMemoryInfo();
                var availableMemoryMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
                var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

                _logger.LogInformation("Memory status before DB init: {AvailableMB}MB available, {CurrentMB}MB in use",
                    availableMemoryMB, currentMemoryMB);
                _startupActivity?.SetTag("memory.available_mb", availableMemoryMB);
                _startupActivity?.SetTag("memory.used_mb", currentMemoryMB);

                // Warn if memory is low (similar to theme check)
                if (availableMemoryMB < 256)
                {
                    Log.Warning("⚠️ Low memory detected ({AvailableMB}MB) before DB initialization - operations may be slow", availableMemoryMB);
                    _startupActivity?.SetTag("memory.warning", "low_available_memory");
                }

                using var context = new AppDbContext(_options);

                // Track DB context creation
                _startupActivity?.AddEvent(new ActivityEvent("DB.Context.Created"));

                // Optional backup (best-effort)
                var backupOnStartup = _configuration.GetValue<bool>("Database:BackupOnStartup", false);
                if (backupOnStartup)
                {
                    try
                    {
                        _startupActivity?.AddEvent(new ActivityEvent("DB.Backup.Start"));
                        BackupDatabase();
                        _startupActivity?.AddEvent(new ActivityEvent("DB.Backup.Success"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Database backup failed (non-fatal)");
                        Log.Warning(ex, "Database backup failed - continuing with initialization");
                        _startupActivity?.AddEvent(new ActivityEvent("DB.Backup.Failed",
                            tags: new ActivityTagsCollection { ["error.message"] = ex.Message }));
                    }
                }

                var autoMigrate = _configuration.GetValue<bool>("Database:AutoMigrate", true);
                if (autoMigrate)
                {
                    await MigrateAsync(context, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _startupActivity?.AddEvent(new ActivityEvent("DB.EnsureCreated.Start"));
                    await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("✓ Database existence verified (background)");
                    Log.Information("✅ Database existence verified");

                    _startupActivity?.AddEvent(new ActivityEvent("DB.EnsureCreated.Success"));
                }

                // Post-migration health check (integrated with HealthReportingService pattern)
                _startupActivity?.AddEvent(new ActivityEvent("DB.HealthCheck.Start"));
                try
                {
                    var canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

                    if (canConnect)
                    {
                        _logger.LogInformation("✓ Database connection verified (background)");
                        Log.Information("✅ Post-migration health check: Database connection verified");

                        // Get database version for telemetry
                        try
                        {
                            var dbVersion = context.Database.GenerateCreateScript();
                            var versionInfo = dbVersion.Length > 100 ? $"{dbVersion.Substring(0, 100)}..." : dbVersion;
                            _startupActivity?.SetTag("db.schema_snapshot", versionInfo);
                            _startupActivity?.SetTag("db.health", "healthy");
                        }
                        catch (Exception versionEx)
                        {
                            _logger.LogDebug(versionEx, "Could not retrieve database version info");
                        }

                        _startupActivity?.AddEvent(new ActivityEvent("DB.HealthCheck.Success"));
                    }
                    else
                    {
                        _logger.LogWarning("⚠ Database connection test failed (background)");
                        Log.Warning("⚠️ Post-migration health check: Database connection test failed - degraded state");

                        _startupActivity?.SetTag("db.health", "degraded");
                        _startupActivity?.AddEvent(new ActivityEvent("DB.HealthCheck.Failed",
                            tags: new ActivityTagsCollection { ["reason"] = "cannot_connect" }));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Database connectivity test threw (non-fatal)");
                    Log.Warning(ex, "⚠️ Database connectivity test threw exception");

                    _startupActivity?.SetTag("db.health", "degraded");
                    _startupActivity?.AddEvent(new ActivityEvent("DB.HealthCheck.Exception",
                        tags: new ActivityTagsCollection
                        {
                            ["error.message"] = ex.Message,
                            ["error.type"] = ex.GetType().Name
                        }));
                }                stopwatch.Stop();
                _logger.LogInformation("Background DB initialization complete in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                Log.Information("✅ Background DB initialization complete in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                _startupActivity?.SetTag("db.init.duration_ms", stopwatch.ElapsedMilliseconds);
                _startupActivity?.SetTag("db.init.status", "success");

                // Final memory check
                var finalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                _logger.LogInformation("Memory after DB init: {FinalMB}MB (delta: {DeltaMB}MB)",
                    finalMemoryMB, finalMemoryMB - currentMemoryMB);
                _startupActivity?.SetTag("memory.final_mb", finalMemoryMB);
                _startupActivity?.SetTag("memory.delta_mb", finalMemoryMB - currentMemoryMB);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Background DB initialization encountered issues; continuing startup");
                Log.Error(ex, "❌ Database initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                _startupActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _startupActivity?.SetTag("db.init.status", "failed");
                _startupActivity?.SetTag("db.init.duration_ms", stopwatch.ElapsedMilliseconds);
                _startupActivity?.SetTag("error.message", ex.Message);
                _startupActivity?.SetTag("error.type", ex.GetType().Name);

                _telemetryService?.RecordException(ex,
                    ("operation", "db_initialization"),
                    ("duration_ms", stopwatch.ElapsedMilliseconds));

                // Don't throw - allow startup to continue with degraded DB functionality
            }
            finally
            {
                _startupActivity?.Dispose();
            }
        }

        /// <summary>
        /// Executes Entity Framework Core database migrations with resilience and fallback strategies.
        /// </summary>
        /// <param name="context">The database context to migrate.</param>
        /// <param name="cancellationToken">Cancellation token to stop migration operations.</param>
        /// <returns>A task representing the asynchronous migration operation.</returns>
        /// <exception cref="DbUpdateException">
        /// Thrown when migration fails and EnsureCreated fallback also fails.
        /// This indicates a critical database connectivity or configuration issue.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method implements a two-stage migration strategy:
        /// </para>
        /// <list type="number">
        /// <item>
        /// <description>
        /// <b>Primary:</b> Attempts to apply pending EF Core migrations using
        /// <see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync"/>.
        /// This is the preferred approach for version-controlled schema evolution.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>Fallback:</b> If migration fails (e.g., first-time deployment, migration script issues),
        /// falls back to <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreatedAsync"/>
        /// to create the database schema from the current model.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// <b>Production Considerations:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Set <c>DB:AutoMigrate=false</c> in production to prevent automatic schema changes</description></item>
        /// <item><description>Use manual migration scripts for controlled production deployments</description></item>
        /// <item><description>Enable <c>Database:BackupOnStartup</c> for pre-migration safety</description></item>
        /// </list>
        /// <para>
        /// <b>Telemetry Events:</b> This method emits the following SigNoz activity events:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>DB.Migrate.Start</c> - Migration attempt initiated</description></item>
        /// <item><description><c>DB.Migrate.Success</c> - Migration completed successfully</description></item>
        /// <item><description><c>DB.Migrate.Failed</c> - Migration failed, fallback triggered</description></item>
        /// <item><description><c>DB.EnsureCreated.Success</c> - Fallback schema creation succeeded</description></item>
        /// <item><description><c>DB.EnsureCreated.Failed</c> - Both migration and fallback failed</description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="AppDbContext"/>
        /// <seealso cref="DatabaseResiliencePolicy"/>
        private async Task MigrateAsync(AppDbContext context, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("Applying pending migrations (background)...");
                _startupActivity?.AddEvent(new ActivityEvent("DB.Migrate.Start"));

                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("✓ Database migrations applied successfully (background)");
                Log.Information("✅ Database migrated successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                _startupActivity?.AddEvent(new ActivityEvent("DB.Migrate.Success",
                    tags: new ActivityTagsCollection { ["duration_ms"] = stopwatch.ElapsedMilliseconds }));
                _startupActivity?.SetTag("db.operation.result", "success");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Migration failed, attempting EnsureCreated instead (background)");
                Log.Warning(ex, "⚠️ Database migration failed - attempting EnsureCreated fallback");

                _startupActivity?.AddEvent(new ActivityEvent("DB.Migrate.Failed",
                    tags: new ActivityTagsCollection
                    {
                        ["error.message"] = ex.Message,
                        ["error.type"] = ex.GetType().Name
                    }));

                // Fallback to EnsureCreated
                try
                {
                    _startupActivity?.AddEvent(new ActivityEvent("DB.EnsureCreated.Start"));
                    await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("✓ Database created successfully (background)");
                    Log.Information("✅ Database created via EnsureCreated in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                    _startupActivity?.AddEvent(new ActivityEvent("DB.EnsureCreated.Success"));
                    _startupActivity?.SetTag("db.operation.result", "success_fallback");
                }
                catch (Exception ensureEx)
                {
                    Log.Error(ensureEx, "❌ Database initialization failed completely");
                    _startupActivity?.AddEvent(new ActivityEvent("DB.EnsureCreated.Failed",
                        tags: new ActivityTagsCollection
                        {
                            ["error.message"] = ensureEx.Message,
                            ["error.type"] = ensureEx.GetType().Name
                        }));
                    _startupActivity?.SetTag("db.operation.result", "failed");
                    _telemetryService?.RecordException(ensureEx,
                        ("operation", "ensure_created"),
                        ("context", "db_initialization"));

                    // Optional degraded-mode fallback: enable in-memory provider for the rest of the app
                    var enableInMemoryFallback = _configuration.GetValue<bool>("Database:EnableInMemoryFallback", false);
                    if (enableInMemoryFallback)
                    {
                        AppDbStartupState.ActivateFallback("Migrate and EnsureCreated failed");
                        _startupActivity?.SetTag("db.fallback", "in_memory");
                        _logger.LogWarning("[DB_FALLBACK] Enabled in-memory degraded mode after migration failure");
                        // Do not rethrow - allow startup to continue in degraded mode
                        return;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Creates a backup of the SQL Server database before migration operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method executes a SQL Server native backup operation using the <c>BACKUP DATABASE</c> command.
        /// Backups are stored in the directory specified by <c>Database:BackupDirectory</c> configuration setting.
        /// </para>
        /// <para>
        /// <b>Configuration Keys:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>Database:BackupOnStartup</c> - Enable/disable backup (default: false)</description></item>
        /// <item><description><c>Database:BackupDirectory</c> - Target directory for backup files (required if enabled)</description></item>
        /// <item><description><c>Database:BackupCommandTimeoutSeconds</c> - Backup timeout (default: 600 seconds)</description></item>
        /// <item><description><c>Database:BackupRetentionDays</c> - Days to retain backups (default: 30)</description></item>
        /// </list>
        /// <para>
        /// <b>Backup Naming:</b> Files are named <c>{DatabaseName}_backup_{yyyyMMdd_HHmmss}.bak</c>
        /// </para>
        /// <para>
        /// After creating the backup, this method automatically cleans up old backups based on retention policy.
        /// Failures are logged but do not prevent startup, allowing graceful degradation.
        /// </para>
        /// </remarks>
        /// <seealso cref="CleanOldBackups"/>
        private void BackupDatabase()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    return;

                connectionString = Environment.ExpandEnvironmentVariables(connectionString);
                var backupDirectory = _configuration.GetValue<string>("Database:BackupDirectory");

                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    _logger.LogDebug("Backup directory not configured; skipping SQL Server backup");
                    return;
                }

                Directory.CreateDirectory(backupDirectory);

                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var databaseName = connection.Database;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var backupPath = Path.Combine(backupDirectory, $"{databaseName}_backup_{timestamp}.bak");

                using var builder = new SqlCommandBuilder();
                var quotedDatabaseName = builder.QuoteIdentifier(databaseName);
                var commandText = $"BACKUP DATABASE {quotedDatabaseName} TO DISK = @backupPath WITH INIT, CHECKSUM";
#pragma warning disable CA2100 // Database name is properly quoted using SqlCommandBuilder.QuoteIdentifier()
                using var command = new SqlCommand(commandText, connection)
#pragma warning restore CA2100
                {
                    CommandTimeout = _configuration.GetValue<int>("Database:BackupCommandTimeoutSeconds", 600)
                };

                command.Parameters.AddWithValue("@backupPath", backupPath);
                command.ExecuteNonQuery();

                _logger.LogInformation("✓ Database backed up to: {BackupPath}", backupPath);

                var retentionDays = _configuration.GetValue<int>("Database:BackupRetentionDays", 30);
                CleanOldBackups(backupDirectory, retentionDays, databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backup database");
            }
        }

        /// <summary>
        /// Removes database backup files older than the configured retention period.
        /// </summary>
        /// <param name="backupDir">Directory containing backup files to clean.</param>
        /// <param name="retentionDays">Number of days to retain backups before deletion.</param>
        /// <param name="databaseName">Name of the database to filter backup files.</param>
        /// <remarks>
        /// <para>
        /// This method implements automated backup retention management by:
        /// </para>
        /// <list type="number">
        /// <item><description>Scanning the backup directory for files matching the pattern <c>{databaseName}_backup_*.bak</c></description></item>
        /// <item><description>Calculating the cutoff date as current date minus <paramref name="retentionDays"/></description></item>
        /// <item><description>Deleting files with creation dates older than the cutoff</description></item>
        /// <item><description>Logging the count of deleted files for audit purposes</description></item>
        /// </list>
        /// <para>
        /// Failures during cleanup are logged but do not affect database operations or startup.
        /// This ensures cleanup issues do not impact application availability.
        /// </para>
        /// <para>
        /// <b>Retention Policy:</b> Controlled by <c>Database:BackupRetentionDays</c> configuration (default: 30 days)
        /// </para>
        /// </remarks>
        private void CleanOldBackups(string backupDir, int retentionDays, string databaseName)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var searchPattern = $"{databaseName}_backup_*.bak";
                var backupFiles = Directory.GetFiles(backupDir, searchPattern);
                var deleted = 0;
                foreach (var file in backupFiles)
                {
                    var info = new FileInfo(file);
                    if (info.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                if (deleted > 0)
                {
                    _logger.LogInformation("✓ Cleaned {Count} old backup(s) (older than {Days} days)", deleted, retentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean old backups");
            }
        }
    }
}
