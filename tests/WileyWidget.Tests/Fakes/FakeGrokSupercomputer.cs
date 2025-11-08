using System.Threading.Tasks;
using WileyWidget.Services;
using WileyWidget.Models;
using System.Collections.Generic;

namespace WileyWidget.Fakes;

public class FakeGrokSupercomputer : IGrokSupercomputer
{
    public Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "")
    {
        return Task.FromResult(new ReportData());
    }

    public Task<AnalyticsData> RunReportCalcsAsync(ReportData data)
    {
        return Task.FromResult(new AnalyticsData());
    }

    public Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget)
    {
        return Task.FromResult(new BudgetInsights());
    }

    public Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise)
    {
        return Task.FromResult(new ComplianceReport());
    }

    public Task<string> AnalyzeMunicipalDataAsync(object data, string context)
    {
        // Return a deterministic short response for tests
        return Task.FromResult("FAKE_ANALYSIS: No issues detected in seeded data.");
    }

    public Task<string> GenerateRecommendationsAsync(object data)
    {
        return Task.FromResult("FAKE_RECOMMENDATIONS");
    }

    public Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> municipalAccounts, BudgetData budgetData)
    {
        return Task.FromResult("FAKE_MUNICIPAL_ANALYSIS");
    }

    public Task<string> QueryAsync(string prompt)
    {
        return Task.FromResult("FAKE_QUERY_RESPONSE");
    }
}
