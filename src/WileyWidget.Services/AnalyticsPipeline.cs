using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of the analytics pipeline that orchestrates end-to-end data processing
    /// from data layer through business logic to AI analysis and UI presentation.
    /// </summary>
    public class AnalyticsPipeline : IAnalyticsPipeline
    {
        private readonly IEnterpriseRepository _repo;
        private readonly IGrokSupercomputer _grok;
        private readonly ILogger<AnalyticsPipeline> _logger;

        /// <summary>
        /// Initializes a new instance of the AnalyticsPipeline class.
        /// </summary>
        /// <param name="repo">The enterprise repository for data access.</param>
        /// <param name="grok">The Grok supercomputer for AI analysis.</param>
        /// <param name="logger">The logger for pipeline operations.</param>
        public AnalyticsPipeline(IEnterpriseRepository repo, IGrokSupercomputer grok, ILogger<AnalyticsPipeline> logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _grok = grok ?? throw new ArgumentNullException(nameof(grok));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the full analytics pipeline from data retrieval to AI analysis.
        /// </summary>
        /// <param name="enterpriseId">Optional enterprise ID to filter data.</param>
        /// <param name="start">Optional start date for data filtering.</param>
        /// <param name="end">Optional end date for data filtering.</param>
        /// <returns>The compliance report with full analytics and AI insights.</returns>
        public async Task<ComplianceReport> ExecuteFullPipelineAsync(int? enterpriseId = null, DateTime? start = null, DateTime? end = null)
        {
            _logger.LogInformation("Pipeline start: Enterprise {Id}", enterpriseId);

            // 1. Data Layer: Retrieve enterprise data
            var enterprises = await _repo.GetAllAsync();
            var targetEnterprise = enterpriseId.HasValue
                ? enterprises.FirstOrDefault(e => e.Id == enterpriseId.Value)
                : enterprises.FirstOrDefault();

            if (targetEnterprise == null)
            {
                _logger.LogWarning("No enterprise found for ID {Id}", enterpriseId);
                targetEnterprise = new Enterprise(); // Fallback to empty enterprise
            }

            // 2. Business Layer: Fetch and process report data
            var report = await _grok.FetchEnterpriseDataAsync(enterpriseId, start, end);
            var analyticsData = await _grok.RunReportCalcsAsync(report);

            // 3. AI Layer: Generate compliance report and perform analysis
            var compliance = await _grok.GenerateComplianceReportAsync(targetEnterprise);
            compliance.UpdateCompliance(); // Perform semantic compliance checks

            // 4. Projections/Analysis: Analyze budget data for insights
            if (compliance.BudgetSummary != null)
            {
                var budgetData = new BudgetData
                {
                    EnterpriseId = targetEnterprise.Id,
                    FiscalYear = DateTime.Now.Year,
                    TotalBudget = compliance.BudgetSummary.TotalBudgeted,
                    TotalExpenditures = compliance.BudgetSummary.TotalActual,
                    RemainingBudget = compliance.BudgetSummary.TotalBudgeted - compliance.BudgetSummary.TotalActual
                };
                await _grok.AnalyzeBudgetDataAsync(budgetData);
            }

            _logger.LogInformation("Pipeline complete: {ComplianceItems} items", compliance.ComplianceItems?.Count ?? 0);
            return compliance;
        }
    }
}
