using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IMainDashboardService for loading dashboard metrics.
    /// </summary>
    public class MainDashboardService : IMainDashboardService
    {
        private readonly ILogger<MainDashboardService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public MainDashboardService(
            ILogger<MainDashboardService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<DashboardDto> LoadDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Loading dashboard data");

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Use LINQ to aggregate dashboard metrics
                var activeAccounts = await context.MunicipalAccounts
                    .Where(a => a.IsActive)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var totalBudget = activeAccounts.Sum(a => a.BudgetAmount);
                var totalActual = activeAccounts.Sum(a => a.Balance);
                var variance = totalBudget - totalActual;
                var activeAccountCount = activeAccounts.Count;

                var totalDepartments = await context.Departments
                    .AsNoTracking()
                    .CountAsync(cancellationToken);

                var lastUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _logger.LogInformation("Dashboard data loaded: {AccountCount} accounts, Budget: {Budget:C}, Actual: {Actual:C}",
                    activeAccountCount, totalBudget, totalActual);

                return new DashboardDto(
                    totalBudget,
                    totalActual,
                    variance,
                    activeAccountCount,
                    totalDepartments,
                    lastUpdateTime);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Loading dashboard data operation was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data");
                throw;
            }
        }
    }
}
