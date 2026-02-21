#nullable enable

// NOTE: GrokAgentService is sealed — this mock implements IAIService instead.
// WarRoomViewModel holds a GrokAgentService? field directly. To use this mock in WarRoomPanel
// integration tests, register it as IAIService in the test DI container, or refactor
// WarRoomViewModel to depend on IAIService rather than the concrete GrokAgentService type.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Tests.Integration.Mocks;

/// <summary>
/// Deterministic mock for <see cref="WileyWidget.Services.Abstractions.IAIService"/>.
/// Returns canned responses without touching any real network endpoints.
/// Cannot subclass <c>GrokAgentService</c> (sealed class).
/// </summary>
public sealed class MockGrokAgentService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(
            "JARVIS here. Mock insight: Your 12% water rate increase with 4% inflation over 5 years " +
            "projects a healthy $2.3M surplus by year 5. Risk level: LOW.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult($"Mock analysis ({analysisType}): Data looks healthy. All systems nominal.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult($"Mock review of '{areaName}': Current state is '{currentState}'. No anomalies detected.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult($"Mock suggestions for {dataType}: Use standard deterministic test fixtures.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult(
            "Mock insight: Scenario analysis complete. Surplus projected.",
            HttpStatusCode: 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Mock validation: API key accepted.", HttpStatusCode: 200));

    public Task UpdateApiKeyAsync(string newApiKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Mock response: Prompt processed successfully.", HttpStatusCode: 200));

    public Task<string> GetChatCompletionAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(
            "JARVIS here, sir. Mock completion: scenario analysis complete. MORE COWBELL!");

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string prompt,
        string? systemMessage = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Analyzing scenario...";
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        yield return "Projected surplus: $2.3M by year 5";
        yield return "Risk: LOW — MORE COWBELL!";
    }

    public Task<string> SendMessageAsync(string message, object conversationHistory, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock response: message received and processed by JARVIS.");
}
