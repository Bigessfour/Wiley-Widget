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
    public partial class AnalyticsViewModel : ObservableObject, IDisposable
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
        private decimal rateIncreasePercentage = 5.0m;

        [ObservableProperty]
        private decimal expenseIncreasePercentage = 3.0m;

        [ObservableProperty]
        private decimal revenueTargetPercentage = 10.0m;

        [ObservableProperty]
        private int projectionYears = 3;

        // Summary properties
        [ObservableProperty]
        private decimal totalBudgetedAmount;

        [ObservableProperty]
        private decimal totalActualAmount;

        [ObservableProperty]
        private decimal totalVarianceAmount;

        [ObservableProperty]
        private decimal averageVariancePercentage;

        [ObservableProperty]
        private string recommendationExplanation = string.Empty;

        // Filtered collections
        [ObservableProperty]
        private ObservableCollection<AnalyticsMetric> filteredMetrics = new();

        [ObservableProperty]
        private ObservableCollection<VarianceAnalysis> filteredTopVariances = new();

        // Search/filter properties
        [ObservableProperty]
        private string metricsSearchText = string.Empty;

        [ObservableProperty]
        private string variancesSearchText = string.Empty;

        /// <summary>
        /// Command to perform exploratory analysis
        /// </summary>
        public IAsyncRelayCommand PerformAnalysisCommand { get; }

        /// <summary>
        /// Command to run scenario analysis
        /// </summary>
        public IAsyncRelayCommand RunScenarioCommand { get; }

        /// <summary>
        /// Command to generate forecast
        /// </summary>
        public IAsyncRelayCommand GenerateForecastCommand { get; }

        /// <summary>
        /// Command to refresh all data
        /// </summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        public AnalyticsViewModel(IAnalyticsService analyticsService, ILogger<AnalyticsViewModel> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PerformAnalysisCommand = new AsyncRelayCommand(PerformExploratoryAnalysisAsync);
            RunScenarioCommand = new AsyncRelayCommand(RunRateScenarioAsync);
            GenerateForecastCommand = new AsyncRelayCommand(GenerateReserveForecastAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAllDataAsync);

            // Auto-load data on initialization
            _ = Task.Run(async () => await RefreshCommand.ExecuteAsync(null));
        }

        /// <summary>
        /// Performs exploratory data analysis asynchronously
        /// </summary>
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

                UpdateSummaries();
                UpdateFilteredCollections();

                StatusText = "Analysis complete";
                _logger.LogInformation("Exploratory analysis completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Analysis failed: {ex.Message}";
                _logger.LogError(ex, "Error performing exploratory analysis");

                // Fallback to sample data
                await LoadSampleDataAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Runs rate scenario analysis asynchronously
        /// </summary>
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

                RecommendationExplanation = $"Scenario analysis shows potential revenue impact of {result.RevenueImpact:C} and reserve impact of {result.ReserveImpact:C}.";

                StatusText = "Scenario analysis complete";
                _logger.LogInformation("Rate scenario analysis completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Scenario failed: {ex.Message}";
                _logger.LogError(ex, "Error running rate scenario");

                // Fallback to sample data
                await LoadSampleScenarioDataAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates reserve forecast asynchronously
        /// </summary>
        private async Task GenerateReserveForecastAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Generating forecast...";

                var result = await _analyticsService.GenerateReserveForecastAsync(ProjectionYears);

                // Update forecast data
                ForecastData.Clear();
                foreach (var point in result.ForecastPoints)
                {
                    ForecastData.Add(point);
                }

                RecommendationExplanation = $"Forecast indicates recommended reserve level of {result.RecommendedReserveLevel:C}. Risk assessment: {result.RiskAssessment}.";

                StatusText = "Forecast generated";
                _logger.LogInformation("Reserve forecast generated successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Forecast failed: {ex.Message}";
                _logger.LogError(ex, "Error generating reserve forecast");

                // Fallback to sample data
                await LoadSampleForecastDataAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes all analytics data asynchronously
        /// </summary>
        private async Task RefreshAllDataAsync()
        {
            await PerformExploratoryAnalysisAsync();
            await RunRateScenarioAsync();
            await GenerateReserveForecastAsync();
        }

        /// <summary>
        /// Updates summary properties based on current data
        /// </summary>
        private void UpdateSummaries()
        {
            TotalBudgetedAmount = TopVariances.Sum(v => v.BudgetedAmount);
            TotalActualAmount = TopVariances.Sum(v => v.ActualAmount);
            TotalVarianceAmount = TopVariances.Sum(v => v.VarianceAmount);
            AverageVariancePercentage = TopVariances.Any() ? TopVariances.Average(v => v.VariancePercentage) : 0;
        }

        /// <summary>
        /// Updates filtered collections based on search text
        /// </summary>
        private void UpdateFilteredCollections()
        {
            FilteredMetrics = new ObservableCollection<AnalyticsMetric>(
                Metrics.Where(m => string.IsNullOrEmpty(MetricsSearchText) ||
                                   m.Name.Contains(MetricsSearchText, StringComparison.OrdinalIgnoreCase)));

            FilteredTopVariances = new ObservableCollection<VarianceAnalysis>(
                TopVariances.Where(v => string.IsNullOrEmpty(VariancesSearchText) ||
                                        v.AccountName.Contains(VariancesSearchText, StringComparison.OrdinalIgnoreCase) ||
                                        v.AccountNumber.Contains(VariancesSearchText, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Called when metrics search text changes
        /// </summary>
        partial void OnMetricsSearchTextChanged(string value) => UpdateFilteredCollections();

        /// <summary>
        /// Called when variances search text changes
        /// </summary>
        partial void OnVariancesSearchTextChanged(string value) => UpdateFilteredCollections();

        /// <summary>
        /// Loads sample data for design-time preview or fallback
        /// </summary>
        private async Task LoadSampleDataAsync()
        {
            await Task.Run(() =>
            {
                Metrics.Clear();
                Metrics.Add(new AnalyticsMetric { Name = "Revenue", Value = 1500000m, Unit = "$" });
                Metrics.Add(new AnalyticsMetric { Name = "Expenses", Value = 1200000m, Unit = "$" });
                Metrics.Add(new AnalyticsMetric { Name = "Reserves", Value = 300000m, Unit = "$" });

                TopVariances.Clear();
                TopVariances.Add(new VarianceAnalysis
                {
                    AccountNumber = "1000",
                    AccountName = "General Fund",
                    BudgetedAmount = 500000m,
                    ActualAmount = 480000m,
                    VarianceAmount = -20000m,
                    VariancePercentage = -0.04m
                });

                TrendData.Clear();
                for (int i = 1; i <= 12; i++)
                {
                    TrendData.Add(new MonthlyTrend
                    {
                        Month = $"2024-{i:D2}",
                        Budgeted = 100000 + i * 5000,
                        Actual = 95000 + i * 4500,
                        Variance = -5000 - i * 500
                    });
                }

                Insights.Clear();
                Insights.Add("Revenue is trending below budget by 4%");
                Insights.Add("Expense controls are effective");

                UpdateSummaries();
                UpdateFilteredCollections();
            });
        }

        /// <summary>
        /// Loads sample scenario data
        /// </summary>
        private async Task LoadSampleScenarioDataAsync()
        {
            await Task.Run(() =>
            {
                ScenarioProjections.Clear();
                for (int i = 1; i <= ProjectionYears; i++)
                {
                    ScenarioProjections.Add(new YearlyProjection
                    {
                        Year = DateTime.Now.Year + i,
                        ProjectedRevenue = 1600000m + i * 50000,
                        ProjectedExpenses = 1250000m + i * 30000,
                        ProjectedReserves = 350000m + i * 20000,
                        RiskLevel = 0.1m + i * 0.05m
                    });
                }

                Recommendations.Clear();
                Recommendations.Add("Consider 5% rate increase to meet revenue targets");
                Recommendations.Add("Monitor expense growth carefully");

                RecommendationExplanation = "Sample scenario shows balanced growth with moderate risk.";
            });
        }

        /// <summary>
        /// Loads sample forecast data
        /// </summary>
        private async Task LoadSampleForecastDataAsync()
        {
            await Task.Run(() =>
            {
                ForecastData.Clear();
                for (int i = 0; i < ProjectionYears * 12; i++)
                {
                    ForecastData.Add(new ForecastPoint
                    {
                        Date = DateTime.Now.AddMonths(i),
                        PredictedReserves = 300000m + i * 2000,
                        ConfidenceInterval = 5000m
                    });
                }

                RecommendationExplanation = "Sample forecast indicates stable reserve levels with low risk.";
            });
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
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
            // Clean up unmanaged resources if any
            _logger.LogDebug("AnalyticsViewModel disposed");
        }
    }

    /// <summary>
    /// Analytics metric for display
    /// </summary>
    public class AnalyticsMetric
    {
        /// <summary>
        /// Gets or sets the category name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metric value
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Gets or sets the unit of measurement
        /// </summary>
        public string Unit { get; set; } = string.Empty;
    }
}
