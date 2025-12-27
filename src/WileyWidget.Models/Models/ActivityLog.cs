#nullable enable

namespace WileyWidget.Models;

/// <summary>
/// Persistence model for activity log entries.
/// </summary>
/// <summary>
/// Represents a class for activitylog.
/// </summary>
public class ActivityLog
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string Activity { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public string Details { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public string User { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the icon.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the activitytype.
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the durationms.
    /// </summary>
    public long DurationMs { get; set; }
    /// <summary>
    /// Gets or sets the entitytype.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the entityid.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    public string Severity { get; set; } = string.Empty;
}
