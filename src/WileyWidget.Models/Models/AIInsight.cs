using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents an AI-generated insight that can be displayed in the UI
/// </summary>
/// <summary>
/// Represents a class for aiinsight.
/// </summary>
public class AIInsight
{
    /// <summary>
    /// Unique identifier for the insight
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Timestamp when the insight was generated
    /// </summary>
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The conversation mode used for this insight
    /// </summary>
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    public ConversationMode Mode { get; set; }

    /// <summary>
    /// The user's query
    /// </summary>
    /// <summary>
    /// Gets or sets the query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// The AI's response
    /// </summary>
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Optional category for the insight (e.g., "Budget Analysis", "Performance")
    /// </summary>
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (High, Medium, Low)
    /// </summary>
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Whether the insight has been acted upon
    /// </summary>
    /// <summary>
    /// Gets or sets the isactioned.
    /// </summary>
    public bool IsActioned { get; set; }

    /// <summary>
    /// Optional notes added by the user
    /// </summary>
    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    public int EnterpriseId { get; set; }

    public int FiscalYear { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public string Content { get; set; } = string.Empty;
}
