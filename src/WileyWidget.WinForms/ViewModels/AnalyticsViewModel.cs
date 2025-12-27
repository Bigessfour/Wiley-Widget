using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Threading;
using System.Threading;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for analytics functionality in the dashboard
    /// </summary>
    public partial class AnalyticsViewModel : ObservableObject, IDisposable
    {
        /// <summary>
        /// Represents the _analyticsservice.
        /// </summary>
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsViewModel> _logger;

        [ObservableProperty]
        /// <summary>
        /// Represents the isloading.
        /// </summary>
        /// <summary>
        /// Represents the isloading.
        /// </summary>
        private bool isLoading;

        [ObservableProperty]
        private string statusText = "Ready";

        [ObservableProperty]
        private ObservableCollection<AnalyticsMetric> metrics = new();

        [ObservableProperty]
        private ObservableCollection<VarianceAnalysis> topVariances = new();

        [ObservableProperty]
        private ObservableCollection<MonthlyTrend> trendData = new();

        [ObservableProperty]
        private ObservableCollection<YearlyProjection> scenarioProjections = new();

        [ObservableProperty]
        private ObservableCollection<ForecastPoint> forecastData = new();

        [ObservableProperty]
        private ObservableCollection<string> insights = new();

        [ObservableProperty]
        private ObservableCollection<string> recommendations = new();

        // Scenario parameters
        [ObservableProperty]
        /// <summary>
        /// Represents the rateincreasepercentage.
        /// </summary>
        /// <summary>
        /// Represents the rateincreasepercentage.
        /// </summary>
        private decimal rateIncreasePercentage;

        [ObservableProperty]
        /// <summary>
        /// Represents the expenseincreasepercentage.
        /// </summary>
        private decimal expenseIncreasePercentage;

        [ObservableProperty]
        /// <summary>
        /// Represents the revenuetargetpercentage.
        /// </summary>
        /// <summary>
        /// Represents the revenuetargetpercentage.
        /// </summary>
        private decimal revenueTargetPercentage;

        [ObservableProperty]
        private int projectionYears = 3;
        /// <summary>
        /// Gets or sets the performanalysiscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the performanalysiscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the performanalysiscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the performanalysiscommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the performanalysiscommand.
        /// </summary>

        public IAsyncRelayCommand PerformAnalysisCommand { get; }
        /// <summary>
        /// Gets or sets the runscenariocommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the runscenariocommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the runscenariocommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the runscenariocommand.
        /// </summary>
        public IAsyncRelayCommand RunScenarioCommand { get; }
        /// <summary>
        /// Gets or sets the generateforecastcommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the generateforecastcommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the generateforecastcommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the generateforecastcommand.
        /// </summary>
        /// <summary>
        /// Gets or sets the generateforecastcommand.
        /// </summary>
        public IAsyncRelayCommand GenerateForecastCommand { get; }

        public AnalyticsViewModel(IAnalyticsService analyticsService, ILogger<AnalyticsViewModel> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PerformAnalysisCommand = new AsyncRelayCommand(PerformExploratoryAnalysisAsync);
            RunScenarioCommand = new AsyncRelayCommand(RunRateScenarioAsync);
            GenerateForecastCommand = new AsyncRelayCommand(GenerateReserveForecastAsync);
        }

        private async Task PerformExploratoryAnalysisAsync()
        {
            try
            {
                // Guarantee UI state changes happen on the UI thread
                await UiThread.InvokeAsync(() =>
                {
                    IsLoading = true;
                    StatusText = "Performing exploratory analysis...";
                });

                var startDate = new DateTime(DateTime.Now.Year - 1, 7, 1);
                var endDate = new DateTime(DateTime.Now.Year, 6, 30);

                BudgetAnalysisResult result = await _analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate);

                // Update UI-bound collections on UI thread to avoid cross-thread exceptions
                await UiThread.InvokeAsync(() =>
                {
                    Metrics.Clear();
                    foreach (var kvp in result.CategoryBreakdown)
                    {
                        Metrics.Add(new AnalyticsMetric
                        {
                            Name = kvp.Key,
                            Value = kvp.Value,
                            Unit = "$"
                        });
                    }

                    TopVariances.Clear();
                    foreach (var variance in result.TopVariances)
                    {
                        TopVariances.Add(variance);
                    }

                    TrendData.Clear();
                    foreach (var trend in result.TrendData.MonthlyTrends)
                    {
                        TrendData.Add(trend);
                    }

                    Insights.Clear();
                    foreach (var insight in result.Insights)
                    {
                        Insights.Add(insight);
                    }

                    StatusText = "Analysis complete";
                });

                _logger.LogInformation("Exploratory analysis completed successfully");
            }
            catch (Exception ex)
            {
                await UiThread.InvokeAsync(() => StatusText = $"Analysis failed: {ex.Message}");
                _logger.LogError(ex, "Error performing exploratory analysis");
            }
            finally
            {
                await UiThread.InvokeAsync(() => IsLoading = false);
            }
        }

        private async Task RunRateScenarioAsync()
        {
            try
            {
                await UiThread.InvokeAsync(() =>
                {
                    IsLoading = true;
                    StatusText = "Running scenario analysis...";
                });

                var parameters = new RateScenarioParameters
                {
                    RateIncreasePercentage = RateIncreasePercentage / 100, // Convert from percentage
                    ExpenseIncreasePercentage = ExpenseIncreasePercentage / 100,
                    RevenueTargetPercentage = RevenueTargetPercentage / 100,
                    ProjectionYears = ProjectionYears
                };

                var result = await _analyticsService.RunRateScenarioAsync(parameters);

                await UiThread.InvokeAsync(() =>
                {
                    // Update projections
                    ScenarioProjections.Clear();
                    foreach (var projection in result.Projections)
                    {
                        ScenarioProjections.Add(projection);
                    }

                    // Update recommendations
                    Recommendations.Clear();
                    foreach (var rec in result.Recommendations)
                    {
                        Recommendations.Add(rec);
                    }

                    StatusText = "Scenario analysis complete";
                });

                _logger.LogInformation("Rate scenario analysis completed successfully");
            }
            catch (Exception ex)
            {
                await UiThread.InvokeAsync(() => StatusText = $"Scenario failed: {ex.Message}");
                _logger.LogError(ex, "Error running rate scenario");
            }
            finally
            {
                await UiThread.InvokeAsync(() => IsLoading = false);
            }
        }

        private async Task GenerateReserveForecastAsync()
        {
            try
            {
                await UiThread.InvokeAsync(() =>
                {
                    IsLoading = true;
                    StatusText = "Generating forecast...";
                });

                var result = await _analyticsService.GenerateReserveForecastAsync(3);

                await UiThread.InvokeAsync(() =>
                {
                    // Update forecast data
                    ForecastData.Clear();
                    foreach (var point in result.ForecastPoints)
                    {
                        ForecastData.Add(point);
                    }

                    StatusText = "Forecast generated";
                });

                _logger.LogInformation("Reserve forecast generated successfully");
            }
            catch (Exception ex)
            {
                await UiThread.InvokeAsync(() => StatusText = $"Forecast failed: {ex.Message}");
                _logger.LogError(ex, "Error generating reserve forecast");
            }
            finally
            {
                await UiThread.InvokeAsync(() => IsLoading = false);
            }
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        /// <summary>
        /// Performs dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up managed resources if needed
            }
            _logger.LogDebug("AnalyticsViewModel disposed");
        }
    }

    /// <summary>
    /// Analytics metric for display
    /// </summary>
    /// <summary>
    /// Represents a class for analyticsmetric.
    /// </summary>
    /// <summary>
    /// Represents a class for analyticsmetric.
    /// </summary>
    /// <summary>
    /// Represents a class for analyticsmetric.
    /// </summary>
    /// <summary>
    /// Represents a class for analyticsmetric.
    /// </summary>
    public class AnalyticsMetric
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public decimal Value { get; set; }
        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        /// <summary>
        /// Gets or sets the unit.
        /// </summary>
        public string Unit { get; set; } = string.Empty;
    }
}
