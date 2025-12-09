using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for analyzing financial data and generating intelligent insights.
/// Goes beyond raw data to provide actionable intelligence, trend detection, and risk assessment.
/// </summary>
public interface IFinancialInsightsService
{
    /// <summary>
    /// Analyzes budget data for a fiscal year and generates comprehensive insights
    /// </summary>
    /// <param name="fiscalYear">Fiscal year to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive financial insights including anomalies, trends, and recommendations</returns>
    Task<FinancialInsights> AnalyzeBudgetAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects anomalies in budget variances
    /// </summary>
    /// <param name="budgetEntries">Budget entries to analyze</param>
    /// <returns>List of detected anomalies</returns>
    List<BudgetAnomaly> DetectAnomalies(IEnumerable<BudgetEntry> budgetEntries);

    /// <summary>
    /// Identifies trends in financial data
    /// </summary>
    /// <param name="fiscalYear">Current fiscal year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected trends</returns>
    Task<List<FinancialTrend>> IdentifyTrendsAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assesses financial risks requiring attention
    /// </summary>
    /// <param name="fiscalYear">Fiscal year to assess</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of identified risks with severity levels</returns>
    Task<List<FinancialRisk>> AssessRisksAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates actionable recommendations based on financial data
    /// </summary>
    /// <param name="insights">Financial insights to base recommendations on</param>
    /// <returns>List of actionable recommendations</returns>
    List<string> GenerateRecommendations(FinancialInsights insights);

    /// <summary>
    /// Identifies positive wins to celebrate
    /// </summary>
    /// <param name="budgetEntries">Budget entries to analyze</param>
    /// <returns>List of positive achievements</returns>
    List<string> IdentifyWins(IEnumerable<BudgetEntry> budgetEntries);

    /// <summary>
    /// Generates smart suggestions based on current context
    /// </summary>
    /// <param name="context">Current user context or query</param>
    /// <param name="recentInsights">Recently generated insights (if available)</param>
    /// <returns>List of contextual smart suggestions</returns>
    List<SmartSuggestion> GetSmartSuggestions(string context, FinancialInsights? recentInsights = null);

    /// <summary>
    /// Calculates overall financial health score (0-100)
    /// </summary>
    /// <param name="fiscalYear">Fiscal year to assess</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health score from 0 (critical) to 100 (excellent)</returns>
    Task<int> CalculateHealthScoreAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
