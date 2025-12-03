using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace WileyWidget.Services
{
    /// <summary>
    /// Audit logging service for compliance and security tracking.
    /// Maintains append-only audit trail of significant system operations.
    /// Uses Serilog for structured logging with dedicated file sink in root project /logs directory.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly ILogger<AuditService> _logger;
        private readonly Logger _auditLogger;

        /// <summary>
        /// Initializes a new instance of the AuditService.
        /// Logs are written to the root project's /logs directory, not bin subdirectories.
        /// </summary>
        /// <param name="logger">Standard logger for service operations</param>
        public AuditService(ILogger<AuditService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create dedicated Serilog logger for audit events
            // Use root project logs directory
            // Path strategy: AppDomain.CurrentDomain.BaseDirectory is typically:
            // C:\path\to\WileyWidget.WinForms\bin\Debug\net9.0-windows
            // We need to go up 4 levels to reach the project root, then into /logs
            var binDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var parent1 = Directory.GetParent(binDirectory);  // net9.0-windows
            var parent2 = Directory.GetParent(parent1?.FullName ?? ".");  // Debug
            var parent3 = Directory.GetParent(parent2?.FullName ?? ".");  // bin
            var parent4 = Directory.GetParent(parent3?.FullName ?? ".");  // WileyWidget.WinForms
            var projectRoot = Directory.GetParent(parent4?.FullName ?? ".")?.FullName ?? ".";  // Root (Wiley-Widget)

            var logsDirectory = Path.Combine(projectRoot, "logs");
            Directory.CreateDirectory(logsDirectory);

            _auditLogger = new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(logsDirectory, "audit.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 5 * 1024 * 1024,  // 5MB per file
                    formatProvider: CultureInfo.InvariantCulture,
                    flushToDiskInterval: TimeSpan.Zero)  // Immediate flush for audit integrity
                .CreateLogger();

            _logger.LogInformation("AuditService initialized with dedicated Serilog file sink at {LogPath}",
                Path.Combine(logsDirectory, "audit.log"));
        }

        /// <summary>
        /// Logs an audit event asynchronously.
        /// </summary>
        /// <param name="eventName">Name of the event being audited</param>
        /// <param name="payload">Event payload containing details</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task AuditAsync(string eventName, object payload)
        {
            try
            {
                var auditEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    EventName = eventName,
                    Payload = payload,
                    MachineName = Environment.MachineName,
                    ProcessId = Environment.ProcessId
                };

                var json = JsonSerializer.Serialize(auditEntry);

                _auditLogger.Information(
                    "Audit Event | Name: {EventName} | Payload: {Payload}",
                    eventName, TruncateForLog(json, 500));

                _logger.LogInformation("Audit event logged: {EventName}", eventName);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit event: {EventName}", eventName);
                // Don't rethrow - audit failures shouldn't crash the application
            }
        }

        /// <summary>
        /// Truncates text for logging to avoid excessive log file size.
        /// </summary>
        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}
