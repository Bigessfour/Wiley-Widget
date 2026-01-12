using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Service that performs data prefetching operations asynchronously after startup.
/// This prevents blocking the UI thread during application initialization.
/// </summary>
public class DataPrefetchService : IAsyncInitializable
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DataPrefetchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPrefetchService"/> class.
    /// </summary>
    /// <param name="dashboardService">The dashboard service for prefetching data.</param>
    /// <param name="logger">The logger instance.</param>
    public DataPrefetchService(
        IDashboardService dashboardService,
        ILogger<DataPrefetchService> logger)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs asynchronous data prefetching operations.
    /// Called after MainForm.Shown to avoid blocking the UI thread.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting asynchronous data prefetch...");

        try
        {
            // Prefetch dashboard data
            await _dashboardService.GetDashboardDataAsync(cancellationToken: cancellationToken);
            _logger.LogDebug("Dashboard data prefetched successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data prefetch failed (non-critical)");
        }

        _logger.LogInformation("Asynchronous data prefetch completed");
    }
}
