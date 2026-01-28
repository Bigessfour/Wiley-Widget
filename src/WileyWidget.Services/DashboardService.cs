using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        /// <summary>
        /// Public collections for dashboard data
        /// </summary>
        public ObservableCollection<DashboardMetric> Metrics { get; set; }
        public ObservableCollection<DepartmentSummary> DepartmentSummaries { get; set; }
        public ObservableCollection<FundSummary> FundSummaries { get; set; }
        public ObservableCollection<AccountVariance> TopVariances { get; set; }
        public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; set; }
        public ObservableCollection<MonthlyRevenue> MonthlyExpenseData { get; set; }

        /// <summary>
        /// Dashboard UI state and messaging
        /// </summary>
        public float TotalBudgetGauge { get; set; }
        public float RevenueGauge { get; set; }
        public float ExpensesGauge { get; set; }
        public float NetPositionGauge { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }

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

            // Initialize collections
            Metrics = new ObservableCollection<DashboardMetric>();
            DepartmentSummaries = new ObservableCollection<DepartmentSummary>();
            FundSummaries = new ObservableCollection<FundSummary>();
            TopVariances = new ObservableCollection<AccountVariance>();
            MonthlyRevenueData = new ObservableCollection<MonthlyRevenue>();
            MonthlyExpenseData = new ObservableCollection<MonthlyRevenue>();
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

        /// <summary>
        /// Populates dashboard collections from the full Town of Wiley 2026 budget dataset
        /// </summary>
        public async Task PopulateDashboardMetricsFromWileyDataAsync(CancellationToken ct = default)
        {
            var rawData = await _budgetRepository.GetTownOfWileyBudgetDataAsync(ct);
            if (rawData == null || !rawData.Any()) return;

            // Clear existing
            Metrics.Clear();
            DepartmentSummaries.Clear();
            FundSummaries.Clear();
            TopVariances.Clear();
            MonthlyRevenueData.Clear();
            MonthlyExpenseData.Clear();

            // === KPIs ===
            var totalBudget = rawData.Where(x => x.BudgetYear > 1000).Sum(x => x.BudgetYear ?? 0);
            var totalActual = rawData.Where(x => x.ActualYTD.HasValue).Sum(x => x.ActualYTD ?? 0);
            var totalRevenue = rawData.Where(x => x.Category == "Revenue" && x.ActualYTD.HasValue).Sum(x => x.ActualYTD ?? 0);
            var totalExpense = rawData.Where(x => x.Category == "Expense" && x.ActualYTD.HasValue).Sum(x => x.ActualYTD ?? 0);

            Metrics.Add(new DashboardMetric { Name = "Total Budget 2026", Value = totalBudget, Unit = "$", Status = "Neutral", Category = "Budget" });
            Metrics.Add(new DashboardMetric { Name = "YTD Actual", Value = totalActual, Unit = "$", Status = totalActual > totalBudget * 0.6m ? "Warning" : "Healthy", Category = "Budget" });
            Metrics.Add(new DashboardMetric { Name = "Budget Remaining", Value = totalBudget - totalActual, Unit = "$", Status = "Healthy", Category = "Budget" });

            // === Department Breakdown (focus on Water, Sewer, Trash, Parks, etc.) ===
            // Use MappedDepartment column from database instead of raw FundOrDepartment
            var deptSummaries = rawData
                .Where(r => !string.IsNullOrEmpty(r.MappedDepartment))
                .GroupBy(r => r.MappedDepartment!)
                .Select(g => new DepartmentSummary
                {
                    DepartmentName = g.Key,
                    TotalBudgeted = g.Sum(r => r.BudgetYear ?? 0m),
                    TotalActual = g.Sum(r => r.ActualYTD ?? 0m),
                    Variance = g.Sum(r => r.ActualYTD ?? 0m) - g.Sum(r => r.BudgetYear ?? 0m),
                    VariancePercentage = g.Sum(r => r.BudgetYear ?? 0m) > 0
                        ? Math.Round(((g.Sum(r => r.ActualYTD ?? 0m) - g.Sum(r => r.BudgetYear ?? 0m)) / g.Sum(r => r.BudgetYear ?? 0m)) * 100m, 1)
                        : 0
                })
                .ToList();

            foreach (var d in deptSummaries)
            {
                DepartmentSummaries.Add(d);
            }

            // === Fund Summary ===
            FundSummaries.Add(new FundSummary { FundName = "General Fund", TotalBudgeted = rawData.Where(x => x.FundOrDepartment == "General Fund").Sum(x => x.BudgetYear ?? 0), TotalActual = rawData.Where(x => x.FundOrDepartment == "General Fund").Sum(x => x.ActualYTD ?? 0) });
            FundSummaries.Add(new FundSummary { FundName = "Utility Fund", TotalBudgeted = rawData.Where(x => x.FundOrDepartment?.Contains("Water") == true || x.FundOrDepartment?.Contains("Trash") == true).Sum(x => x.BudgetYear ?? 0), TotalActual = rawData.Where(x => x.FundOrDepartment?.Contains("Water") == true || x.FundOrDepartment?.Contains("Trash") == true).Sum(x => x.ActualYTD ?? 0) });
            FundSummaries.Add(new FundSummary { FundName = "Sanitation District", TotalBudgeted = 7970969m, TotalActual = 737669m });  // From image
            FundSummaries.Add(new FundSummary { FundName = "Conservation Trust", TotalBudgeted = 8500m, TotalActual = 0m });
            FundSummaries.Add(new FundSummary { FundName = "Recreation", TotalBudgeted = 20325m, TotalActual = 7951m });

            // === Top Variances (worst offenders from Sanitation + others) ===
            var variances = rawData
                .Where(x => x.Remaining.HasValue && x.BudgetYear > 100)
                .Select(x => new { x.Description, Variance = -(x.Remaining ?? 0), x.PercentOfBudget })
                .OrderByDescending(x => x.Variance)
                .Take(5);

            foreach (var v in variances)
            {
                TopVariances.Add(new AccountVariance
                {
                    AccountName = v.Description ?? "Unknown",
                    VarianceAmount = v.Variance,
                    VariancePercentage = v.PercentOfBudget ?? 0
                });
            }

            // Gauges
            TotalBudgetGauge = 100f;
            RevenueGauge = totalRevenue > 0 ? (float)(totalActual / totalRevenue * 100) : 0f;
            ExpensesGauge = totalBudget > 0 ? (float)(totalExpense / totalBudget * 100) : 0f;
            NetPositionGauge = totalRevenue > totalExpense ? 75f : 45f;

            StatusMessage = $"Wiley 2026 Budget Loaded — {DepartmentSummaries.Count} departments/funds";
            HasError = false;
            ErrorMessage = null;

            _logger.LogInformation("PopulateDashboardMetricsFromWileyDataAsync: Loaded {DeptCount} departments, {FundCount} funds, {VarCount} top variances",
                DepartmentSummaries.Count, FundSummaries.Count, TopVariances.Count);
        }

        /// <summary>
        /// Populates department summaries from Town of Wiley 2026 budget data using mapped departments
        /// </summary>
        public async Task PopulateDepartmentSummariesFromSanitationAsync(CancellationToken ct = default)
        {
            var rows = await _budgetRepository.GetTownOfWileyBudgetDataAsync(ct);

            var validRows = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.MappedDepartment)
                         && ((r.BudgetYear.GetValueOrDefault() > 0 || r.PriorYearActual.GetValueOrDefault() > 0)
                             || r.ActualYTD.GetValueOrDefault() > 0))
                .ToList();

            if (!validRows.Any())
            {
                _logger.LogWarning("PopulateDepartmentSummariesFromSanitationAsync: No valid rows found");
                return;
            }

            var deptGroups = validRows
                .GroupBy(r => r.MappedDepartment!)
                .Select(g => new
                {
                    Department = g.Key,
                    Budgeted = g.Sum(r => r.BudgetYear ?? r.PriorYearActual ?? 0m),
                    Actual = g.Sum(r => r.ActualYTD ?? 0m),
                    Remaining = g.Sum(r => r.Remaining ?? 0m)
                })
                .OrderByDescending(x => x.Budgeted)
                .ToList();

            DepartmentSummaries.Clear();

            foreach (var group in deptGroups)
            {
                var variance = group.Actual - group.Budgeted;
                var variancePercent = group.Budgeted != 0
                    ? Math.Round((variance / group.Budgeted) * 100m, 1)
                    : 0m;

                DepartmentSummaries.Add(new DepartmentSummary
                {
                    DepartmentName = group.Department,
                    TotalBudgeted = group.Budgeted,
                    TotalActual = group.Actual,
                    Variance = variance,
                    VariancePercentage = variancePercent
                });
            }

            StatusMessage = $"Sanitation District departments loaded: {DepartmentSummaries.Count} groups";
            HasError = false;
            ErrorMessage = null;

            _logger.LogInformation("PopulateDepartmentSummariesFromSanitationAsync: Loaded {DeptCount} departments with mapped budget data",
                DepartmentSummaries.Count);
        }
    }
}
