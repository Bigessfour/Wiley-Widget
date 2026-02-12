#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WileyWidget.Models;

/// <summary>
/// Result for a budget forecast containing all proposed line items and adjustments for a fiscal year.
/// Used for AI-generated budget proposals and Excel exports.
/// </summary>
[Description("Budget forecast result with proposed line items for next fiscal year")]
public sealed class BudgetForecastResult
{
    [JsonPropertyName("enterpriseId")] public int EnterpriseId { get; set; }
    [JsonPropertyName("enterpriseName")] public string EnterpriseName { get; set; } = string.Empty;
    [JsonPropertyName("currentFiscalYear")] public int CurrentFiscalYear { get; set; }
    [JsonPropertyName("proposedFiscalYear")] public int ProposedFiscalYear { get; set; }
    [JsonPropertyName("totalCurrentBudget")] public decimal TotalCurrentBudget { get; set; }
    [JsonPropertyName("totalProposedBudget")] public decimal TotalProposedBudget { get; set; }
    [JsonPropertyName("totalIncrease")] public decimal TotalIncrease { get; set; }
    [JsonPropertyName("totalIncreasePercent")] public decimal TotalIncreasePercent { get; set; }
    [JsonPropertyName("inflationRate")] public decimal InflationRate { get; set; }
    [JsonPropertyName("proposedLineItems")] public List<ProposedLineItem> ProposedLineItems { get; set; } = new();
    [JsonPropertyName("goals")] public List<string> Goals { get; set; } = new();
    [JsonPropertyName("assumptions")] public List<string> Assumptions { get; set; } = new();
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("isValid")] public bool IsValid { get; set; }
    [JsonPropertyName("generatedDate")] public DateTime GeneratedDate { get; set; } = DateTime.Now;
    [JsonPropertyName("historicalTrends")] public List<HistoricalBudgetYear>? HistoricalTrends { get; set; }
}

/// <summary>
/// A proposed budget line item for next fiscal year with justification.
/// </summary>
[Description("A proposed budget line item for next fiscal year")]
public sealed class ProposedLineItem
{
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("currentAmount")] public decimal CurrentAmount { get; set; }
    [JsonPropertyName("proposedAmount")] public decimal ProposedAmount { get; set; }
    [JsonPropertyName("increase")] public decimal Increase { get; set; }
    [JsonPropertyName("increasePercent")] public decimal IncreasePercent { get; set; }
    [JsonPropertyName("justification")] public string Justification { get; set; } = string.Empty;
    [JsonPropertyName("isGoalDriven")] public bool IsGoalDriven { get; set; }
}

/// <summary>
/// Historical budget data for a single fiscal year (for trend analysis).
/// </summary>
[Description("Historical budget total for a fiscal year")]
public sealed class HistoricalBudgetYear
{
    [JsonPropertyName("fiscalYear")] public int FiscalYear { get; set; }
    [JsonPropertyName("totalBudget")] public decimal TotalBudget { get; set; }
    [JsonPropertyName("yearOverYearChange")] public decimal YearOverYearChange { get; set; }
    [JsonPropertyName("yearOverYearPercent")] public decimal YearOverYearPercent { get; set; }
}
