using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public class NullAIService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("AI services are currently unavailable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult("Data analysis is currently unavailable.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult("Application review is currently unavailable.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock data generation is currently unavailable.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("AI services are currently unavailable.", 503, "Unavailable"));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Live key validation is unavailable.", 503, "Unavailable"));

    public Task UpdateApiKeyAsync(string newApiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Sent prompt failed: AI unavailable.", 503, "Unavailable"));

    public async IAsyncEnumerable<string> StreamResponseAsync(string prompt, string? systemMessage = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "AI ";
        yield return "services ";
        yield return "are ";
        yield return "currently ";
        yield return "unavailable.";
        await Task.CompletedTask;
    }

    public Task<string> SendMessageAsync(string message, object conversationHistory, CancellationToken cancellationToken = default)
        => Task.FromResult("AI services are currently unavailable.");
}

