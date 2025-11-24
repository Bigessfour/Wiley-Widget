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
        Task<ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "");
        Task<AnalyticsData> RunReportCalcsAsync(ReportData data);
        Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget);
        Task<ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise);
        Task<string> AnalyzeMunicipalDataAsync(object data, string context);
        Task<string> GenerateRecommendationsAsync(object data);
        Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<MunicipalAccount> municipalAccounts, BudgetData budgetData);
        Task<string> QueryAsync(string prompt);
    }
}
