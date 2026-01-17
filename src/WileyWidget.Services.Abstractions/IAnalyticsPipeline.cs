using System.Threading;
using System;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Interface for the analytics pipeline that orchestrates end-to-end data processing
    /// from data layer through business logic to AI analysis and UI presentation.
    /// </summary>
    public interface IAnalyticsPipeline
    {
        /// <summary>
        /// Executes the full analytics pipeline from data retrieval to AI analysis.
        /// </summary>
        /// <param name="enterpriseId">Optional enterprise ID to filter data.</param>
        /// <param name="start">Optional start date for data filtering.</param>
        /// <param name="end">Optional end date for data filtering.</param>
        /// <returns>The compliance report with full analytics and AI insights.</returns>
        Task<ComplianceReport> ExecuteFullPipelineAsync(int? enterpriseId = null, DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default);
    }
}
