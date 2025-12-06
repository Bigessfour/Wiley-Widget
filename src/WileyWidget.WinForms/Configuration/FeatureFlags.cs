using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Configuration;

/// <summary>
/// Feature flags for controlling optional functionality
/// Provides runtime switches for experimental or environment-specific features
/// </summary>
public class FeatureFlags
{
    /// <summary>
    /// Enable Python tool execution via AIAssistantService
    /// When false, falls back to .NET-only tool implementations
    /// Default: true
    /// </summary>
    public bool EnablePythonTools { get; set; } = true;

    /// <summary>
    /// Enable conversational AI fallback in AIChatControl
    /// When false, only tool-based responses are provided
    /// Default: true
    /// </summary>
    public bool EnableConversationalAI { get; set; } = true;

    /// <summary>
    /// Enable automatic conversation history persistence
    /// When false, conversations are not auto-saved after each message
    /// Default: true
    /// </summary>
    public bool EnableAutoSaveConversations { get; set; } = true;

    /// <summary>
    /// Enable xAI tool calling (function calling) integration
    /// When false, standard prompt/response without tools
    /// Default: true
    /// </summary>
    public bool EnableXAIToolCalling { get; set; } = true;

    /// <summary>
    /// Enable Polly resilience policies for UI calls to AI services
    /// When false, direct calls without retry/circuit breaker
    /// Default: true (already implemented in XAIService)
    /// </summary>
    public bool EnablePollyResilience { get; set; } = true;

    /// <summary>
    /// Maximum conversation history length to send to AI
    /// Limits token usage for long conversations
    /// Default: 50 messages
    /// </summary>
    public int MaxConversationHistoryLength { get; set; } = 50;

    /// <summary>
    /// Enable conversation history search functionality
    /// When false, search is disabled in UI
    /// Default: false (planned feature)
    /// </summary>
    public bool EnableConversationSearch { get; set; } = false;

    /// <summary>
    /// Enable AI-powered anomaly detection in financial data
    /// When false, anomaly detection tools are unavailable
    /// Default: false (planned feature)
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = false;

    /// <summary>
    /// Enable scenario simulation tools
    /// When false, what-if analysis tools are unavailable
    /// Default: true
    /// </summary>
    public bool EnableScenarioSimulation { get; set; } = true;

    /// <summary>
    /// Load configuration from appsettings.json section "FeatureFlags"
    /// </summary>
    public static FeatureFlags FromConfiguration(IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var flags = new FeatureFlags();
        configuration.GetSection("FeatureFlags").Bind(flags);
        return flags;
    }

    /// <summary>
    /// Validate feature flag configuration
    /// Ensures dependencies between features are met
    /// </summary>
    public void Validate()
    {
        // If conversational AI is disabled, tool fallback must be available
        if (!EnableConversationalAI && !EnablePythonTools && !EnableXAIToolCalling)
        {
            throw new InvalidOperationException(
                "At least one of EnableConversationalAI, EnablePythonTools, or EnableXAIToolCalling must be true.");
        }

        // Validate numeric ranges
        if (MaxConversationHistoryLength < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConversationHistoryLength),
                "MaxConversationHistoryLength must be at least 1.");
        }

        if (MaxConversationHistoryLength > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConversationHistoryLength),
                "MaxConversationHistoryLength should not exceed 200 to avoid token limits.");
        }
    }
}

/// <summary>
/// Extension methods for dependency injection registration
/// </summary>
public static class FeatureFlagsExtensions
{
    /// <summary>
    /// Register FeatureFlags in DI container from configuration
    /// Usage: services.AddFeatureFlags(configuration);
    /// </summary>
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var flags = FeatureFlags.FromConfiguration(configuration);
        flags.Validate();
        services.AddSingleton(flags);
        return services;
    }
}
