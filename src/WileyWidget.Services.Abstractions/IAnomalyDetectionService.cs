using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Provides AI-powered anomaly detection for financial data analysis.
    /// Identifies unusual patterns in budget variance, revenue trends, and utility consumption.
    /// </summary>
    public interface IAnomalyDetectionService
    {
        /// <summary>
        /// Detects anomalies in a dataset using AI analysis.
        /// </summary>
        /// <typeparam name="T">The type of data items</typeparam>
        /// <param name="items">Collection of data items to analyze</param>
        /// <param name="valueExtractor">Function to extract numeric value for analysis</param>
        /// <param name="contextExtractor">Function to extract context description</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of detected anomalies with explanations</returns>
        Task<List<AnomalyDetectionResult<T>>> DetectAnomaliesAsync<T>(
            IEnumerable<T> items,
            Func<T, double> valueExtractor,
            Func<T, string> contextExtractor,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Analyzes budget variance anomalies for a fiscal period.
        /// </summary>
        /// <param name="budgetedAmount">Budgeted amount</param>
        /// <param name="actualAmount">Actual amount spent/received</param>
        /// <param name="accountName">Account name for context</param>
        /// <param name="period">Time period description</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Anomaly analysis result with explanation</returns>
        Task<AnomalyAnalysis> AnalyzeBudgetVarianceAsync(
            decimal budgetedAmount,
            decimal actualAmount,
            string accountName,
            string period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if anomaly detection is available.
        /// </summary>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a detected anomaly in the dataset.
    /// </summary>
    public class AnomalyDetectionResult<T> where T : class
    {
        /// <summary>
        /// The anomalous data item.
        /// </summary>
        public required T Item { get; init; }

    /// <summary>
    /// Severity score (0.0 to 1.0, where 1.0 is most severe).
    /// </summary>
    public required double Severity { get; init; }

    /// <summary>
    /// AI-generated explanation of why this is an anomaly.
    /// </summary>
    public required string Explanation { get; init; }

    /// <summary>
    /// Expected value range for context.
    /// </summary>
    public string? ExpectedRange { get; init; }

    /// <summary>
    /// Actual value that triggered the anomaly.
    /// </summary>
    public required double ActualValue { get; init; }
}

/// <summary>
/// Result of budget variance anomaly analysis.
/// </summary>
public class AnomalyAnalysis
{
    /// <summary>
    /// Whether an anomaly was detected.
    /// </summary>
    public required bool IsAnomaly { get; init; }

    /// <summary>
    /// Severity score (0.0 to 1.0).
    /// </summary>
    public required double Severity { get; init; }

    /// <summary>
    /// AI-generated explanation.
    /// </summary>
    public required string Explanation { get; init; }

    /// <summary>
    /// Variance percentage.
    /// </summary>
    public required decimal VariancePercent { get; init; }

    /// <summary>
    /// Recommended actions.
    /// </summary>
    public List<string> RecommendedActions { get; init; } = new();
}
}
