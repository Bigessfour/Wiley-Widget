using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services;

namespace WileyWidget.Fakes;

public class FakeAIService : IAIService
{
    public Task<string> GetInsightsAsync(string a, string b, CancellationToken ct = default)
        => Task.FromResult("FAKE_AI_INSIGHTS");

    public Task<string> AnalyzeDataAsync(string a, string b, CancellationToken ct = default)
        => Task.FromResult("FAKE_ANALYSIS");

    public Task<string> ReviewApplicationAreaAsync(string a, string b, CancellationToken ct = default)
        => Task.FromResult("FAKE_REVIEW");

    public Task<string> GenerateMockDataSuggestionsAsync(string a, string b, CancellationToken ct = default)
        => Task.FromResult("FAKE_MOCK_DATA");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken ct = default)
        => Task.FromResult(new AIResponseResult("FAKE_INSIGHTS"));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
        => Task.FromResult(new AIResponseResult("API_KEY_VALID"));

    public Task UpdateApiKeyAsync(string key)
        => Task.CompletedTask;

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult(new AIResponseResult("FAKE_PROMPT_RESPONSE"));
}
