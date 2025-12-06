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

        /// <summary>
        /// Analyzes budget data for a specific fiscal year using AI
        /// </summary>
        /// <param name="fiscalYear">The fiscal year to analyze</param>
        /// <returns>AI-generated budget insights</returns>
        Task<string> AnalyzeBudgetAsync(int fiscalYear);

        /// <summary>
        /// Analyzes enterprise data using AI
        /// </summary>
        /// <param name="enterpriseId">The enterprise ID to analyze</param>
        /// <returns>AI-generated enterprise insights</returns>
        Task<string> AnalyzeEnterpriseAsync(int enterpriseId);

        /// <summary>
        /// Analyzes audit findings using AI
        /// </summary>
        /// <param name="startDate">Optional start date for audit data</param>
        /// <param name="endDate">Optional end date for audit data</param>
        /// <returns>AI-generated audit analysis</returns>
        Task<string> AnalyzeAuditAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Analyzes all municipal accounts using AI
        /// </summary>
        /// <returns>AI-generated analysis of municipal accounts</returns>
        Task<string> AnalyzeMunicipalAccountsAsync();
    }
}
