using System;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// No-op Grok Supercomputer service used in development/testing when AI services are not configured.
/// Prevents startup failures by providing predictable stub responses.
/// </summary>
public class NullGrokSupercomputer : IGrokSupercomputer
{
    public Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "")
        => Task.FromResult(new ReportData
        {
            Title = "[Dev Stub] Enterprise Data Report",
            GeneratedAt = DateTime.Now,
            BudgetSummary = new BudgetVarianceAnalysis(),
            VarianceAnalysis = new BudgetVarianceAnalysis(),
            Departments = new System.Collections.ObjectModel.ObservableCollection<DepartmentSummary>(),
            Funds = new System.Collections.ObjectModel.ObservableCollection<FundSummary>()
        });

    public Task<AnalyticsData> RunReportCalcsAsync(ReportData data)
        => Task.FromResult(new AnalyticsData());

    public Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget)
        => Task.FromResult(new BudgetInsights());

    public Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise)
        => Task.FromResult(new ComplianceReport());

    public Task<string> AnalyzeMunicipalDataAsync(object data, string context)
        => Task.FromResult("[Dev Stub] Municipal data analysis is disabled in development. Configure AI services to enable.");

    public Task<string> GenerateRecommendationsAsync(object data)
        => Task.FromResult("[Dev Stub] Recommendation generation is disabled in development.");

    public Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> accounts, BudgetData budget)
        => Task.FromResult("[Dev Stub] Municipal accounts analysis is disabled in development. Configure AI services to enable.");

    public Task<string> QueryAsync(string prompt)
        => Task.FromResult("[Dev Stub] AI query is disabled in development. Configure AI services to enable.");
}
