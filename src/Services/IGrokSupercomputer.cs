using System;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for Grok Supercomputer AI services providing municipal utility analytics and compliance reporting.
    /// This interface defines the contract for AI-powered operations in municipal finance management,
    /// including enterprise data retrieval, analytical calculations, budget analysis, and regulatory compliance.
    /// </summary>
    public interface IGrokSupercomputer
    {
        /// <summary>
        /// Fetches enterprise data for municipal utilities within specified parameters.
        /// Used in municipal finance to retrieve operational data for analysis, reporting, and decision-making.
        /// </summary>
        /// <param name="enterpriseId">Optional specific enterprise identifier. If null, fetches data for all enterprises.</param>
        /// <param name="startDate">Optional start date for data filtering. If null, no start date filter applied.</param>
        /// <param name="endDate">Optional end date for data filtering. If null, no end date filter applied.</param>
        /// <param name="filter">Optional string filter for additional data filtering criteria.</param>
        /// <returns>A Task containing ReportData with enterprise operational information for municipal utilities.</returns>
        Task<WileyWidget.Models.ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "");

        /// <summary>
        /// Runs analytical calculations on report data for municipal utility performance metrics.
        /// Processes enterprise data to generate insights for municipal finance management and operational efficiency.
        /// </summary>
        /// <param name="data">The ReportData containing enterprise information to analyze.</param>
        /// <returns>A Task containing AnalyticsData with calculated metrics and performance indicators.</returns>
        Task<AnalyticsData> RunReportCalcsAsync(ReportData data);

        /// <summary>
        /// Analyzes budget data to provide insights for municipal utility financial planning.
        /// Evaluates budget allocations, expenditures, and projections for municipal finance optimization.
        /// </summary>
        /// <param name="budget">The BudgetData containing financial information to analyze.</param>
        /// <returns>A Task containing BudgetInsights with recommendations and analysis results.</returns>
        Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget);

        /// <summary>
        /// Generates compliance reports for municipal utility enterprises.
        /// Ensures regulatory compliance and provides documentation for municipal finance auditing and reporting requirements.
        /// </summary>
        /// <param name="enterprise">The Enterprise object containing information about the municipal utility to evaluate.</param>
        /// <returns>A Task containing ComplianceReport with regulatory compliance status and recommendations.</returns>
        Task<WileyWidget.Models.ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise);

        /// <summary>
        /// Analyzes municipal data using AI to provide insights and recommendations.
        /// </summary>
        /// <param name="data">The data to analyze.</param>
        /// <param name="context">Additional context for the analysis.</param>
        /// <returns>A Task containing the analysis results as a string.</returns>
        Task<string> AnalyzeMunicipalDataAsync(object data, string context);

        /// <summary>
        /// Generates recommendations based on analyzed data.
        /// </summary>
        /// <param name="data">The data to generate recommendations for.</param>
        /// <returns>A Task containing the recommendations as a string.</returns>
        Task<string> GenerateRecommendationsAsync(object data);
    }
}
