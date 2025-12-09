using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Service for analyzing financial data and generating intelligent insights.
/// Provides anomaly detection, trend analysis, risk assessment, and actionable recommendations.
/// </summary>
public class FinancialInsightsService : IFinancialInsightsService
{
    private readonly ILogger<FinancialInsightsService> _logger;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IEnterpriseRepository _enterpriseRepository;

    public FinancialInsightsService(
        ILogger<FinancialInsightsService> logger,
        IBudgetRepository budgetRepository,
        IEnterpriseRepository enterpriseRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    public async Task<FinancialInsights> AnalyzeBudgetAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive budget analysis for fiscal year {FiscalYear}", fiscalYear);

        var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
        var budgetList = budgetEntries.ToList();

        var insights = new FinancialInsights
        {
            GeneratedAt = DateTime.UtcNow
        };

        // Run analysis components in parallel for efficiency
        var anomaliesTask = Task.Run(() => DetectAnomalies(budgetList), cancellationToken);
        var trendsTask = IdentifyTrendsAsync(fiscalYear, cancellationToken);
        var risksTask = AssessRisksAsync(fiscalYear, cancellationToken);
        var healthScoreTask = CalculateHealthScoreAsync(fiscalYear, cancellationToken);

        await Task.WhenAll(anomaliesTask, trendsTask, risksTask, healthScoreTask);

        insights.Anomalies = anomaliesTask.Result;
        insights.Trends = trendsTask.Result;
        insights.Risks = risksTask.Result;
        insights.HealthScore = healthScoreTask.Result;
        insights.Wins = IdentifyWins(budgetList);
        insights.Recommendations = GenerateRecommendations(insights);
        insights.ExecutiveSummary = GenerateExecutiveSummary(insights);

        _logger.LogInformation("Budget analysis complete: {AnomalyCount} anomalies, {TrendCount} trends, {RiskCount} risks, health score {HealthScore}",
            insights.Anomalies.Count, insights.Trends.Count, insights.Risks.Count, insights.HealthScore);

        return insights;
    }

    public List<BudgetAnomaly> DetectAnomalies(IEnumerable<BudgetEntry> budgetEntries)
    {
        var anomalies = new List<BudgetAnomaly>();
        var entries = budgetEntries.ToList();

        foreach (var entry in entries)
        {
            var variance = entry.ActualSpent - entry.TotalBudget;
            var variancePercent = entry.TotalBudget != 0 ? (variance / entry.TotalBudget) * 100 : 0;

            // Critical overspending (>25%)
            if (variancePercent > 25)
            {
                anomalies.Add(new BudgetAnomaly
                {
                    Entity = $"{entry.Fund?.FundCode ?? entry.AccountNumber} - {entry.Department?.Name ?? "Unknown"}",
                    AnomalyType = "Critical Overspending",
                    Severity = "Critical",
                    VarianceAmount = variance,
                    VariancePercent = variancePercent,
                    Description = $"Critical budget overrun: {variancePercent:F1}% over allocated budget",
                    HistoricalContext = variancePercent > 50 ? "Unprecedented overage level" : "Significantly above normal variance threshold"
                });
            }
            // High overspending (15-25%)
            else if (variancePercent > 15)
            {
                anomalies.Add(new BudgetAnomaly
                {
                    Entity = $"{entry.Fund?.FundCode ?? entry.AccountNumber} - {entry.Department?.Name ?? "Unknown"}",
                    AnomalyType = "High Overspending",
                    Severity = "High",
                    VarianceAmount = variance,
                    VariancePercent = variancePercent,
                    Description = $"Significant overspending: {variancePercent:F1}% over budget",
                    HistoricalContext = "Requires immediate management attention"
                });
            }
            // Unusual underspending (>30% under budget)
            else if (variancePercent < -30)
            {
                anomalies.Add(new BudgetAnomaly
                {
                    Entity = $"{entry.Fund?.FundCode ?? entry.AccountNumber} - {entry.Department?.Name ?? "Unknown"}",
                    AnomalyType = "Unusual Underspending",
                    Severity = "Medium",
                    VarianceAmount = variance,
                    VariancePercent = variancePercent,
                    Description = $"Significantly under budget: {Math.Abs(variancePercent):F1}% unspent",
                    HistoricalContext = "May indicate project delays or budget overallocation"
                });
            }
        }

        _logger.LogInformation("Detected {Count} budget anomalies", anomalies.Count);
        return anomalies.OrderByDescending(a => Math.Abs(a.VariancePercent)).ToList();
    }

    public async Task<List<FinancialTrend>> IdentifyTrendsAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var trends = new List<FinancialTrend>();

        try
        {
            // Get current and previous year data
            var currentYearData = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
            var currentList = currentYearData.ToList();
            var previousYearData = await _budgetRepository.GetByFiscalYearAsync(fiscalYear - 1);
            var previousList = previousYearData.ToList();

            if (!previousList.Any())
            {
                _logger.LogInformation("No previous year data available for trend analysis");
                return trends;
            }

            // Calculate aggregate trends
            var currentTotal = currentList.Sum(e => e.ActualSpent);
            var previousTotal = previousList.Sum(e => e.ActualSpent);
            var spendingChange = previousTotal != 0 ? ((currentTotal - previousTotal) / previousTotal) * 100 : 0;

            if (Math.Abs(spendingChange) > 5)
            {
                trends.Add(new FinancialTrend
                {
                    Category = "Overall Spending",
                    Direction = spendingChange > 0 ? "Rising" : "Falling",
                    RateOfChange = $"{spendingChange:+0.0;-0.0}% YoY",
                    Description = $"Total spending has {(spendingChange > 0 ? "increased" : "decreased")} by {Math.Abs(spendingChange):F1}% compared to last fiscal year",
                    Confidence = 95,
                    ProjectedImpact = Math.Abs(spendingChange) > 15 
                        ? "Significant budget pressure expected if trend continues" 
                        : "Moderate impact on future budget planning"
                });
            }

            // Department-level trends
            var departmentTrends = currentList
                .Where(e => e.Department != null)
                .GroupBy(e => e.Department.Name)
                .Select(g => new
                {
                    Department = g.Key,
                    CurrentSpend = g.Sum(e => e.ActualSpent),
                    PreviousSpend = previousList.Where(p => p.Department != null && p.Department.Name == g.Key).Sum(e => e.ActualSpent)
                })
                .Where(d => d.PreviousSpend > 0)
                .Select(d => new
                {
                    d.Department,
                    Change = ((d.CurrentSpend - d.PreviousSpend) / d.PreviousSpend) * 100
                })
                .Where(d => Math.Abs(d.Change) > 10)
                .OrderByDescending(d => Math.Abs(d.Change))
                .Take(5);

            foreach (var deptTrend in departmentTrends)
            {
                trends.Add(new FinancialTrend
                {
                    Category = $"{deptTrend.Department} Spending",
                    Direction = deptTrend.Change > 0 ? "Rising" : "Falling",
                    RateOfChange = $"{deptTrend.Change:+0.0;-0.0}% YoY",
                    Description = $"{deptTrend.Department} spending {(deptTrend.Change > 0 ? "increased" : "decreased")} {Math.Abs(deptTrend.Change):F1}% year-over-year",
                    Confidence = 85,
                    ProjectedImpact = Math.Abs(deptTrend.Change) > 20 ? "Recommend departmental budget review" : "Monitor for continued pattern"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying financial trends");
        }

        _logger.LogInformation("Identified {Count} financial trends", trends.Count);
        return trends;
    }

    public async Task<List<FinancialRisk>> AssessRisksAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var risks = new List<FinancialRisk>();

        try
        {
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
            var entries = budgetEntries.ToList();

            // Calculate current fiscal year progress (assumes July 1 start)
            var fiscalYearStart = new DateTime(fiscalYear, 7, 1);
            var fiscalYearEnd = fiscalYearStart.AddYears(1).AddDays(-1);
            var today = DateTime.Now;
            var daysIntoYear = (today - fiscalYearStart).TotalDays;
            var totalDaysInYear = (fiscalYearEnd - fiscalYearStart).TotalDays;
            var yearProgress = Math.Max(0, Math.Min(1, daysIntoYear / totalDaysInYear));

            foreach (var entry in entries)
            {
                var budgetUtilization = entry.TotalBudget != 0 ? entry.ActualSpent / entry.TotalBudget : 0;

                // Budget depletion risk
                if (budgetUtilization > 0.9m && yearProgress < 0.75)
                {
                    var remainingDays = (fiscalYearEnd - today).TotalDays;
                    risks.Add(new FinancialRisk
                    {
                        RiskType = "Budget Depletion",
                        Severity = budgetUtilization > 1.0m ? "Critical" : "High",
                        AffectedEntity = $"{entry.Fund?.FundCode ?? entry.AccountNumber} - {entry.Department?.Name ?? "Unknown"}",
                        Description = $"Budget {budgetUtilization:P0} depleted with {remainingDays:F0} days remaining in fiscal year",
                        TimeToImpact = budgetUtilization > 1.0m ? "Already exceeded" : $"~{(remainingDays * (double)(1.0m - budgetUtilization) / (double)(budgetUtilization - (decimal)yearProgress)):F0} days to depletion",
                        MitigationActions = new List<string>
                        {
                            "Review remaining essential expenses",
                            "Identify potential spending cuts or deferrals",
                            "Request budget supplement if justified",
                            "Implement spending freeze for non-critical items"
                        }
                    });
                }

                // Rapid burn rate risk
                if (yearProgress > 0.25 && budgetUtilization > (decimal)yearProgress * 1.5m)
                {
                    risks.Add(new FinancialRisk
                    {
                        RiskType = "Uncontrolled Spending",
                        Severity = "High",
                        AffectedEntity = $"{entry.Fund?.FundCode ?? entry.AccountNumber} - {entry.Department?.Name ?? "Unknown"}",
                        Description = $"Spending at {((double)budgetUtilization / yearProgress):F1}x expected rate for this point in fiscal year",
                        TimeToImpact = "Current quarter",
                        MitigationActions = new List<string>
                        {
                            "Conduct immediate spending review",
                            "Identify cause of accelerated burn rate",
                            "Implement enhanced approval controls",
                            "Develop corrective action plan"
                        }
                    });
                }
            }

            // Enterprise-level cash flow risks
            var enterprises = await _enterpriseRepository.GetAllAsync();
            foreach (var enterprise in enterprises)
            {
                if (enterprise.Status == EnterpriseStatus.Active && enterprise.MonthlyRevenue < enterprise.MonthlyExpenses)
                {
                    var deficit = enterprise.MonthlyExpenses - enterprise.MonthlyRevenue;
                    risks.Add(new FinancialRisk
                    {
                        RiskType = "Negative Cash Flow",
                        Severity = deficit > enterprise.MonthlyRevenue * 0.2m ? "Critical" : "High",
                        AffectedEntity = enterprise.Name,
                        Description = $"Monthly expenses ({enterprise.MonthlyExpenses:C}) exceed revenue ({enterprise.MonthlyRevenue:C}) by {deficit:C}",
                        TimeToImpact = "Ongoing",
                        MitigationActions = new List<string>
                        {
                            "Review rate structure for potential adjustment",
                            "Identify cost reduction opportunities",
                            "Analyze revenue collection efficiency",
                            "Consider operational consolidation"
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing financial risks");
        }

        _logger.LogInformation("Identified {Count} financial risks", risks.Count);
        return risks.OrderByDescending(r => r.Severity).ToList();
    }

    public List<string> GenerateRecommendations(FinancialInsights insights)
    {
        var recommendations = new List<string>();

        // Based on health score
        if (insights.HealthScore < 50)
        {
            recommendations.Add("🚨 URGENT: Schedule executive budget review within 48 hours—financial health is critically low");
        }
        else if (insights.HealthScore < 70)
        {
            recommendations.Add("⚠️ Recommend comprehensive spending review and corrective action plan");
        }

        // Based on anomalies
        var criticalAnomalies = insights.Anomalies.Where(a => a.Severity == "Critical").ToList();
        if (criticalAnomalies.Any())
        {
            recommendations.Add($"🔴 Address {criticalAnomalies.Count} critical budget overruns immediately—focus on {criticalAnomalies.First().Entity}");
        }

        // Based on trends
        var risingExpenses = insights.Trends.FirstOrDefault(t => t.Category.Contains("Overall") && t.Direction == "Rising");
        if (risingExpenses != null)
        {
            recommendations.Add($"📈 Rising spending trend detected ({risingExpenses.RateOfChange})—implement cost containment measures");
        }

        // Based on risks
        var criticalRisks = insights.Risks.Where(r => r.Severity == "Critical").ToList();
        if (criticalRisks.Any())
        {
            foreach (var risk in criticalRisks.Take(2))
            {
                recommendations.Add($"🔥 {risk.RiskType} risk for {risk.AffectedEntity}: {risk.MitigationActions.FirstOrDefault() ?? "Immediate action required"}");
            }
        }

        // Positive recommendations
        if (insights.HealthScore >= 80 && insights.Wins.Any())
        {
            recommendations.Add("✨ Strong fiscal performance—consider investing surplus in strategic initiatives or reserve fund");
        }

        if (!recommendations.Any())
        {
            recommendations.Add("👍 Financial position is stable—continue current budget management practices");
        }

        return recommendations;
    }

    public List<string> IdentifyWins(IEnumerable<BudgetEntry> budgetEntries)
    {
        var wins = new List<string>();
        var entries = budgetEntries.ToList();

        // Departments significantly under budget
        var underBudgetDepts = entries
            .Where(e => e.TotalBudget > 0 && e.ActualSpent < e.TotalBudget * 0.85m)
            .OrderBy(e => e.ActualSpent / e.TotalBudget)
            .Take(3);

        foreach (var dept in underBudgetDepts)
        {
            var savingsPercent = ((dept.TotalBudget - dept.ActualSpent) / dept.TotalBudget) * 100;
            wins.Add($"🎯 {dept.Department?.Name ?? dept.AccountNumber} is {savingsPercent:F0}% under budget—excellent cost management!");
        }

        // Overall budget health
        var totalBudget = entries.Sum(e => e.TotalBudget);
        var totalSpent = entries.Sum(e => e.ActualSpent);
        if (totalBudget > 0 && totalSpent < totalBudget * 0.9m)
        {
            var overallSavings = totalBudget - totalSpent;
            wins.Add($"💰 Overall spending tracking well: {overallSavings:C0} in budget reserves");
        }

        // Balanced budgets (within 5%)
        var balancedCount = entries.Count(e => e.TotalBudget > 0 && 
            Math.Abs((e.ActualSpent - e.TotalBudget) / e.TotalBudget) < 0.05m);
        
        if (balancedCount > entries.Count * 0.5)
        {
            wins.Add($"⚖️ {balancedCount} departments operating within 5% of budget—strong planning accuracy");
        }

        return wins;
    }

    public List<SmartSuggestion> GetSmartSuggestions(string context, FinancialInsights? recentInsights = null)
    {
        var suggestions = new List<SmartSuggestion>();

        // Context-based suggestions
        if (context.Contains("budget", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new SmartSuggestion
            {
                Text = "Find departments with unusual spending patterns",
                Category = "Analysis",
                Icon = "🔍",
                Action = "analyze_spending_patterns",
                Priority = 10
            });

            suggestions.Add(new SmartSuggestion
            {
                Text = "Compare this year to last year's performance",
                Category = "Comparison",
                Icon = "📊",
                Action = "compare_fiscal_years",
                Priority = 8
            });
        }

        // Insights-based suggestions
        if (recentInsights != null)
        {
            if (recentInsights.Risks.Any(r => r.Severity == "Critical"))
            {
                suggestions.Add(new SmartSuggestion
                {
                    Text = "Show me financial risks that need attention",
                    Category = "Investigation",
                    Icon = "⚠️",
                    Action = "show_critical_risks",
                    Priority = 15
                });
            }

            if (recentInsights.Anomalies.Any())
            {
                suggestions.Add(new SmartSuggestion
                {
                    Text = "Investigate budget anomalies",
                    Category = "Investigation",
                    Icon = "🎯",
                    Action = "investigate_anomalies",
                    Priority = 12
                });
            }

            if (recentInsights.Wins.Any())
            {
                suggestions.Add(new SmartSuggestion
                {
                    Text = "Show positive achievements",
                    Category = "Analysis",
                    Icon = "🏆",
                    Action = "show_wins",
                    Priority = 5
                });
            }
        }

        // General suggestions
        suggestions.Add(new SmartSuggestion
        {
            Text = "Export financial report",
            Category = "Export",
            Icon = "📄",
            Action = "export_report",
            Priority = 3
        });

        suggestions.Add(new SmartSuggestion
        {
            Text = "Show enterprise health dashboard",
            Category = "Visualization",
            Icon = "📈",
            Action = "show_dashboard",
            Priority = 6
        });

        return suggestions.OrderByDescending(s => s.Priority).Take(5).ToList();
    }

    public async Task<int> CalculateHealthScoreAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        try
        {
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
            var entries = budgetEntries.ToList();

            if (!entries.Any())
                return 50; // Neutral score for no data

            int score = 100;

            // Penalty for overspending
            var overBudgetCount = entries.Count(e => e.ActualSpent > e.TotalBudget);
            var overBudgetPercent = entries.Any() ? (decimal)overBudgetCount / entries.Count : 0;
            score -= (int)(overBudgetPercent * 30);

            // Penalty for severe variances
            var severeVariances = entries.Count(e => e.TotalBudget > 0 && 
                Math.Abs(e.ActualSpent - e.TotalBudget) / e.TotalBudget > 0.25m);
            score -= severeVariances * 5;

            // Bonus for departments under budget
            var underBudgetCount = entries.Count(e => e.ActualSpent < e.TotalBudget * 0.95m);
            var underBudgetPercent = entries.Any() ? (decimal)underBudgetCount / entries.Count : 0;
            score += (int)(underBudgetPercent * 10);

            // Penalty for enterprises with negative cash flow
            var enterprises = await _enterpriseRepository.GetAllAsync();
            var negativeCashFlowCount = enterprises.Count(e => 
                e.Status == EnterpriseStatus.Active && e.MonthlyRevenue < e.MonthlyExpenses);
            score -= negativeCashFlowCount * 8;

            return Math.Max(0, Math.Min(100, score));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating health score");
            return 50; // Default to neutral on error
        }
    }

    private string GenerateExecutiveSummary(FinancialInsights insights)
    {
        if (insights.HealthScore >= 80)
            return $"Strong fiscal position (Score: {insights.HealthScore}/100)—budget management is effective with {insights.Wins.Count} notable achievements.";
        
        if (insights.HealthScore >= 60)
            return $"Stable financial status (Score: {insights.HealthScore}/100)—minor concerns with {insights.Anomalies.Count} variances requiring monitoring.";
        
        if (insights.HealthScore >= 40)
            return $"Concerning financial position (Score: {insights.HealthScore}/100)—{insights.Risks.Count} risks identified requiring corrective action.";
        
        return $"Critical financial situation (Score: {insights.HealthScore}/100)—immediate executive intervention required to address {insights.Risks.Count} risks and {insights.Anomalies.Count} budget anomalies.";
    }
}
