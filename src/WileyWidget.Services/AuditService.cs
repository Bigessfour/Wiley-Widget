using System.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

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
            // Use workspace logs folder for centralized logging
            var logsDirectory = LogPathResolver.GetLogsDirectory();
            _auditPath = Path.Combine(logsDirectory, "audit.log");
        }

        public Task AuditAsync(string eventName, object details, CancellationToken cancellationToken = default)
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

        public async Task<IEnumerable<AuditEntry>> GetAuditEntriesAsync(DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null,
            int? skip = null,
            int? take = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_auditPath))
                    return Enumerable.Empty<AuditEntry>();

                var lines = await File.ReadAllLinesAsync(_auditPath);
                var entries = new List<AuditEntry>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var auditRecord = JsonSerializer.Deserialize<AuditRecord>(line);
                        if (auditRecord == null) continue;

                        // Apply filters
                        if (startDate.HasValue && auditRecord.Timestamp < startDate.Value) continue;
                        if (endDate.HasValue && auditRecord.Timestamp > endDate.Value) continue;

                        // For now, map Event to Action, and Details to Changes
                        // In a real implementation, Details would contain structured audit data
                        var entry = new AuditEntry
                        {
                            Id = entries.Count + 1, // Simple ID for display
                            EntityType = "Unknown", // Would come from Details
                            EntityId = 0, // Would come from Details
                            Action = auditRecord.Event,
                            User = "System", // Would come from Details or context
                            Timestamp = auditRecord.Timestamp.DateTime,
                            Changes = auditRecord.Details?.ToString() ?? string.Empty
                        };

                        entries.Add(entry);
                    }
                    catch (JsonException)
                    {
                        // Skip malformed lines
                        continue;
                    }
                }

                // Apply additional filters
                if (!string.IsNullOrEmpty(actionType))
                    entries = entries.Where(e => e.Action.Contains(actionType, StringComparison.OrdinalIgnoreCase)).ToList();

                if (!string.IsNullOrEmpty(user))
                    entries = entries.Where(e => e.User.Contains(user, StringComparison.OrdinalIgnoreCase)).ToList();

                // Apply pagination
                if (skip.HasValue)
                    entries = entries.Skip(skip.Value).ToList();

                if (take.HasValue)
                    entries = entries.Take(take.Value).ToList();

                return entries.OrderByDescending(e => e.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit entries");
                return Enumerable.Empty<AuditEntry>();
            }
        }

        public async Task<int> GetAuditEntriesCountAsync(DateTime? startDate = null,
            DateTime? endDate = null,
            string? actionType = null,
            string? user = null, CancellationToken cancellationToken = default)
        {
            var entries = await GetAuditEntriesAsync(startDate, endDate, actionType, user, null, null);
            return entries.Count();
        }

        private class AuditRecord
        {
            public DateTimeOffset Timestamp { get; set; }
            public string Event { get; set; } = string.Empty;
            public object? Details { get; set; }
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
