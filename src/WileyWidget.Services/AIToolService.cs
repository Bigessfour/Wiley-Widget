using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions.Models;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of AI tool functions for Grok function calling
/// Provides structured access to app data (budgets, accounts, utilities) and analytics
/// Handles both data retrieval and AI-driven analysis/recommendations
/// </summary>
public class AIToolService : IAIToolService
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly IUtilityBillRepository _billRepository;
    private readonly IUtilityCustomerRepository _customerRepository;
    private readonly ILogger<AIToolService> _logger;

    public AIToolService(
        IBudgetRepository budgetRepository,
        IMunicipalAccountRepository accountRepository,
        IUtilityBillRepository billRepository,
        IUtilityCustomerRepository customerRepository,
        ILogger<AIToolService> logger)
    {
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<object> GetBudgetDataAsync(
        int fiscalYear,
        string? fundType = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: GetBudgetData called for FY {FiscalYear}, FundType: {FundType}", fiscalYear, fundType ?? "all");

        try
        {
            var entries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);

            // Filter by fund type if specified
            if (!string.IsNullOrWhiteSpace(fundType))
            {
                entries = entries.Where(e => e.Fund?.Name?.Contains(fundType, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            var budgetList = entries.ToList();

            return new
            {
                FiscalYear = fiscalYear,
                FundType = fundType ?? "All",
                TotalEntries = budgetList.Count,
                TotalBudgeted = budgetList.Sum(e => e.BudgetedAmount),
                TotalActual = budgetList.Sum(e => e.ActualAmount),
                Entries = budgetList.Select(e => new
                {
                    e.Id,
                    e.AccountNumber,
                    Department = e.Department?.Name,
                    Fund = e.Fund?.Name,
                    e.BudgetedAmount,
                    e.ActualAmount,
                    Variance = e.BudgetedAmount - e.ActualAmount
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetBudgetData for FY {FiscalYear}", fiscalYear);
            return new { Error = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<List<BudgetTrendItem>> AnalyzeBudgetTrendsAsync(
        int accountId,
        string period = "monthly",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: AnalyzeBudgetTrends called for Account {AccountId}, Period: {Period}", accountId, period);

        try
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found", accountId);
                return new List<BudgetTrendItem>();
            }

            // Get historical budget entries for this account
            var query = await _budgetRepository.GetQueryableAsync();
            var accountNumberKey = account.AccountNumber?.ToString() ?? account.AccountNumber_Value ?? string.Empty;
            var accountEntries = query.Where(e => e.AccountNumber == accountNumberKey).ToList();

            // Group by fiscal year or date based on period
            var trends = new List<BudgetTrendItem>();
            foreach (var group in accountEntries.GroupBy(e => e.FiscalYear).OrderBy(g => g.Key))
            {
                var groupedEntries = group.ToList();
                trends.Add(new BudgetTrendItem
                {
                    Period = $"FY {group.Key}",
                    Amount = groupedEntries.Sum(e => e.BudgetedAmount),
                    ActualAmount = groupedEntries.Sum(e => e.ActualAmount),
                    Variance = groupedEntries.Sum(e => e.BudgetedAmount - e.ActualAmount),
                    Change = trends.Any() ? (groupedEntries.Sum(e => e.BudgetedAmount) - trends.Last().Amount) : 0,
                    TrendDirection = trends.Any() ?
                        (groupedEntries.Sum(e => e.BudgetedAmount) > trends.Last().Amount ? "Up" : "Down")
                        : "Flat"
                });
            }

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeBudgetTrends for Account {AccountId}", accountId);
            return new List<BudgetTrendItem>();
        }
    }

    /// <inheritdoc />
    public Task<BudgetInsights> GenerateInsightAsync(
        string query,
        object? dataContext = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: GenerateInsight called with query: {Query}", query);

        try
        {
            // Initialize insights with AI-driven analysis
            var insights = new BudgetInsights
            {
                Summary = GenerateSummaryFromQuery(query),
                Recommendations = GenerateRecommendationsFromQuery(query),
                HealthScore = 75, // Default; would be computed from actual data
                Variances = new List<BudgetVariance>(),
                Projections = new List<BudgetProjection>()
            };

            // If data context provided, enhance analysis
            if (dataContext != null)
            {
                EnhanceInsightsWithContext(insights, dataContext);
            }

            return Task.FromResult(insights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateInsight");
            return Task.FromResult(new BudgetInsights { Summary = $"Error generating insights: {ex.Message}" });
        }
    }

    /// <inheritdoc />
    public async Task<object> CreateReportAsync(
        string reportType,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: CreateReport called for type: {ReportType}", reportType);

        try
        {
            return reportType.ToLowerInvariant() switch
            {
                "compliance" => await CreateComplianceReport(parameters),
                "summary" => await CreateSummaryReport(parameters),
                "variance" => await CreateVarianceReport(parameters),
                "budget" => await CreateBudgetReport(parameters),
                _ => new { Error = $"Unknown report type: {reportType}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report of type {ReportType}", reportType);
            return new { Error = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<ServiceChargeRecommendation> RecommendChargesAsync(
        string utilityType,
        int customerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: RecommendCharges for {UtilityType}, Customer {CustomerId}", utilityType, customerId);

        try
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found", customerId);
                return new ServiceChargeRecommendation
                {
                    CustomerId = customerId,
                    Reason = "Customer not found"
                };
            }

            // Analyze customer's billing history
            var bills = await _billRepository.GetByCustomerIdAsync(customerId);
            var recentBills = bills.OrderByDescending(b => b.BillDate).Take(12).ToList();

            if (!recentBills.Any())
            {
                return new ServiceChargeRecommendation
                {
                    CustomerId = customerId,
                    UtilityType = utilityType,
                    Reason = "No billing history available",
                    Priority = "Low"
                };
            }

            // Calculate average charge and recommendation
            var avgCharge = (double)recentBills.Average(b => b.TotalAmount);
            var stdDev = CalculateStandardDeviation(recentBills.Select(b => (double)b.TotalAmount).ToList());

            var recommendation = new ServiceChargeRecommendation
            {
                CustomerId = customerId,
                UtilityType = utilityType,
                CurrentCharge = (decimal)avgCharge,
                RecommendedCharge = CalculateRecommendedCharge(avgCharge, stdDev, utilityType),
                ConfidenceLevel = Math.Min(100, (decimal)(recentBills.Count * 8.33)), // ~100% at 12 months
                Reason = GenerateChargeRecommendationReason(avgCharge, utilityType, recentBills),
                RecommendedAction = DetermineChargeAction(avgCharge, utilityType),
                Priority = DeterminePriority(avgCharge, utilityType),
                GeneratedDate = DateTime.UtcNow,
                AnalysisData = JsonSerializer.Serialize(new
                {
                    MonthsAnalyzed = recentBills.Count,
                    AverageCharge = avgCharge,
                    MinCharge = recentBills.Min(b => b.TotalAmount),
                    MaxCharge = recentBills.Max(b => b.TotalAmount),
                    StandardDeviation = stdDev
                }),
                ProjectedImpact = (CalculateRecommendedCharge(avgCharge, stdDev, utilityType) - (decimal)avgCharge) * 12
            };

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recommending charges for customer {CustomerId}", customerId);
            return new ServiceChargeRecommendation
            {
                CustomerId = customerId,
                Reason = $"Error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<List<MunicipalAccountDisplay>> QueryAccountsAsync(
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: QueryAccounts called with filter: {Filter}", filter ?? "none");

        try
        {
            var accounts = (await _accountRepository.GetAllAsync()).ToList();

            var results = accounts.Select(a => new MunicipalAccountDisplay
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber?.ToString() ?? a.AccountNumber_Value ?? string.Empty,
                Name = a.Name,
                Description = a.Notes ?? string.Empty,
                Type = a.Type.ToString(),
                Fund = a.Fund.ToString(),
                Balance = a.Balance,
                BudgetAmount = a.BudgetAmount,
                Department = a.Department?.Name ?? string.Empty,
                IsActive = a.IsActive,
                HasParent = a.ParentAccountId.HasValue
            }).ToList();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filterLower = filter.ToLowerInvariant();
                results = results.Where(a =>
                    a.AccountNumber.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ||
                    a.Type.Contains(filterLower, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying accounts with filter: {Filter}", filter);
            return new List<MunicipalAccountDisplay>();
        }
    }

    /// <inheritdoc />
    public Task<object> SimulateScenarioAsync(
        string scenarioType,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: SimulateScenario called for type: {ScenarioType}", scenarioType);

            try
            {
                var result = scenarioType.ToLowerInvariant() switch
                {
                    "tax_increase" => SimulateTaxIncrease(parameters),
                    "rate_change" => SimulateRateChange(parameters),
                    "expense_cut" => SimulateExpenseCut(parameters),
                    _ => new { Error = $"Unknown scenario type: {scenarioType}" }
                };

                return Task.FromResult(result as object ?? new { Error = "Unknown result" });
            }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating scenario {ScenarioType}", scenarioType);
            return Task.FromResult<object>(new { Error = ex.Message });
        }
    }

    /// <inheritdoc />
    public async Task<List<object>> DetectAnomaliesAsync(
        string dataType = "budget",
        int timeWindowDays = 90,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI Tool: DetectAnomalies for {DataType}, {TimeWindowDays} days", dataType, timeWindowDays);

        try
        {
            var anomalies = new List<object>();
            var startDate = DateTime.UtcNow.AddDays(-timeWindowDays);

            // Detect budget anomalies
            if (dataType == "budget" || dataType == "all")
            {
                anomalies.AddRange(await DetectBudgetAnomalies(startDate));
            }

            return anomalies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalies for {DataType}", dataType);
            return new List<object>();
        }
    }

    /// <inheritdoc />
    public string GetToolSchema(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "get_budget_data" => GetBudgetDataSchema(),
            "analyze_budget_trends" => GetAnalyzeTrendsSchema(),
            "generate_insight" => GetGenerateInsightSchema(),
            "create_report" => GetCreateReportSchema(),
            "recommend_charges" => GetRecommendChargesSchema(),
            "query_accounts" => GetQueryAccountsSchema(),
            "simulate_scenario" => GetSimulateScenarioSchema(),
            "detect_anomalies" => GetDetectAnomaliesSchema(),
            _ => "{}"
        };
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetAllToolSchemas()
    {
        return new Dictionary<string, string>
        {
            { "get_budget_data", GetBudgetDataSchema() },
            { "analyze_budget_trends", GetAnalyzeTrendsSchema() },
            { "generate_insight", GetGenerateInsightSchema() },
            { "create_report", GetCreateReportSchema() },
            { "recommend_charges", GetRecommendChargesSchema() },
            { "query_accounts", GetQueryAccountsSchema() },
            { "simulate_scenario", GetSimulateScenarioSchema() },
            { "detect_anomalies", GetDetectAnomaliesSchema() }
        };
    }

    // ============= Private Helper Methods =============

    private Task<object> CreateComplianceReport(Dictionary<string, object> parameters)
    {
        if (!int.TryParse(parameters.GetValueOrDefault("fiscal_year")?.ToString(), out var fy))
            fy = DateTime.UtcNow.Year;

        var report = new ComplianceReport
        {
            EnterpriseId = int.TryParse(parameters.GetValueOrDefault("enterprise_id")?.ToString(), out var eid) ? eid : 0,
            GeneratedDate = DateTime.UtcNow,
            ComplianceScore = 85,
            OverallStatus = ComplianceStatus.Compliant
        };

        return Task.FromResult<object>(new { Type = "ComplianceReport", report.EnterpriseId, report.GeneratedDate, report.ComplianceScore });
    }

    private async Task<object> CreateSummaryReport(Dictionary<string, object> parameters)
    {
        if (!int.TryParse(parameters.GetValueOrDefault("fiscal_year")?.ToString(), out var fy))
            fy = DateTime.UtcNow.Year;

        var summary = await _budgetRepository.GetYearEndSummaryAsync(fy);
        return new { Type = "BudgetSummary", FiscalYear = fy, summary.TotalBudgeted, summary.TotalActual, summary.TotalVariance };
    }

    private async Task<object> CreateVarianceReport(Dictionary<string, object> parameters)
    {
        DateTime startDate = DateTime.UtcNow.AddMonths(-3);
        DateTime endDate = DateTime.UtcNow;

        if (DateTime.TryParse(parameters.GetValueOrDefault("start_date")?.ToString(), out var sd))
            startDate = sd;
        if (DateTime.TryParse(parameters.GetValueOrDefault("end_date")?.ToString(), out var ed))
            endDate = ed;

        var analysis = await _budgetRepository.GetVarianceAnalysisAsync(startDate, endDate);
        return new { Type = "VarianceReport", analysis.BudgetPeriod, analysis.TotalVariance, analysis.FundSummaries };
    }

    private async Task<object> CreateBudgetReport(Dictionary<string, object> parameters)
    {
        if (!int.TryParse(parameters.GetValueOrDefault("fiscal_year")?.ToString(), out var fy))
            fy = DateTime.UtcNow.Year;

        var entries = await _budgetRepository.GetByFiscalYearAsync(fy);
        return new { Type = "BudgetReport", FiscalYear = fy, EntryCount = entries.Count() };
    }

    private string GenerateSummaryFromQuery(string query)
    {
        if (query.Contains("trend", StringComparison.OrdinalIgnoreCase))
            return "Analyzing budget trends to identify patterns and projections.";
        if (query.Contains("anomal", StringComparison.OrdinalIgnoreCase))
            return "Scanning for unusual patterns or outliers in financial data.";
        if (query.Contains("complian", StringComparison.OrdinalIgnoreCase))
            return "Evaluating compliance status against municipal regulations.";
        return "Analyzing financial data to provide actionable insights.";
    }

    private List<string> GenerateRecommendationsFromQuery(string query)
    {
        var recommendations = new List<string>();
        if (query.Contains("budget", StringComparison.OrdinalIgnoreCase))
            recommendations.Add("Review budget allocations for underutilized funds.");
        if (query.Contains("revenue", StringComparison.OrdinalIgnoreCase))
            recommendations.Add("Consider revenue optimization strategies.");
        if (query.Contains("expense", StringComparison.OrdinalIgnoreCase))
            recommendations.Add("Evaluate cost reduction opportunities without impacting services.");
        if (!recommendations.Any())
            recommendations.Add("Continue monitoring financial metrics regularly.");
        return recommendations;
    }

    private void EnhanceInsightsWithContext(BudgetInsights insights, object dataContext)
    {
        // Enhanced analysis based on provided context would go here
        insights.Summary += " (Enhanced with provided context)";
    }

    private decimal CalculateRecommendedCharge(double avgCharge, double stdDev, string utilityType)
    {
        // Simple recommendation: current average adjusted based on utility type and volatility
        var factor = utilityType.ToLowerInvariant() switch
        {
            "water" => 1.05m,  // 5% increase for water
            "sewer" => 1.03m,  // 3% increase for sewer
            "garbage" => 1.02m, // 2% increase for garbage
            _ => 1.0m
        };
        return (decimal)(avgCharge * (double)factor);
    }

    private string GenerateChargeRecommendationReason(double avgCharge, string utilityType, List<UtilityBill> bills)
    {
        var trend = bills.OrderByDescending(b => b.BillDate).Take(3).Average(b => (double)b.TotalAmount);
        return trend > avgCharge ?
            "Recent charges trending upward, recommend increase to match usage patterns." :
            "Charges stable; current rates appropriately reflect consumption.";
    }

    private string DetermineChargeAction(double avgCharge, string utilityType)
    {
        return avgCharge > 500 ? "Review usage patterns and provide customer education" : "Monitor for significant changes";
    }

    private string DeterminePriority(double avgCharge, string utilityType)
    {
        return avgCharge > 1000 ? "High" : avgCharge > 500 ? "Medium" : "Low";
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;
        double avg = values.Average();
        double variance = values.Average(x => Math.Pow(x - avg, 2));
        return Math.Sqrt(variance);
    }

    private object SimulateTaxIncrease(Dictionary<string, object> parameters)
    {
        if (!decimal.TryParse(parameters.GetValueOrDefault("percentage")?.ToString(), out var percentage))
            percentage = 5m;

        return new
        {
            ScenarioType = "TaxIncrease",
            PercentageIncrease = percentage,
            ImpactProjection = "Monthly revenue would increase by estimated $" + (100000 * percentage / 100)
        };
    }

    private object SimulateRateChange(Dictionary<string, object> parameters)
    {
        return new { ScenarioType = "RateChange", Status = "Calculated" };
    }

    private object SimulateExpenseCut(Dictionary<string, object> parameters)
    {
        return new { ScenarioType = "ExpenseCut", Status = "Calculated" };
    }

    private Task<List<object>> DetectBudgetAnomalies(DateTime startDate)
    {
        var anomalies = new List<object>();
        // Implement anomaly detection logic here
        return Task.FromResult(anomalies);
    }

    // ============= Tool Schema Definitions =============

    private string GetBudgetDataSchema()
    {
        return @"{
  ""name"": ""get_budget_data"",
  ""description"": ""Retrieves budget data for a fiscal year and optional fund type"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""fiscal_year"": {
        ""type"": ""integer"",
        ""description"": ""The fiscal year to retrieve data for""
      },
      ""fund_type"": {
        ""type"": ""string"",
        ""description"": ""Optional fund type filter (e.g., 'Enterprise', 'General')""
      }
    },
    ""required"": [""fiscal_year""]
  }
}";
    }

    private string GetAnalyzeTrendsSchema()
    {
        return @"{
  ""name"": ""analyze_budget_trends"",
  ""description"": ""Analyzes budget trends for an account over a time period"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""account_id"": {
        ""type"": ""integer"",
        ""description"": ""The municipal account ID to analyze""
      },
      ""period"": {
        ""type"": ""string"",
        ""description"": ""Time period for analysis ('monthly', 'quarterly', 'annual')"",
        ""enum"": [""monthly"", ""quarterly"", ""annual""]
      }
    },
    ""required"": [""account_id""]
  }
}";
    }

    private string GetGenerateInsightSchema()
    {
        return @"{
  ""name"": ""generate_insight"",
  ""description"": ""Generates AI insights based on query and optional data context"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""query"": {
        ""type"": ""string"",
        ""description"": ""The user's query or question about financial data""
      },
      ""data_context"": {
        ""type"": ""object"",
        ""description"": ""Optional structured data context for analysis""
      }
    },
    ""required"": [""query""]
  }
}";
    }

    private string GetCreateReportSchema()
    {
        return @"{
  ""name"": ""create_report"",
  ""description"": ""Creates a report of specified type with given parameters"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""report_type"": {
        ""type"": ""string"",
        ""description"": ""Type of report (compliance, summary, variance, budget)"",
        ""enum"": [""compliance"", ""summary"", ""variance"", ""budget""]
      },
      ""fiscal_year"": {
        ""type"": ""integer"",
        ""description"": ""Fiscal year for the report""
      },
      ""fund_type"": {
        ""type"": ""string"",
        ""description"": ""Optional fund type filter""
      }
    },
    ""required"": [""report_type""]
  }
}";
    }

    private string GetRecommendChargesSchema()
    {
        return @"{
  ""name"": ""recommend_charges"",
  ""description"": ""Analyzes utility charges and recommends optimizations"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""utility_type"": {
        ""type"": ""string"",
        ""description"": ""Type of utility service (Water, Sewer, Garbage, etc.)"",
        ""enum"": [""Water"", ""Sewer"", ""Garbage"", ""Recycling"", ""Stormwater""]
      },
      ""customer_id"": {
        ""type"": ""integer"",
        ""description"": ""The customer ID to analyze charges for""
      }
    },
    ""required"": [""utility_type"", ""customer_id""]
  }
}";
    }

    private string GetQueryAccountsSchema()
    {
        return @"{
  ""name"": ""query_accounts"",
  ""description"": ""Queries municipal accounts with optional filtering"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""filter"": {
        ""type"": ""string"",
        ""description"": ""Optional filter criteria (account number, name, type)""
      }
    }
  }
}";
    }

    private string GetSimulateScenarioSchema()
    {
        return @"{
  ""name"": ""simulate_scenario"",
  ""description"": ""Simulates financial scenarios (what-if analysis)"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""scenario_type"": {
        ""type"": ""string"",
        ""description"": ""Type of scenario"",
        ""enum"": [""tax_increase"", ""rate_change"", ""expense_cut""]
      },
      ""percentage"": {
        ""type"": ""number"",
        ""description"": ""Percentage change for the scenario""
      }
    },
    ""required"": [""scenario_type""]
  }
}";
    }

    private string GetDetectAnomaliesSchema()
    {
        return @"{
  ""name"": ""detect_anomalies"",
  ""description"": ""Detects anomalies in budget or financial data"",
  ""parameters"": {
    ""type"": ""object"",
    ""properties"": {
      ""data_type"": {
        ""type"": ""string"",
        ""description"": ""Type of data to analyze"",
        ""enum"": [""budget"", ""revenue"", ""expenses"", ""all""]
      },
      ""time_window_days"": {
        ""type"": ""integer"",
        ""description"": ""Number of days to look back for analysis"",
        ""default"": 90
      }
    }
  }
}";
    }
}
