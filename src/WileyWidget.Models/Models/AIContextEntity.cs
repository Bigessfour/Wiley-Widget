using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents extracted contextual entities from AI conversations.
/// Stores names, dates, events, and other important information mentioned during chats
/// to enable personalized AI responses and long-term memory capabilities.
/// </summary>
public class AIContextEntity
{
    /// <summary>
    /// Unique identifier for the context entity.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the conversation where this entity was mentioned.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity: Person, Organization, Date, Event, Location, Account, Budget, etc.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The actual value or name of the entity (e.g., "John Smith", "2025-12-15", "Budget Meeting").
    /// </summary>
    public string EntityValue { get; set; } = string.Empty;

    /// <summary>
    /// Normalized/canonical form of the entity for matching (lowercase, standardized format).
    /// </summary>
    public string NormalizedValue { get; set; } = string.Empty;

    /// <summary>
    /// Context around when the entity was mentioned (sentence or paragraph excerpt).
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Confidence score from extraction (0.0 to 1.0), if using ML/NLP extraction.
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// When this entity was first mentioned/extracted.
    /// </summary>
    public DateTime FirstMentionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this entity was last mentioned/updated.
    /// </summary>
    public DateTime LastMentionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// How many times this entity has been referenced across conversations.
    /// </summary>
    public int MentionCount { get; set; } = 1;

    /// <summary>
    /// Additional metadata as JSON (relationships, attributes, etc.).
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Importance/relevance score for prioritization (0-100).
    /// </summary>
    public int ImportanceScore { get; set; } = 50;

    /// <summary>
    /// Whether this entity is currently active/relevant.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tags for categorization (comma-separated or JSON array).
    /// </summary>
    public string? Tags { get; set; }
}
