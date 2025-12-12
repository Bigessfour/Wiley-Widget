#nullable enable

namespace WileyWidget.Models;

/// <summary>
/// Persistence model for activity log entries.
/// </summary>
public class ActivityLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Activity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
