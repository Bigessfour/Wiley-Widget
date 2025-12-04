using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.ViewModels
{
    public class FinancialMetric
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// ViewModel for budget overview. Orchestrates UI interactions and delegates
    /// all business logic to IMainDashboardService (MVVM purity - Phase 3 refactoring).
    /// </summary>
    public partial class BudgetOverviewViewModel : ObservableObject
    {
        private readonly ILogger<BudgetOverviewViewModel> _logger;
        private readonly IMainDashboardService _dashboardService;

        [ObservableProperty]
        private ObservableCollection<FinancialMetric> metrics = new();

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal variance;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public IAsyncRelayCommand LoadDataCommand { get; }

        public BudgetOverviewViewModel(ILogger<BudgetOverviewViewModel> logger, IMainDashboardService dashboardService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));

            try
            {
                LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
                _logger.LogInformation("BudgetOverviewViewModel constructed with IMainDashboardService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during BudgetOverviewViewModel construction");
                throw;
            }
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger.LogInformation("Loading budget overview data");

                // Delegate business logic to service
                var data = await _dashboardService.LoadDashboardDataAsync(cancellationToken);

                // Update computed properties
                TotalBudget = data.TotalBudget;
                TotalActual = data.TotalActual;
                Variance = data.Variance;

                // Build metrics collection for UI display
                Metrics.Clear();
                Metrics.Add(new FinancialMetric { Category = "Total Budget", Amount = TotalBudget });
                Metrics.Add(new FinancialMetric { Category = "Total Actual", Amount = TotalActual });
                Metrics.Add(new FinancialMetric { Category = "Variance", Amount = Variance });

                _logger.LogInformation("Budget overview loaded: Budget={Budget:C}, Actual={Actual:C}, Variance={Variance:C}",
                    TotalBudget, TotalActual, Variance);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Budget overview loading canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget overview");
                ErrorMessage = "Failed to load budget overview";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await LoadDataAsync(cancellationToken);
        }
    }
}
