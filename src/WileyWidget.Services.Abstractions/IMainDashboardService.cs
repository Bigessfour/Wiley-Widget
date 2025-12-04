using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service for loading main dashboard data and metrics.
    /// </summary>
    public interface IMainDashboardService
    {
        /// <summary>
        /// Loads dashboard data including budget totals, account counts, and recent activity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dashboard data DTO</returns>
        Task<DashboardDto> LoadDashboardDataAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Dashboard data transfer object.
    /// </summary>
    public record DashboardDto(
        decimal TotalBudget,
        decimal TotalActual,
        decimal Variance,
        int ActiveAccountCount,
        int TotalDepartments,
        string LastUpdateTime);
}
