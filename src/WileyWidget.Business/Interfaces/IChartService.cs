using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions.Models;

namespace WileyWidget.Business.Interfaces
{
    public interface IChartService
    {
        /// <summary>
        /// Returns time-series totals (e.g., monthly totals) for the specified year.
        /// </summary>
        Task<IEnumerable<ChartDataPoint>> GetMonthlyTotalsAsync(int year, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a category breakdown (e.g., by department or category) for the given date range.
        /// </summary>
        Task<IEnumerable<ChartDataPoint>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? category = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the count of transactions within the date range and optional category filter.
        /// </summary>
        Task<int> GetTransactionCountAsync(DateTime start, DateTime end, string? category = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the budget variance (budgeted - actual) for the specified year.
        /// </summary>
        Task<decimal> GetBudgetVarianceAsync(int year, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the trend percentage comparing current month to previous month for the year.
        /// </summary>
        Task<double> GetTrendAsync(int year, int month, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the total budgeted amount for the specified year.
        /// </summary>
        Task<decimal> GetBudgetedAmountAsync(int year, CancellationToken cancellationToken = default);
    }
}
