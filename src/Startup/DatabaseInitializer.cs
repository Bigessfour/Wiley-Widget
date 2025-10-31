using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using Microsoft.Data.SqlClient;
using System.IO;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Performs database initialization (backup, migrate/ensure created, connectivity test)
    /// on a background thread to keep the UI responsive.
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            DbContextOptions<AppDbContext> options,
            IConfiguration configuration,
            ILogger<DatabaseInitializer> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Background DB initialization starting...");

                using var context = new AppDbContext(_options);

                // Optional backup (best-effort)
                var backupOnStartup = _configuration.GetValue<bool>("Database:BackupOnStartup", false);
                if (backupOnStartup)
                {
                    try { BackupDatabase(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Database backup failed (non-fatal)"); }
                }

                var autoMigrate = _configuration.GetValue<bool>("Database:AutoMigrate", true);
                if (autoMigrate)
                {
                    try
                    {
                        _logger.LogDebug("Applying pending migrations (background)...");
                        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("✓ Database migrations applied successfully (background)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Migration failed, attempting EnsureCreated instead (background)");
                        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("✓ Database created successfully (background)");
                    }
                }
                else
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("✓ Database existence verified (background)");
                }

                // Connectivity test
                try
                {
                    var canConnect = await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                    if (canConnect) _logger.LogInformation("✓ Database connection verified (background)");
                    else _logger.LogWarning("⚠ Database connection test failed (background)");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Database connectivity test threw (non-fatal)");
                }

                _logger.LogInformation("Background DB initialization complete.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background DB initialization encountered issues; continuing startup");
            }
        }

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
