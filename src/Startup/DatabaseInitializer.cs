using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;

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
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            connectionString = Environment.ExpandEnvironmentVariables(connectionString);
            var dbPath = ExtractDatabasePath(connectionString);
            if (string.IsNullOrWhiteSpace(dbPath)) return;
            if (!System.IO.File.Exists(dbPath)) return;

            var backupDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dbPath) ?? string.Empty, "backups");
            if (!System.IO.Directory.Exists(backupDir))
            {
                System.IO.Directory.CreateDirectory(backupDir);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var backupPath = System.IO.Path.Combine(backupDir, $"wileywidget_backup_{timestamp}.db");
            System.IO.File.Copy(dbPath, backupPath, overwrite: false);
            _logger.LogInformation("✓ Database backed up to: {BackupPath}", backupPath);

            var retentionDays = _configuration.GetValue<int>("Database:BackupRetentionDays", 30);
            CleanOldBackups(backupDir, retentionDays);
        }

        private void CleanOldBackups(string backupDir, int retentionDays)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var backupFiles = System.IO.Directory.GetFiles(backupDir, "wileywidget_backup_*.db");
                var deleted = 0;
                foreach (var file in backupFiles)
                {
                    var info = new System.IO.FileInfo(file);
                    if (info.CreationTime < cutoffDate)
                    {
                        System.IO.File.Delete(file);
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

        private static string? ExtractDatabasePath(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;
            if (connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)) return null;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)) return p["Data Source=".Length..].Trim();
                if (p.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase)) return p["DataSource=".Length..].Trim();
                if (p.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase)) return p["Filename=".Length..].Trim();
            }
            return null;
        }
    }
}
