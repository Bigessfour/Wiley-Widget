using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public class DashboardService : IDashboardService
{
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(ILogger<DashboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching dashboard summary...");
        await Task.Delay(500, cancellationToken);

        var metrics = new List<DashboardMetric>
        {
            new DashboardMetric { Name = "Total Revenues", Value = 2_450_000, Unit = "$", Description = "FY 2025 municipal revenues", Trend = MetricTrend.Up },
            new DashboardMetric { Name = "Total Expenditures", Value = 2_200_000, Unit = "$", Description = "FY 2025 municipal expenditures", Trend = MetricTrend.Down },
            new DashboardMetric { Name = "Budget Balance", Value = 250_000, Unit = "$", Description = "Surplus/Deficit", Trend = MetricTrend.Up },
            new DashboardMetric { Name = "Active Accounts", Value = 127, Unit = "Count", Description = "Municipal account count" },
            new DashboardMetric { Name = "Budget Utilization", Value = 89.8, Unit = "%", Description = "Percentage of budget used" }
        };

        return new DashboardSummary
        {
            Metrics = metrics,
            GeneratedAt = DateTime.UtcNow,
            MunicipalityName = "Town of Wiley",
            FiscalYear = 2025
        };
    }

    public async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing dashboard data...");
        await Task.Delay(200, cancellationToken);
    }
}