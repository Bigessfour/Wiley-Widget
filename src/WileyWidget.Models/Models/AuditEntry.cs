using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents an audit trail entry for tracking changes to budget data
/// </summary>
/// <summary>
/// Represents a class for auditentry.
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the entitytype.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the entityid.
    /// </summary>
    public int EntityId { get; set; }
    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public string User { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; } // JSON serialized old values
    public string? NewValues { get; set; } // JSON serialized new values
    public string? Changes { get; set; } // Description of what changed
}
