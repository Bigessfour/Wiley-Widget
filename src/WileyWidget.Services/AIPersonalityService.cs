using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of AI personality management service.
/// Handles personality configuration, system prompt generation, and response formatting.
/// </summary>
public class AIPersonalityService : IAIPersonalityService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIPersonalityService> _logger;
    private AIPersonalityConfig _currentPersonality;

    public AIPersonalityService(IConfiguration configuration, ILogger<AIPersonalityService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load personality from configuration or default to Friendly
        var personalityName = _configuration["AI:Personality"] ?? "Friendly";
        _currentPersonality = AIPersonalityConfig.GetPresetByName(personalityName);
        
        _logger.LogInformation("AIPersonalityService initialized with personality: {Personality}", _currentPersonality.DisplayName);
    }

    public AIPersonalityConfig CurrentPersonality => _currentPersonality;

    public void SetPersonality(AIPersonalityType personalityType)
    {
        _currentPersonality = AIPersonalityConfig.GetPreset(personalityType);
        _logger.LogInformation("Personality changed to: {Personality}", _currentPersonality.DisplayName);
    }

    public void SetPersonalityByName(string personalityName)
    {
        if (string.IsNullOrWhiteSpace(personalityName))
        {
            _logger.LogWarning("Attempted to set empty personality name, keeping current: {Current}", _currentPersonality.DisplayName);
            return;
        }

        _currentPersonality = AIPersonalityConfig.GetPresetByName(personalityName);
        _logger.LogInformation("Personality changed to: {Personality} (from name: {Name})", _currentPersonality.DisplayName, personalityName);
    }

    public string BuildSystemPrompt(string systemContext, string context = "")
    {
        var template = _currentPersonality.SystemPromptTemplate;
        
        // Replace placeholders
        var prompt = template
            .Replace("{systemContext}", systemContext ?? string.Empty)
            .Replace("{context}", context ?? string.Empty);

        _logger.LogDebug("Built system prompt with personality: {Personality}", _currentPersonality.DisplayName);
        return prompt;
    }

    public Dictionary<AIPersonalityType, AIPersonalityConfig> GetAllPersonalities()
    {
        return AIPersonalityConfig.Presets;
    }

    public string ApplyPersonalityFormatting(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // For now, return as-is. Future: Add post-processing based on personality
        // e.g., inject emoji, adjust punctuation for enthusiasm, etc.
        return response;
    }
}
