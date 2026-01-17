using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public class NullGrokSupercomputer : IGrokSupercomputer
{
    public Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "", CancellationToken cancellationToken = default)
        => Task.FromResult(new ReportData());

    public Task<AnalyticsData> RunReportCalcsAsync(ReportData data, CancellationToken cancellationToken = default)
        => Task.FromResult(new AnalyticsData());

    public Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget, CancellationToken cancellationToken = default)
        => Task.FromResult(new BudgetInsights());

    public Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise, CancellationToken cancellationToken = default)
        => Task.FromResult(new ComplianceReport());

    public Task<string> AnalyzeMunicipalDataAsync(object data, string context, CancellationToken cancellationToken = default)
        => Task.FromResult("Municipal data analysis is currently unavailable.");

    public Task<string> GenerateRecommendationsAsync(object data, CancellationToken cancellationToken = default)
        => Task.FromResult("Recommendations are currently unavailable.");

    public Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> municipalAccounts, BudgetData budgetData, CancellationToken cancellationToken = default)
        => Task.FromResult("Municipal account analysis is currently unavailable.");

    public Task<string> QueryAsync(string prompt, CancellationToken cancellationToken = default) => Task.FromResult("Offline: Grok is currently disconnected.");

    public async IAsyncEnumerable<string> StreamQueryAsync(string prompt)
    {
        yield return "Offline: ";
        yield return "Grok ";
        yield return "is ";
        yield return "currently ";
        yield return "disconnected.";
        await Task.CompletedTask;
    }
}

