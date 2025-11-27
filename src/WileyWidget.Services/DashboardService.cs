using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Dashboard service with real data integration via repository
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly ILogger<DashboardService> _logger;
        private readonly IDashboardRepository _repository;

        public DashboardService(
            ILogger<DashboardService> logger,
            IDashboardRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<IEnumerable<DashboardMetric>> GetDashboardDataAsync()
        {
            _logger.LogInformation("Loading dashboard data from repository");

            try
            {
                var currentFiscalYear = $"FY {DateTime.Now.Year}";
                var metrics = await _repository.GetDashboardMetricsAsync(currentFiscalYear);

                _logger.LogInformation("Loaded {Count} dashboard metrics", metrics.Count);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data from repository");
                // Return fallback mock data if DB fails
                return GetFallbackMetrics();
            }
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            _logger.LogInformation("Loading dashboard summary from repository");

            try
            {
                var currentFiscalYear = $"FY {DateTime.Now.Year}";
                var metrics = await _repository.GetDashboardMetricsAsync(currentFiscalYear);
                var totalBudget = await _repository.GetTotalBudgetAsync(currentFiscalYear);
                var totalRevenue = await _repository.GetTotalRevenueAsync(currentFiscalYear);
                var totalExpenses = await _repository.GetTotalExpensesAsync(currentFiscalYear);

                return new DashboardSummary
                {
                    MunicipalityName = "Town of Wiley",
                    FiscalYear = currentFiscalYear,
                    LastUpdated = DateTime.Now,
                    Metrics = new List<DashboardMetric>(metrics),
                    TotalBudget = totalBudget,
                    TotalRevenue = totalRevenue,
                    TotalExpenses = totalExpenses,
                    NetPosition = totalRevenue - totalExpenses,
                    IsLoading = false,
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard summary from repository");
                // Return fallback summary if DB fails
                return GetFallbackSummary();
            }
        }

        public async Task RefreshDashboardAsync()
        {
            _logger.LogInformation("Refreshing dashboard data");
            // In a real implementation, this would clear caches or trigger data refresh
            await Task.Delay(200);
            _logger.LogInformation("Dashboard data refreshed");
        }

        private IEnumerable<DashboardMetric> GetFallbackMetrics()
        {
            _logger.LogWarning("Using fallback mock data");

            return new List<DashboardMetric>
            {
                new DashboardMetric
                {
                    Name = "Total Budget",
                    Value = 2500000.00,
                    Unit = "$",
                    Trend = Trend.Up,
                    ChangePercent = 5.2,
                    Description = "Total municipal budget (fallback data)",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Revenue Growth",
                    Value = 8.5,
                    Unit = "%",
                    Trend = Trend.Up,
                    ChangePercent = 2.1,
                    Description = "Year-over-year revenue growth (fallback data)",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Active Accounts",
                    Value = 1250,
                    Unit = "accounts",
                    Trend = Trend.Stable,
                    ChangePercent = 0.5,
                    Description = "Number of active municipal accounts (fallback data)",
                    LastUpdated = DateTime.Now
                }
            };
        }

        private DashboardSummary GetFallbackSummary()
        {
            var metrics = GetFallbackMetrics();
            return new DashboardSummary
            {
                MunicipalityName = "Town of Wiley",
                FiscalYear = $"FY {DateTime.Now.Year}",
                LastUpdated = DateTime.Now,
                Metrics = new List<DashboardMetric>(metrics),
                TotalBudget = 2500000m,
                TotalRevenue = 2000000m,
                TotalExpenses = 1750000m,
                NetPosition = 250000m,
                IsLoading = false,
                ErrorMessage = "Using fallback data - database connection unavailable"
            };
        }
    }
}
