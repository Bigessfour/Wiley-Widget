using System;
using System.IO;
using Serilog;
using Serilog.Context;

namespace WileyWidget.Infrastructure.Security;

/// <summary>
/// Security auditing system for enterprise security logging.
/// Tracks authentication, authorization, and security events.
/// </summary>
public class SecurityAuditor : ISecurityAuditor, IDisposable
{
    private readonly string _auditLogPath;
    private bool _disposed;

    public SecurityAuditor()
    {
        _auditLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "security-audit.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_auditLogPath));
    }

    public void LogSecurityEvent(string eventType, string details, string userId = null, object data = null)
    {
        using (LogContext.PushProperty("SecurityEvent", eventType))
        using (LogContext.PushProperty("SecurityDetails", details))
        using (LogContext.PushProperty("UserId", userId ?? "Anonymous"))
        using (LogContext.PushProperty("SecurityData", data))
        {
            Log.Information("🔒 Security Event: {SecurityEvent} - {SecurityDetails}", eventType, details);
        }

        // Write to dedicated security audit log
        try
        {
            var auditEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} | {eventType} | {userId ?? "Anonymous"} | {details}";
            File.AppendAllText(_auditLogPath, auditEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write to security audit log");
        }
    }

    public void LogAuthenticationEvent(string action, bool success, string userId = null)
    {
        LogSecurityEvent("Authentication", $"{action} - {(success ? "Success" : "Failed")}", userId);
    }

    public void LogAuthorizationEvent(string resource, string action, bool allowed, string userId = null)
    {
        LogSecurityEvent("Authorization", $"{action} on {resource} - {(allowed ? "Allowed" : "Denied")}", userId);
    }

    /// <summary>
    /// Performs a security audit with the given message.
    /// </summary>
    /// <param name="message">The audit message.</param>
    public void Audit(string message)
    {
        // TODO: Implement audit logic (e.g., log to file, database, or external system)
        // Example: Log the message using your logging service
        // _logger.LogInformation("Security Audit: {Message}", message);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
            }
            _disposed = true;
        }
    }
}
