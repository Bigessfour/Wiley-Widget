using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Minimal no-op DashboardService that returns empty data. Used as a safe fallback for DI quick-unblock.
    /// </summary>
    public class NoOpDashboardService : IDashboardService
    {
        public Task<IEnumerable<DashboardItem>> GetDashboardDataAsync()
        {
            return Task.FromResult<IEnumerable<DashboardItem>>(new DashboardItem[0]);
        }

        public Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync()
        {
            return Task.FromResult<IEnumerable<DashboardItem>>(new DashboardItem[0]);
        }

        public Task RefreshDashboardAsync()
        {
            return Task.CompletedTask;
        }
    }
}
