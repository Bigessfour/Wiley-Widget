using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Represents a saved conversation with the AI assistant.
/// Stores the conversation metadata and history for persistence and retrieval.
/// </summary>
public class ConversationHistory
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User-friendly identifier or title for the conversation.
    /// </summary>
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Title or summary of the conversation.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description or notes about the conversation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON-serialized conversation messages.
    /// Stores the full chat history for retrieval.
    /// </summary>
    public string MessagesJson { get; set; } = "[]";

    /// <summary>
    /// Context or initial prompt that started the conversation.
    /// </summary>
    public string? InitialContext { get; set; }

    /// <summary>
    /// Metadata about the conversation as JSON.
    /// Can store application-specific data like selected account, budget period, etc.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation was last accessed.
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Total number of messages in the conversation.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Total number of tool calls made during the conversation.
    /// </summary>
    public int ToolCallCount { get; set; }

    /// <summary>
    /// Whether this conversation is archived (soft delete).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Whether this conversation is a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }
}

/// <summary>
/// DTO for serializing chat messages to JSON storage.
/// </summary>
public class ChatMessageData
{
    /// <summary>
    /// Whether this is a user message.
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// The message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional metadata attached to the message.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
