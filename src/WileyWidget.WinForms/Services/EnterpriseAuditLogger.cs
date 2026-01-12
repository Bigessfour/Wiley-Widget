using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Comprehensive audit logging for enterprise compliance and security tracking.
    /// Logs all user actions, data modifications, and access attempts.
    /// </summary>
    public class EnterpriseAuditLogger : IDisposable
    {
        private readonly IActivityLogRepository _repository;
        private readonly ILogger<EnterpriseAuditLogger> _logger;

        public EnterpriseAuditLogger(IActivityLogRepository repository, ILogger<EnterpriseAuditLogger> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Logs a user action.
        /// </summary>
        public async Task LogActionAsync(AuditLogEntry entry)
        {
            try
            {
                var log = new WileyWidget.Services.Abstractions.ActivityLog
                {
                    ActivityType = entry.ActionType,
                    Activity = entry.Description,
                    Details = entry.Details,
                    User = entry.User ?? Environment.UserName,
                    Status = entry.IsSuccess ? "Success" : "Failed",
                    EntityType = entry.EntityType,
                    EntityId = entry.EntityId,
                    Severity = entry.Severity,
                    Timestamp = DateTime.UtcNow
                };

                await _repository.LogActivityAsync(log);
                _logger.LogDebug("Audit log recorded: {Action} by {User}", entry.ActionType, entry.User);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry");
            }
        }

        /// <summary>
        /// Logs data access.
        /// </summary>
        public async Task LogAccessAsync(string user, string resource, string action, bool allowed)
        {
            await LogActionAsync(new AuditLogEntry
            {
                ActionType = "DataAccess",
                Description = $"Access to {resource}",
                Details = action,
                User = user,
                EntityType = "Resource",
                EntityId = resource,
                Severity = allowed ? "Info" : "Warning",
                IsSuccess = allowed
            });
        }

        /// <summary>
        /// Logs data modification.
        /// </summary>
        public async Task LogModificationAsync(string user, string entityType, string entityId, string changeDetails)
        {
            await LogActionAsync(new AuditLogEntry
            {
                ActionType = "DataModification",
                Description = $"Modified {entityType}",
                Details = changeDetails,
                User = user,
                EntityType = entityType,
                EntityId = entityId,
                Severity = "Info",
                IsSuccess = true
            });
        }

        /// <summary>
        /// Logs security events.
        /// </summary>
        public async Task LogSecurityEventAsync(string eventType, string details, string severity = "Warning")
        {
            await LogActionAsync(new AuditLogEntry
            {
                ActionType = "SecurityEvent",
                Description = eventType,
                Details = details,
                User = Environment.UserName,
                Severity = severity,
                IsSuccess = false
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Audit log entry structure.
    /// </summary>
    public class AuditLogEntry
    {
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string User { get; set; } = Environment.UserName;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public bool IsSuccess { get; set; } = true;
    }
}
