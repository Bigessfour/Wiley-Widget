using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Interface for AI services providing insights and analysis
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Get AI insights for the provided context and question
    /// </summary>
    Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Review application areas and provide recommendations
    /// </summary>
    Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate mock data suggestions
    /// </summary>
    Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get AI insights along with a status code and machine-friendly error code when applicable.
    /// This allows the UI to distinguish network/auth/rate-limit errors from valid responses.
    /// </summary>
    Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed result for AI responses that includes status and machine code for UI handling
/// </summary>
public record AIResponseResult(string Content, int HttpStatusCode = 200, string? ErrorCode = null, string? RawErrorBody = null);