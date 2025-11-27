using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics = new();

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string municipalityName = "Town of Wiley";

        [ObservableProperty]
        private string fiscalYear = "FY 2026";

        [ObservableProperty]
        private DateTime lastUpdated;

        // Gauge bindings
        [ObservableProperty]
        private decimal totalBudgetGauge;

        [ObservableProperty]
        private decimal revenueGauge;

        [ObservableProperty]
        private decimal expensesGauge;

        [ObservableProperty]
        private decimal netPositionGauge;

        public IAsyncRelayCommand LoadDashboardCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }

        public DashboardViewModel(IDashboardService dashboardService, ILogger<DashboardViewModel> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;

            LoadDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDashboardAsync);

            // Initial load
            LoadDashboardCommand.ExecuteAsync(null);
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var data = await _dashboardService.GetDashboardDataAsync();
                var summary = await _dashboardService.GetDashboardSummaryAsync();

                Metrics.Clear();
                foreach (var metric in data)
                {
                    Metrics.Add(metric);
                }

                MunicipalityName = summary.MunicipalityName;
                FiscalYear = summary.FiscalYear;
                LastUpdated = summary.LastUpdated;

                TotalBudgetGauge = summary.TotalBudget;
                RevenueGauge = summary.TotalRevenue;
                ExpensesGauge = summary.TotalExpenses;
                NetPositionGauge = summary.NetPosition;

                _logger.LogInformation("Dashboard loaded: {Budget:C}, Revenue {Revenue:C}", summary.TotalBudget, summary.TotalRevenue);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to load dashboard");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDashboardAsync()
        {
            await _dashboardService.RefreshDashboardAsync();
            await LoadDashboardAsync();
        }
    }
}
