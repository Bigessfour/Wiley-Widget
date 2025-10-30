using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;

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

    public Task<(string status, string result)> GetInsightsWithStatusAsync(string a, string b, CancellationToken ct = default)
        => Task.FromResult(("ok", "FAKE_INSIGHTS"));

    public Task<bool> ValidateApiKeyAsync(string key, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task UpdateApiKeyAsync(string key)
        => Task.CompletedTask;
}
