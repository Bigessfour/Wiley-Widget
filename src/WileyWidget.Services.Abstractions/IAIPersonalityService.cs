using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for managing AI personality configuration and system prompt generation.
/// Provides dynamic personality switching and context-aware prompt building.
/// </summary>
public interface IAIPersonalityService
{
    /// <summary>
    /// Gets the currently active personality configuration
    /// </summary>
    AIPersonalityConfig CurrentPersonality { get; }

    /// <summary>
    /// Sets the active personality type
    /// </summary>
    /// <param name="personalityType">Personality type to activate</param>
    void SetPersonality(AIPersonalityType personalityType);

    /// <summary>
    /// Sets the active personality by name (case-insensitive)
    /// </summary>
    /// <param name="personalityName">Personality name (e.g., "Witty", "Friendly")</param>
    void SetPersonalityByName(string personalityName);

    /// <summary>
    /// Builds a system prompt with the current personality
    /// </summary>
    /// <param name="systemContext">Current system context (from IWileyWidgetContextService)</param>
    /// <param name="context">Additional context for the query</param>
    /// <returns>Formatted system prompt with personality applied</returns>
    string BuildSystemPrompt(string systemContext, string context = "");

    /// <summary>
    /// Gets all available personality presets
    /// </summary>
    /// <returns>Dictionary of personality types and configurations</returns>
    Dictionary<AIPersonalityType, AIPersonalityConfig> GetAllPersonalities();

    /// <summary>
    /// Applies personality-specific formatting to a response
    /// (e.g., adding emoji, adjusting tone)
    /// </summary>
    /// <param name="response">Raw AI response</param>
    /// <returns>Response with personality formatting applied</returns>
    string ApplyPersonalityFormatting(string response);
}
