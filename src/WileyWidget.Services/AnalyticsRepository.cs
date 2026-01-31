using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Repository implementation for analytics-specific data operations
    /// </summary>
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<AnalyticsRepository> _logger;

        public AnalyticsRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<AnalyticsRepository> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets historical reserve data points for forecasting
        /// </summary>
        public async Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Get transactions that affect reserves (savings, investments, fund balances)
            var reserveTransactions = await context.Transactions
                .AsNoTracking()
                .Include(t => t.BudgetEntry)
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .Where(t => t.BudgetEntry.AccountNumber.StartsWith("1") || // Assets
                           t.BudgetEntry.AccountNumber.StartsWith("2") || // Liabilities
                           t.BudgetEntry.AccountNumber.StartsWith("3"))   // Equity/Reserves
                .OrderBy(t => t.TransactionDate)
                .ToListAsync(cancellationToken);

            if (!reserveTransactions.Any())
            {
                _logger.LogWarning("No reserve transactions found for period {Start} to {End}", startDate, endDate);
                return Array.Empty<ReserveDataPoint>();
            }

            // Calculate running reserve balance
            var dataPoints = new List<ReserveDataPoint>();
            decimal runningBalance = 0;

            foreach (var transaction in reserveTransactions)
            {
                runningBalance += transaction.Amount;

                dataPoints.Add(new ReserveDataPoint
                {
                    Date = transaction.TransactionDate,
                    Reserves = runningBalance
                });
            }

            _logger.LogInformation("Retrieved {Count} reserve data points for forecasting", dataPoints.Count);
            return dataPoints;
        }

        /// <summary>
        /// Gets current reserve balance
        /// </summary>
        public async Task<decimal> GetCurrentReserveBalanceAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Calculate current reserves from latest transactions
            var latestTransactions = await context.Transactions
                .AsNoTracking()
                .Include(t => t.BudgetEntry)
                .Where(t => t.BudgetEntry.AccountNumber.StartsWith("3")) // Equity/Reserves accounts
                .OrderByDescending(t => t.TransactionDate)
                .Take(100) // Get recent transactions for calculation
                .ToListAsync(cancellationToken);

            if (!latestTransactions.Any())
            {
                _logger.LogWarning("No reserve transactions found for current balance calculation");
                return 0;
            }

            // Calculate balance from most recent transactions
            var balance = latestTransactions.Sum(t => t.Amount);

            _logger.LogInformation("Calculated current reserve balance: {Balance:C}", balance);
            return balance;
        }

        /// <summary>
        /// Gets budget entries for variance analysis
        /// </summary>
        public async Task<IEnumerable<BudgetEntry>> GetBudgetEntriesForVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.BudgetEntries
                .AsNoTracking()
                .Include(be => be.Fund)
                .Include(be => be.MunicipalAccount)
                .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
                .Where(be => be.BudgetedAmount > 0) // Only entries with budget amounts
                .OrderBy(be => be.AccountNumber)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets municipal account information for analytics
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.MunicipalAccounts
                .AsNoTracking()
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets available entity names for filtering
        /// </summary>
        public async Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.BudgetEntries
                .AsNoTracking()
                .Where(be => !string.IsNullOrWhiteSpace(be.EntityName))
                .Select(be => be.EntityName!)
                .Distinct()
                .OrderBy(entity => entity)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets trend data for forecasting
        /// </summary>
        public async Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
        {
            // TODO: Implement trend data retrieval
            _logger.LogWarning("GetTrendDataAsync not implemented");
            return new List<TrendSeries>();
        }

        /// <summary>
        /// Runs a scenario analysis
        /// </summary>
        public async Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
        {
            // TODO: Implement scenario analysis
            _logger.LogWarning("RunScenarioAsync not implemented");
            return new ScenarioResult("Scenario not implemented", 0, 0);
        }
    }
}
