using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Business.Interfaces;
using WileyWidget.Abstractions.Models;
using WileyWidget.Models;

namespace WileyWidget.Data.Services
{
    /// <summary>
    /// Aggregates chart-friendly data from the DbContext for use in UI charts.
    /// Implemented in Data layer to keep query logic next to EF models while exposing an interface
    /// in Business.Interfaces for higher layers to consume.
    /// </summary>
    public class ChartService : IChartService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> _contextFactory;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public ChartService(Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // Note: ChartService prefers IDbContextFactory<AppDbContext>. Unit tests should provide
        // a small test implementation if constructing ChartService directly.

        /// <summary>
        /// Returns monthly totals for the specified calendar year using transaction amounts.
        /// Months with no data will appear with value 0 so charts render consistently.
        /// </summary>
        public async Task<IEnumerable<ChartDataPoint>> GetMonthlyTotalsAsync(int year, CancellationToken cancellationToken = default)
        {
            // Query transactions for the specified year and group by month
            using var ctx = _contextFactory.CreateDbContext();
            var monthly = await ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate.Year == year)
                .GroupBy(t => t.TransactionDate.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            // Build result for all 12 months (Jan..Dec)
            var results = Enumerable.Range(1, 12)
                .Select(m => new ChartDataPoint
                {
                    Category = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedMonthName(m),
                    Value = (double)(monthly.FirstOrDefault(x => x.Month == m)?.Total ?? 0)
                })
                .ToList();

            return results;
        }

        /// <summary>
        /// Returns a category breakdown between the two dates (inclusive). Uses Department.Name as category
        /// and sums transaction amounts that fall within the range. Departments with zero transactions are omitted.
        /// </summary>
        public async Task<IEnumerable<ChartDataPoint>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? category = null, CancellationToken cancellationToken = default)
        {
            // Normalize dates to UTC / whole-day
            var s = start.Date;
            var e = end.Date.AddDays(1).AddTicks(-1);

            // Join transactions -> budget entry -> department -> group
            using var ctx = _contextFactory.CreateDbContext();
            var baseQuery = ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate >= s && t.TransactionDate <= e);

            if (!string.IsNullOrEmpty(category) && category != "All Categories")
            {
                baseQuery = baseQuery.Where(t => t.BudgetEntry.Department.Name == category);
            }

            var transactions = await baseQuery
                .Include(t => t.BudgetEntry)
                    .ThenInclude(be => be.Department)
                .ToListAsync(cancellationToken);

            var breakdown = transactions
                .GroupBy(t => t.BudgetEntry?.Department?.Name ?? "(Unassigned)")
                .Select(g => new ChartDataPoint { Category = g.Key, Value = (double)g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Value)
                .ToList();

            return breakdown;
        }

        /// <summary>
        /// Returns the count of transactions within the date range and optional category filter.
        /// </summary>
        public async Task<int> GetTransactionCountAsync(DateTime start, DateTime end, string? category = null, CancellationToken cancellationToken = default)
        {
            var s = start.Date;
            var e = end.Date.AddDays(1).AddTicks(-1);

            using var ctx = _contextFactory.CreateDbContext();
            var query = ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate >= s && t.TransactionDate <= e);

            if (!string.IsNullOrEmpty(category) && category != "All Categories")
            {
                query = query.Where(t => t.BudgetEntry.Department.Name == category);
            }

            return await query.CountAsync(cancellationToken);
        }

        /// <summary>
        /// Returns the budget variance (budgeted - actual) for the specified year.
        /// </summary>
        public async Task<decimal> GetBudgetVarianceAsync(int year, CancellationToken cancellationToken = default)
        {

            using var ctx = _contextFactory.CreateDbContext();
            var budgeted = await ctx.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == year)
                .SumAsync(be => be.BudgetedAmount, cancellationToken);

            var actual = await ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate.Year == year)
                .SumAsync(t => t.Amount, cancellationToken);

            return budgeted - actual;
        }

        /// <summary>
        /// Returns the trend percentage comparing current month to previous month for the year.
        /// </summary>
        public async Task<double> GetTrendAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            using var ctx = _contextFactory.CreateDbContext();
            var currentMonth = (double)await ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate.Year == year && t.TransactionDate.Month == month)
                .SumAsync(t => t.Amount, cancellationToken);

            var prevMonth = month == 1 ? 12 : month - 1;
            var prevYear = month == 1 ? year - 1 : year;

            var previousMonth = (double)await ctx.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate.Year == prevYear && t.TransactionDate.Month == prevMonth)
                .SumAsync(t => t.Amount, cancellationToken);

            if (previousMonth == 0) return 0; // Avoid division by zero

            return ((currentMonth - previousMonth) / previousMonth) * 100;
        }

        /// <summary>
        /// Returns the total budgeted amount for the specified year.
        /// </summary>
        public async Task<decimal> GetBudgetedAmountAsync(int year, CancellationToken cancellationToken = default)
        {
            using var ctx = _contextFactory.CreateDbContext();
            return await ctx.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == year)
                .SumAsync(be => be.BudgetedAmount, cancellationToken);
        }
    }
}
