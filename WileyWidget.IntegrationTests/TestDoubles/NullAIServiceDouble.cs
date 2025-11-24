using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.IntegrationTests.TestDoubles;

/// <summary>
/// Test double for IAIService - returns predictable stub responses.
/// </summary>
public class NullAIServiceDouble : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] AI insights disabled in integration tests.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Data analysis disabled in integration tests.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Review disabled in integration tests.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Mock data generation disabled in integration tests.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Test Stub] AI insights disabled in integration tests.", 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Test stub: validation not available.", 200, null, null));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey)
        => ValidateApiKeyAsync(apiKey, CancellationToken.None);

    public Task UpdateApiKeyAsync(string newApiKey)
        => Task.CompletedTask;

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Test Stub] AI prompt disabled in integration tests.", 200, null, null));
}
