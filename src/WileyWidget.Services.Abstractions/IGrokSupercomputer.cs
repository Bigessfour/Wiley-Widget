using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Interface for Grok Supercomputer AI services providing municipal utility analytics and compliance reporting.
    /// This interface defines the contract for AI-powered operations in municipal finance management,
    /// including enterprise data retrieval, analytical calculations, budget analysis, and regulatory compliance.
    /// </summary>
    public interface IGrokSupercomputer
    {
        Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "", CancellationToken cancellationToken = default);
        Task<AnalyticsData> RunReportCalcsAsync(ReportData data, CancellationToken cancellationToken = default);
        Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget, CancellationToken cancellationToken = default);
        Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise, CancellationToken cancellationToken = default);
        Task<string> AnalyzeMunicipalDataAsync(object data, string context, CancellationToken cancellationToken = default);
        Task<string> GenerateRecommendationsAsync(object data, CancellationToken cancellationToken = default);
        Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> municipalAccounts, BudgetData budgetData, CancellationToken cancellationToken = default);
        Task<string> QueryAsync(string prompt, CancellationToken cancellationToken = default);
        System.Collections.Generic.IAsyncEnumerable<string> StreamQueryAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
