using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Repository interface for analytics-specific data operations
    /// </summary>
    public interface IAnalyticsRepository
    {
        /// <summary>
        /// Gets historical reserve data points for forecasting
        /// </summary>
        Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current reserve balance
        /// </summary>
        Task<decimal> GetCurrentReserveBalanceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets municipal account information for analytics
        /// </summary>
        Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available entity names for filtering
        /// </summary>
        Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets trend data for forecasting
        /// </summary>
        Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a scenario analysis
        /// </summary>
        Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default);
    }
}
