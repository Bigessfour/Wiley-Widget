namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Service interface for AI-driven rate recommendations using xAI Grok API.
/// </summary>
public interface IGrokRecommendationService
{
    /// <summary>
    /// Queries Grok API for recommended adjustment factors based on department expenses.
    /// </summary>
    /// <param name="departmentExpenses">Dictionary of department name to monthly expenses</param>
    /// <param name="targetProfitMargin">Target profit margin percentage (e.g., 15.0 for 15%)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of department name to recommended adjustment factor</returns>
    Task<Dictionary<string, decimal>> GetRecommendedAdjustmentFactorsAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a text explanation of the recommendations from Grok.
    /// </summary>
    /// <param name="departmentExpenses">Department expenses</param>
    /// <param name="targetProfitMargin">Target profit margin</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated explanation text</returns>
    Task<string> GetRecommendationExplanationAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default);
}
