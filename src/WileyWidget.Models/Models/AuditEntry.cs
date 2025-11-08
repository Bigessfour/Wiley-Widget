using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents an audit trail entry for tracking changes to budget data
/// </summary>
public class AuditEntry
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; } // JSON serialized old values
    public string? NewValues { get; set; } // JSON serialized new values
    public string? Changes { get; set; } // Description of what changed
}