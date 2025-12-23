using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Business.Services;

/// <summary>
/// Service for AI-driven rate recommendations using xAI Grok API.
/// </summary>
public class GrokRecommendationService : IGrokRecommendationService
{
    private readonly ILogger<GrokRecommendationService> _logger;
    private readonly HttpClient _httpClient;
    private const string GrokApiUrl = "https://api.x.ai/v1/chat/completions";

    public GrokRecommendationService(
        ILogger<GrokRecommendationService> logger,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<Dictionary<string, decimal>> GetRecommendedAdjustmentFactorsAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Querying Grok API for recommendations with {ProfitMargin}% margin",
                targetProfitMargin);

            var prompt = BuildRecommendationPrompt(departmentExpenses, targetProfitMargin);
            var response = await CallGrokApiAsync(prompt, cancellationToken);
            var recommendations = ParseRecommendationResponse(response);

            _logger.LogInformation("Grok recommendations: {Recommendations}",
                string.Join(", ", recommendations.Select(r => $"{r.Key}={r.Value:F2}")));

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Grok API for recommendations");
            throw;
        }
    }

    public async Task<string> GetRecommendationExplanationAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Requesting explanation from Grok API");

            var prompt = BuildExplanationPrompt(departmentExpenses, targetProfitMargin);
            var response = await CallGrokApiAsync(prompt, cancellationToken);
            var explanation = ParseExplanationResponse(response);

            return explanation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting explanation from Grok API");
            throw;
        }
    }

    private string BuildRecommendationPrompt(Dictionary<string, decimal> expenses, decimal margin)
    {
        var expenseList = string.Join(", ", expenses.Select(e => $"{e.Key}: ${e.Value:N2}"));
        return $"Based on monthly expenses [{expenseList}], recommend adjustment factors for full cost recovery + {margin}% profit margin. Return only a JSON object with department names as keys and decimal adjustment factors as values (e.g., {{\"Water\": 1.15, \"Sewer\": 1.12}}).";
    }

    private string BuildExplanationPrompt(Dictionary<string, decimal> expenses, decimal margin)
    {
        var expenseList = string.Join(", ", expenses.Select(e => $"{e.Key}: ${e.Value:N2}"));
        return $"Explain the recommended rate adjustments for monthly expenses [{expenseList}] with a target profit margin of {margin}%. Provide a clear, concise explanation of why each department needs the recommended adjustment.";
    }

    private async Task<string> CallGrokApiAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            model = "grok-4-0709",
            stream = false,
            temperature = 0.7
        };

        var response = await _httpClient.PostAsJsonAsync(GrokApiUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseContent;
    }

    private Dictionary<string, decimal> ParseRecommendationResponse(string response)
    {
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (!string.IsNullOrEmpty(content))
            {
                // Try to parse JSON from the content
                try
                {
                    using var contentDoc = JsonDocument.Parse(content);
                    var recommendations = new Dictionary<string, decimal>();

                    foreach (var property in contentDoc.RootElement.EnumerateObject())
                    {
                        if (property.Value.TryGetDecimal(out var value))
                        {
                            recommendations[property.Name] = value;
                        }
                    }

                    return recommendations;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to parse JSON from Grok response content: {Content}", content);
                }
            }
        }

        // Fallback: return default adjustments
        _logger.LogWarning("Using fallback recommendations due to parsing failure");
        return new Dictionary<string, decimal>
        {
            ["Water"] = 1.15m,
            ["Sewer"] = 1.12m,
            ["Trash"] = 1.08m,
            ["Apartments"] = 1.10m
        };
    }

    private string ParseExplanationResponse(string response)
    {
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? "Unable to generate explanation.";
        }

        return "Unable to generate explanation from Grok API response.";
    }
}
