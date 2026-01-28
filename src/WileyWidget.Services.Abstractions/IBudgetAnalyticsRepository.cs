using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Repository for budget analytics queries, providing specific methods for analysis operations.
    /// </summary>
    public interface IBudgetAnalyticsRepository
    {
        /// <summary>
        /// Gets top budget variances by amount for a fiscal year.
        /// </summary>
        Task<List<VarianceAnalysis>> GetTopVariancesAsync(int topN, int fiscalYear, CancellationToken ct = default);

        /// <summary>
        /// Gets reserve history data points for forecasting.
        /// </summary>
        Task<List<ReserveDataPoint>> GetReserveHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default);

        /// <summary>
        /// Gets category breakdown for a date range and optional entity filter.
        /// </summary>
        Task<Dictionary<string, decimal>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? entityName, CancellationToken ct = default);

        /// <summary>
        /// Gets trend analysis data for budget entries.
        /// </summary>
        Task<TrendAnalysis> GetTrendAnalysisAsync(DateTime start, DateTime end, CancellationToken ct = default);

        /// <summary>
        /// Gets budget overview data for a fiscal year.
        /// </summary>
        Task<BudgetOverviewData> GetBudgetOverviewDataAsync(int fiscalYear, CancellationToken ct = default);

        /// <summary>
        /// Gets budget metrics for grid display.
        /// </summary>
        Task<List<BudgetMetric>> GetBudgetMetricsAsync(int fiscalYear, CancellationToken ct = default);

        /// <summary>
        /// Gets summary KPIs for dashboard display.
        /// </summary>
        Task<List<SummaryKpi>> GetSummaryKpisAsync(int fiscalYear, CancellationToken ct = default);

        /// <summary>
        /// Gets detailed variance records for a fiscal year.
        /// </summary>
        Task<List<VarianceRecord>> GetVarianceDetailsAsync(int fiscalYear, CancellationToken ct = default);
    }
}
