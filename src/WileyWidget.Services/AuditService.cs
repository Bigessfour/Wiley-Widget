using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Simple audit service. Writes structured metadata to the normal ILogger and an append-only audit file.
    /// This service MUST NOT store secret values. Callers should redact sensitive fields before calling.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly ILogger<AuditService> _logger;
        private readonly string _auditPath;

        public AuditService(ILogger<AuditService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var logs = Path.Combine(AppContext.BaseDirectory ?? ".", "logs");
            Directory.CreateDirectory(logs);
            _auditPath = Path.Combine(logs, "audit.log");
        }

        public Task AuditAsync(string eventName, object details)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentNullException(nameof(eventName));

            // Log structured data via ILogger
            try
            {
                _logger.LogInformation("Audit: {Event} {@Details}", eventName, details);
            }
            catch
            {
                // Swallow logging exceptions to avoid impact on UX
            }

            // Also append a compact line to the audit file. Ensure secrets are not present.
            try
            {
                // Rotate and perform retention maintenance before writing
                TryRotateAuditFileIfNeeded();
                PerformAuditRetentionCleanup();

                var entry = new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Event = eventName,
                    Details = details
                };

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
                // Append newline-terminated entry to audit file
                File.AppendAllText(_auditPath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Don't let audit writes throw into calling code; log and continue
                try { _logger.LogWarning(ex, "Failed to write audit entry"); } catch { }
            }

            return Task.CompletedTask;
        }

        private void TryRotateAuditFileIfNeeded()
        {
            try
            {
                const long maxBytes = 5 * 1024 * 1024; // 5 MB
                if (!File.Exists(_auditPath)) return;

                var fi = new FileInfo(_auditPath);
                if (fi.Length < maxBytes) return;

                var rotated = _auditPath + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".log";
                // Move current audit file to rotated name
                File.Move(_auditPath, rotated);
                _logger.LogInformation("Rotated audit log to {Rotated}", rotated);
            }
            catch (Exception ex)
            {
                try { _logger.LogWarning(ex, "Failed to rotate audit file"); } catch { }
            }
        }

        private void PerformAuditRetentionCleanup()
        {
            try
            {
                var folder = Path.GetDirectoryName(_auditPath) ?? AppContext.BaseDirectory;
                var files = Directory.GetFiles(folder, "audit.log.*.log");
                var threshold = DateTime.UtcNow.AddDays(-30);
                foreach (var file in files)
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.LastWriteTimeUtc < threshold)
                        {
                            fi.Delete();
                            _logger.LogInformation("Deleted old audit file {File}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        try { _logger.LogDebug(ex, "Failed to delete audit file {File}", file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                try { _logger.LogWarning(ex, "Failed to perform audit retention cleanup"); } catch { }
            }
        }
    }
}
