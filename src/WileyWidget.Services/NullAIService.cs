using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// No-op AI service used in development/testing when API keys are not configured.
/// Prevents startup failures by providing predictable stub responses.
/// </summary>
public class NullAIService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] AI insights are disabled in development. Configure XAI_API_KEY to enable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Data analysis is disabled in development.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Review is disabled in development.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Mock data generation is disabled in development.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Dev Stub] AI insights are disabled in development. Configure XAI_API_KEY to enable.", 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Dev stub: validation not available in development.", 403, "AuthFailure", null));

    // Adapter overload to satisfy the Abstractions interface which declares
    // ValidateApiKeyAsync(string) without a CancellationToken parameter.
    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey)
        => ValidateApiKeyAsync(apiKey, CancellationToken.None);

    public Task UpdateApiKeyAsync(string newApiKey)
    {
        // No-op in dev stub
        return Task.CompletedTask;
    }

    public Task<AIResponseResult> SendPromptAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Dev Stub] AI prompt sending is disabled in development. Configure XAI_API_KEY to enable.", 403, "AuthFailure", null));
}
