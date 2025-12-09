using System;
using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Models;

/// <summary>
/// Represents intelligent financial insights derived from budget analysis.
/// Provides actionable intelligence beyond raw data.
/// </summary>
public class FinancialInsights
{
    /// <summary>
    /// Timestamp when insights were generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall financial health score (0-100)
    /// </summary>
    public int HealthScore { get; set; }

    /// <summary>
    /// One-sentence summary of financial status
    /// </summary>
    public string ExecutiveSummary { get; set; } = string.Empty;

    /// <summary>
    /// Detected anomalies in spending patterns
    /// </summary>
    public List<BudgetAnomaly> Anomalies { get; set; } = new();

    /// <summary>
    /// Identified trends over time
    /// </summary>
    public List<FinancialTrend> Trends { get; set; } = new();

    /// <summary>
    /// Financial risk flags requiring attention
    /// </summary>
    public List<FinancialRisk> Risks { get; set; } = new();

    /// <summary>
    /// Actionable recommendations for improvement
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Positive wins to celebrate
    /// </summary>
    public List<string> Wins { get; set; } = new();

    /// <summary>
    /// Contextual insights about specific departments/funds
    /// </summary>
    public Dictionary<string, string> ContextualInsights { get; set; } = new();
}

/// <summary>
/// Represents an anomalous budget variance
/// </summary>
public class BudgetAnomaly
{
    /// <summary>
    /// Department or fund name
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Type of anomaly (Overspending, Underspending, SuddenSpike, UnexpectedDrop)
    /// </summary>
    public string AnomalyType { get; set; } = string.Empty;

    /// <summary>
    /// Severity level (Low, Medium, High, Critical)
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Variance amount
    /// </summary>
    public decimal VarianceAmount { get; set; }

    /// <summary>
    /// Variance percentage
    /// </summary>
    public decimal VariancePercent { get; set; }

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Historical context (e.g., "Highest overage in 3 years")
    /// </summary>
    public string HistoricalContext { get; set; } = string.Empty;
}

/// <summary>
/// Represents a detected financial trend
/// </summary>
public class FinancialTrend
{
    /// <summary>
    /// Category of trend (Revenue, Expenses, Utilization, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Direction of trend (Rising, Falling, Stable, Volatile)
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Rate of change (e.g., "+12% YoY")
    /// </summary>
    public string RateOfChange { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level in trend detection (0-100)
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Projected impact if trend continues
    /// </summary>
    public string ProjectedImpact { get; set; } = string.Empty;
}

/// <summary>
/// Represents a financial risk requiring attention
/// </summary>
public class FinancialRisk
{
    /// <summary>
    /// Type of risk (BudgetDepletion, CashFlowNegative, UncontrolledSpending, etc.)
    /// </summary>
    public string RiskType { get; set; } = string.Empty;

    /// <summary>
    /// Severity level (Low, Medium, High, Critical)
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Entity affected (department, fund, enterprise)
    /// </summary>
    public string AffectedEntity { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the risk
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Estimated time to impact (e.g., "Budget depletes in 6 weeks")
    /// </summary>
    public string TimeToImpact { get; set; } = string.Empty;

    /// <summary>
    /// Suggested mitigation actions
    /// </summary>
    public List<string> MitigationActions { get; set; } = new();
}

/// <summary>
/// Represents a smart suggestion for proactive guidance
/// </summary>
public class SmartSuggestion
{
    /// <summary>
    /// Display text for the suggestion
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Category of suggestion (Analysis, Comparison, Investigation, Export, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Icon/emoji for visual representation
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Command or action to execute when clicked
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Priority ranking (higher = more important)
    /// </summary>
    public int Priority { get; set; }
}
