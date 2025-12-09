using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents a user activity log entry for tracking system interactions.
/// Used to populate the Activity Grid with real-time user actions and system events.
/// </summary>
public class ActivityLog
{
    /// <summary>
    /// Unique identifier for the activity log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// When the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of activity (e.g., "UserLogin", "AccountUpdated", "ReportGenerated", "QuickBooksSync").
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the activity.
    /// </summary>
    public string Activity { get; set; } = string.Empty;

    /// <summary>
    /// Additional details about the activity.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// User who performed the activity.
    /// </summary>
    public string User { get; set; } = "System";

    /// <summary>
    /// Entity ID that was affected (e.g., AccountId, BudgetEntryId).
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Entity type that was affected (e.g., "Account", "BudgetEntry", "Report").
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Result or status of the activity (e.g., "Success", "Failed", "Warning").
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration of the activity in milliseconds (for performance tracking).
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// IP address or machine name where the activity originated.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Severity level (Info, Warning, Error, Critical).
    /// </summary>
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// Whether this activity is archived (for cleanup).
    /// </summary>
    public bool IsArchived { get; set; } = false;
}
