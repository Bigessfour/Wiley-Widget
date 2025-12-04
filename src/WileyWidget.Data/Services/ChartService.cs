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
        private readonly AppDbContext _context;

        public ChartService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Returns monthly totals for the specified calendar year using transaction amounts.
        /// Months with no data will appear with value 0 so charts render consistently.
        /// </summary>
        public async Task<IEnumerable<ChartDataPoint>> GetMonthlyTotalsAsync(int year, CancellationToken cancellationToken = default)
        {
            // Query transactions for the specified year and group by month
            var monthly = await _context.Transactions
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
            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate >= s && t.TransactionDate <= e)
                .Include(t => t.BudgetEntry)
                    .ThenInclude(be => be.Department);

            if (!string.IsNullOrEmpty(category) && category != "All Categories")
            {
                query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Transaction, Department>)query.Where(t => t.BudgetEntry.Department.Name == category);
            }

            var transactions = await query.ToListAsync(cancellationToken);

            var breakdown = transactions
                .GroupBy(t => t.BudgetEntry?.Department?.Name ?? "(Unassigned)")
                .Select(g => new ChartDataPoint { Category = g.Key, Value = (double)g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Value)
                .ToList();

            // Temporary debug: check what we have
            if (transactions.Any())
            {
                var first = transactions.First();
                var key = first.BudgetEntry?.Department?.Name ?? "(Unassigned)";
                var sum = (double)transactions.Sum(x => x.Amount);
                // Can't log, but this would help debug
            }

            return breakdown;
        }

        /// <summary>
        /// Returns the count of transactions within the date range and optional category filter.
        /// </summary>
        public async Task<int> GetTransactionCountAsync(DateTime start, DateTime end, string? category = null, CancellationToken cancellationToken = default)
        {
            var s = start.Date;
            var e = end.Date.AddDays(1).AddTicks(-1);

            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate >= s && t.TransactionDate <= e);

            if (!string.IsNullOrEmpty(category) && category != "All Categories")
            {
                query = (IQueryable<Transaction>)query.Include(t => t.BudgetEntry)
                             .ThenInclude(be => be.Department)
                             .Where(t => t.BudgetEntry.Department.Name == category);
            }

            return await query.CountAsync(cancellationToken);
        }

        /// <summary>
        /// Returns the budget variance (budgeted - actual) for the specified year.
        /// </summary>
        public async Task<decimal> GetBudgetVarianceAsync(int year, CancellationToken cancellationToken = default)
        {
            var budgeted = await _context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == year)
                .SumAsync(be => be.BudgetedAmount, cancellationToken);

            var actual = await _context.Transactions
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
            var currentMonth = (double)await _context.Transactions
                .AsNoTracking()
                .Where(t => t.TransactionDate.Year == year && t.TransactionDate.Month == month)
                .SumAsync(t => t.Amount, cancellationToken);

            var prevMonth = month == 1 ? 12 : month - 1;
            var prevYear = month == 1 ? year - 1 : year;

            var previousMonth = (double)await _context.Transactions
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
            return await _context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == year)
                .SumAsync(be => be.BudgetedAmount, cancellationToken);
        }
    }
}
