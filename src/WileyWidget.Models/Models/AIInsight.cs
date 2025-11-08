using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents an AI-generated insight that can be displayed in the UI
/// </summary>
public class AIInsight
{
    /// <summary>
    /// Unique identifier for the insight
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Timestamp when the insight was generated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The conversation mode used for this insight
    /// </summary>
    public ConversationMode Mode { get; set; }

    /// <summary>
    /// The user's query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// The AI's response
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Optional category for the insight (e.g., "Budget Analysis", "Performance")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (High, Medium, Low)
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Whether the insight has been acted upon
    /// </summary>
    public bool IsActioned { get; set; }

    /// <summary>
    /// Optional notes added by the user
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
