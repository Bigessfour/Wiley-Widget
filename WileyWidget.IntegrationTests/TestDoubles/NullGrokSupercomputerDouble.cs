using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.IntegrationTests.TestDoubles;

/// <summary>
/// Test double for IGrokSupercomputer - returns empty/stub data structures.
/// </summary>
public class NullGrokSupercomputerDouble : IGrokSupercomputer
{
    public Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "")
        => Task.FromResult(new ReportData
        {
            Title = "[Test Stub] Enterprise Data Report",
            GeneratedAt = DateTime.Now,
            BudgetSummary = new BudgetVarianceAnalysis(),
            VarianceAnalysis = new BudgetVarianceAnalysis(),
            Departments = new ObservableCollection<DepartmentSummary>(),
            Funds = new ObservableCollection<FundSummary>()
        });

    public Task<AnalyticsData> RunReportCalcsAsync(ReportData data)
        => Task.FromResult(new AnalyticsData
        {
            Categories = new List<string>(),
            ChartData = new Dictionary<string, double>(),
            SummaryStats = new Dictionary<string, double>()
        });

    public Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget)
        => Task.FromResult(new BudgetInsights
        {
            Variances = new List<BudgetVariance>(),
            Projections = new List<BudgetProjection>(),
            Recommendations = new List<string> { "[Test Stub] Analysis disabled in integration tests" },
            HealthScore = 100
        });

    public Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise)
        => Task.FromResult(new ComplianceReport
        {
            EnterpriseId = enterprise?.Id ?? 0,
            GeneratedDate = DateTime.Now,
            OverallStatus = ComplianceStatus.Compliant,
            ComplianceScore = 100,
            Violations = new List<ComplianceViolation>(),
            Recommendations = new List<string>()
        });

    public Task<string> AnalyzeMunicipalDataAsync(object data, string context)
        => Task.FromResult("[Test Stub] Municipal data analysis disabled in integration tests.");

    public Task<string> GenerateRecommendationsAsync(object data)
        => Task.FromResult("[Test Stub] Recommendations disabled in integration tests.");

    public Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> accounts, BudgetData budget)
        => Task.FromResult("[Test Stub] Municipal accounts analysis disabled in integration tests.");

    public Task<string> QueryAsync(string prompt)
        => Task.FromResult("[Test Stub] AI query disabled in integration tests.");
}
