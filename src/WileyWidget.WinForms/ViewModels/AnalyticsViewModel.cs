using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for analytics functionality in the dashboard
    /// </summary>
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsViewModel> _logger;

        [ObservableProperty]
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
        private decimal rateIncreasePercentage;

        [ObservableProperty]
        private decimal expenseIncreasePercentage;

        [ObservableProperty]
        private decimal revenueTargetPercentage;

        [ObservableProperty]
        private int projectionYears = 3;

        public IAsyncRelayCommand PerformAnalysisCommand { get; }
        public IAsyncRelayCommand RunScenarioCommand { get; }
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
                IsLoading = true;
                StatusText = "Performing exploratory analysis...";

                var startDate = new DateTime(DateTime.Now.Year - 1, 7, 1);
                var endDate = new DateTime(DateTime.Now.Year, 6, 30);

                var result = await _analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate);

                // Update metrics
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

                // Update variances
                TopVariances.Clear();
                foreach (var variance in result.TopVariances)
                {
                    TopVariances.Add(variance);
                }

                // Update trends
                TrendData.Clear();
                foreach (var trend in result.TrendData.MonthlyTrends)
                {
                    TrendData.Add(trend);
                }

                // Update insights
                Insights.Clear();
                foreach (var insight in result.Insights)
                {
                    Insights.Add(insight);
                }

                StatusText = "Analysis complete";
                _logger.LogInformation("Exploratory analysis completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Analysis failed: {ex.Message}";
                _logger.LogError(ex, "Error performing exploratory analysis");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RunRateScenarioAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Running scenario analysis...";

                var parameters = new RateScenarioParameters
                {
                    RateIncreasePercentage = RateIncreasePercentage / 100, // Convert from percentage
                    ExpenseIncreasePercentage = ExpenseIncreasePercentage / 100,
                    RevenueTargetPercentage = RevenueTargetPercentage / 100,
                    ProjectionYears = ProjectionYears
                };

                var result = await _analyticsService.RunRateScenarioAsync(parameters);

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
                _logger.LogInformation("Rate scenario analysis completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Scenario failed: {ex.Message}";
                _logger.LogError(ex, "Error running rate scenario");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateReserveForecastAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Generating forecast...";

                var result = await _analyticsService.GenerateReserveForecastAsync(3);

                // Update forecast data
                ForecastData.Clear();
                foreach (var point in result.ForecastPoints)
                {
                    ForecastData.Add(point);
                }

                StatusText = "Forecast generated";
                _logger.LogInformation("Reserve forecast generated successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Forecast failed: {ex.Message}";
                _logger.LogError(ex, "Error generating reserve forecast");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Analytics metric for display
    /// </summary>
    public class AnalyticsMetric
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}