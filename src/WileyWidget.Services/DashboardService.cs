using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for dashboard data operations with caching and resilience
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly ILogger<DashboardService> _logger;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IMunicipalAccountRepository _accountRepository;
        private readonly ICacheService? _cacheService;
        private readonly IConfiguration? _configuration;
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public DashboardService(
            ILogger<DashboardService> logger,
            IBudgetRepository budgetRepository,
            IMunicipalAccountRepository accountRepository,
            ICacheService? cacheService = null,
            IConfiguration? configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _cacheService = cacheService;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the configured fiscal year, defaulting to 2026
        /// </summary>
        private int GetCurrentFiscalYear()
        {
            // Read the configured default fiscal year to keep UI and services consistent
            return _configuration?.GetValue<int>("UI:DefaultFiscalYear", 2026) ?? 2026;
        }

        /// <summary>
        /// Gets dashboard data with caching
        /// </summary>
        public async Task<IEnumerable<DashboardItem>> GetDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("DashboardService: GetDashboardDataAsync called for FY {FiscalYear}", GetCurrentFiscalYear());

            try
            {
                const string cacheKey = "dashboard_items";

                // Try to get from cache first
                if (_cacheService != null)
                {
                    var cached = await _cacheService.GetAsync<List<DashboardItem>>(cacheKey);
                    if (cached != null && DateTime.UtcNow - _lastRefresh < _cacheExpiration)
                    {
                        _logger.LogDebug("DashboardService: Returning {Count} dashboard items from cache", cached.Count);
                        return cached;
                    }
                }

                // Fetch fresh data
                _logger.LogDebug("DashboardService: Fetching fresh dashboard data from repository");
                var items = await FetchDashboardDataAsync(cancellationToken);
                var itemsList = items.ToList();
                _logger.LogInformation("DashboardService: Retrieved {Count} dashboard items from repository", itemsList.Count);

                // Cache the results
                if (_cacheService != null)
                {
                    await _cacheService.SetAsync(cacheKey, itemsList, _cacheExpiration);
                }

                _lastRefresh = DateTime.UtcNow;
                return itemsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data");
                throw;
            }
        }

        /// <summary>
        /// Gets dashboard items for display
        /// </summary>
        public async Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync(CancellationToken cancellationToken = default)
        {
            return await GetDashboardDataAsync(cancellationToken);
        }

        /// <summary>
        /// Refreshes dashboard data by clearing cache
        /// </summary>
        public async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Refreshing dashboard data");

                // Clear cache
                if (_cacheService != null)
                {
                    await _cacheService.RemoveAsync("dashboard_items");
                }

                _lastRefresh = DateTime.MinValue;

                // Fetch fresh data
                await GetDashboardDataAsync(cancellationToken);

                _logger.LogInformation("Dashboard data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard");
                throw;
            }
        }

        /// <summary>
        /// Fetches dashboard data from repositories
        /// </summary>
        private async Task<IEnumerable<DashboardItem>> FetchDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            var items = new List<DashboardItem>();
            var currentFiscalYear = GetCurrentFiscalYear();
            var fiscalYearStart = new DateTime(currentFiscalYear - 1, 7, 1);
            var fiscalYearEnd = new DateTime(currentFiscalYear, 6, 30);

            _logger.LogInformation("DashboardService: Fetching data for FY {FiscalYear} ({StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd})",
                currentFiscalYear, fiscalYearStart, fiscalYearEnd);

            // Get budget summary
            try
            {
                _logger.LogDebug("DashboardService: Calling GetBudgetSummaryAsync for date range {Start} to {End}", fiscalYearStart, fiscalYearEnd);
                var budgetSummary = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd, cancellationToken);
                _logger.LogInformation("DashboardService: Budget summary retrieved (IsNull={IsNull})", budgetSummary == null);

                if (budgetSummary != null)
                {
                    _logger.LogInformation("DashboardService: Budget metrics - Budgeted: {Budgeted:C}, Actual: {Actual:C}, Variance: {Variance:C}",
                        budgetSummary.TotalBudgeted, budgetSummary.TotalActual, budgetSummary.TotalVariance);
                    items.Add(new DashboardItem
                    {
                        Title = "Total Budget",
                        Value = budgetSummary.TotalBudgeted.ToString("C0", System.Globalization.CultureInfo.CurrentCulture),
                        Description = $"Total budgeted for FY {currentFiscalYear}",
                        Category = "Budget"
                    });

                    items.Add(new DashboardItem
                    {
                        Title = "Total Actual",
                        Value = budgetSummary.TotalActual.ToString("C0", System.Globalization.CultureInfo.CurrentCulture),
                        Description = "Total actual spending",
                        Category = "Budget"
                    });

                    items.Add(new DashboardItem
                    {
                        Title = "Variance",
                        Value = budgetSummary.TotalVariance.ToString("C0", System.Globalization.CultureInfo.CurrentCulture),
                        Description = $"{budgetSummary.TotalVariancePercentage:F1}% {(budgetSummary.TotalVariance >= 0 ? "under" : "over")} budget",
                        Category = "Budget"
                    });

                    // Add fund summaries
                    foreach (var fund in budgetSummary.FundSummaries.Take(5))
                    {
                        items.Add(new DashboardItem
                        {
                            Title = $"Fund: {fund.FundName}",
                            Value = fund.Actual.ToString("C0", System.Globalization.CultureInfo.CurrentCulture),
                            Description = $"Budgeted: {fund.Budgeted:C0}, Variance: {fund.Variance:C0}",
                            Category = "Funds"
                        });
                    }

                    _logger.LogInformation("DashboardService: Added {FundCount} fund summaries (of {TotalFunds} total)",
                        Math.Min(5, budgetSummary.FundSummaries.Count), budgetSummary.FundSummaries.Count);
                }
                else
                {
                    _logger.LogWarning("DashboardService: Budget summary is null - no budget data available for period");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DashboardService: Error fetching budget summary - continuing with other data sources");
            }

            // Get account count (IMunicipalAccountRepository exposes GetAllAsync)
            try
            {
                var accountList = await _accountRepository.GetAllAsync(cancellationToken);
                var accountCount = accountList?.Count() ?? 0;
                _logger.LogInformation("DashboardService: Retrieved {AccountCount} municipal accounts", accountCount);

                items.Add(new DashboardItem
                {
                    Title = "Active Accounts",
                    Value = accountCount.ToString(System.Globalization.CultureInfo.CurrentCulture),
                    Description = "Total municipal accounts",
                    Category = "Accounts"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DashboardService: Error fetching account count - continuing with other data sources");
            }

            // Get budget entries and account statistics
            try
            {
                var recentEntries = await _budgetRepository.GetByFiscalYearAsync(currentFiscalYear, cancellationToken);
                _logger.LogInformation("DashboardService: Retrieved {EntryCount} budget entries for FY {FiscalYear}",
                    recentEntries?.Count() ?? 0, currentFiscalYear);
                var revenueAccounts = await _budgetRepository.GetRevenueAccountCountAsync(currentFiscalYear, cancellationToken);
                var expenseAccounts = await _budgetRepository.GetExpenseAccountCountAsync(currentFiscalYear, cancellationToken);

                items.Add(new DashboardItem
                {
                    Title = "Revenue Accounts",
                    Value = revenueAccounts.ToString(System.Globalization.CultureInfo.CurrentCulture),
                    Description = "Accounts with revenue activity",
                    Category = "Activity"
                });

                items.Add(new DashboardItem
                {
                    Title = "Expense Accounts",
                    Value = expenseAccounts.ToString(System.Globalization.CultureInfo.CurrentCulture),
                    Description = "Accounts with expense activity",
                    Category = "Activity"
                });

                _logger.LogInformation("DashboardService: Successfully fetched dashboard items (Revenue accts: {RevenueCount}, Expense accts: {ExpenseCount})",
                    revenueAccounts, expenseAccounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DashboardService: Error fetching account statistics - continuing with available data");
            }

            return items;
        }

        /// <summary>
        /// Gets data statistics for diagnostic purposes
        /// </summary>
        public async Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("DashboardService: Retrieving data statistics...");

                var currentFiscalYear = GetCurrentFiscalYear();
                var (totalRecords, oldestRecord, newestRecord) = await _budgetRepository.GetDataStatisticsAsync(currentFiscalYear, cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation("DashboardService: Data statistics retrieved in {ElapsedMs}ms - {Count} records, Oldest: {Oldest}, Newest: {Newest}",
                    stopwatch.ElapsedMilliseconds,
                    totalRecords,
                    oldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                    newestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");

                if (totalRecords == 0)
                {
                    _logger.LogWarning("DashboardService: Database has no budget entries for FY {FiscalYear}. Dashboard will show empty data.", currentFiscalYear);
                }

                return (totalRecords, oldestRecord, newestRecord);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "DashboardService: Error retrieving data statistics after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
