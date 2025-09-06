using System;
using System.IO;
using Serilog.Context;

namespace WileyWidget.Diagnostics.Security;

/// <summary>
/// Security auditing system for enterprise security logging.
/// Tracks authentication, authorization, and security events.
/// </summary>
public class SecurityAuditor
{
    private readonly string _auditLogPath;

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
            Serilog.Log.Information("🔒 Security Event: {SecurityEvent} - {SecurityDetails}", eventType, details);
        }

        // Write to dedicated security audit log
        try
        {
            var auditEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} | {eventType} | {userId ?? "Anonymous"} | {details}";
            File.AppendAllText(_auditLogPath, auditEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to write to security audit log");
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
}
