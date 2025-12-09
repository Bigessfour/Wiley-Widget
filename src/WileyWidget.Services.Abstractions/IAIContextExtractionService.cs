using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service interface for extracting contextual entities from AI conversations.
/// Identifies names, dates, events, locations, and other important information
/// to build AI memory and enable personalized responses.
/// </summary>
public interface IAIContextExtractionService
{
    /// <summary>
    /// Extract entities from a chat message and conversation context.
    /// </summary>
    Task<List<AIContextEntity>> ExtractEntitiesAsync(string message, string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Extract entities from multiple messages in batch.
    /// </summary>
    Task<List<AIContextEntity>> ExtractEntitiesFromConversationAsync(List<ChatMessage> messages, string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Normalize entity values for consistent matching (e.g., lowercase, standard date formats).
    /// </summary>
    string NormalizeEntityValue(string entityValue, string entityType);

    /// <summary>
    /// Calculate importance score based on context and frequency.
    /// </summary>
    int CalculateImportanceScore(AIContextEntity entity, List<ChatMessage> conversationHistory);
}
