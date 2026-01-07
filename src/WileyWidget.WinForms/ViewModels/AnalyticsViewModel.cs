using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using System; // Added for ArgumentNullException, DateTime, etc.

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for analytics functionality providing exploratory analysis, scenario modeling, and forecasting.
    /// Supports budget variance analysis, rate scenario projections, and reserve forecasting with AI-driven insights.
    /// </summary>
    public partial class AnalyticsViewModel : ObservableObject, IDisposable
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsViewModel> _logger;

        /// <summary>
        /// Gets or sets a value indicating whether data is currently being loaded or processed.
        /// </summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Gets or sets the current status text displayed to the user.
        /// </summary>
        [ObservableProperty]
        private string statusText = "Ready";

        /// <summary>
        /// Gets or sets the collection of analytics metrics (e.g., Revenue, Expenses, Reserves).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<AnalyticsMetric> metrics = new();

        /// <summary>
        /// Gets or sets the collection of top variance analyses for budget accounts.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<VarianceAnalysis> topVariances = new();

        /// <summary>
        /// Gets or sets the collection of monthly trend data for budget vs actual comparison.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MonthlyTrend> trendData = new();

        /// <summary>
        /// Gets or sets the collection of yearly projections from scenario analysis.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<YearlyProjection> scenarioProjections = new();

        /// <summary>
        /// Gets or sets the collection of forecast data points for reserve predictions.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ForecastPoint> forecastData = new();

        /// <summary>
        /// Gets or sets the collection of key insights generated from exploratory analysis.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> insights = new();

        /// <summary>
        /// Gets or sets the collection of recommendations from scenario and forecast analysis.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> recommendations = new();

        /// <summary>
        /// Gets or sets the rate increase percentage for scenario modeling (0-100).
        /// Default is 5.0%.
        /// </summary>
        [ObservableProperty]
        private decimal rateIncreasePercentage = 5.0m;

        /// <summary>
        /// Gets or sets the expense increase percentage for scenario modeling (0-100).
        /// Default is 3.0%.
        /// </summary>
        [ObservableProperty]
        private decimal expenseIncreasePercentage = 3.0m;

        /// <summary>
        /// Gets or sets the revenue target increase percentage for scenario modeling (0-100).
        /// Default is 10.0%.
        /// </summary>
        [ObservableProperty]
        private decimal revenueTargetPercentage = 10.0m;

        /// <summary>
        /// Gets or sets the number of years to project for forecasting (1-10).
        /// Default is 3 years.
        /// </summary>
        [ObservableProperty]
        private int projectionYears = 3;

        /// <summary>
        /// Gets or sets the total budgeted amount across all analyzed accounts.
        /// </summary>
        [ObservableProperty]
        private decimal totalBudgetedAmount;

        /// <summary>
        /// Gets or sets the total actual amount across all analyzed accounts.
        /// </summary>
        [ObservableProperty]
        private decimal totalActualAmount;

        /// <summary>
        /// Gets or sets the total variance amount (Actual - Budgeted).
        /// </summary>
        [ObservableProperty]
        private decimal totalVarianceAmount;

        /// <summary>
        /// Gets or sets the average variance percentage across all analyzed accounts.
        /// </summary>
        [ObservableProperty]
        private decimal averageVariancePercentage;

        /// <summary>
        /// Gets or sets the detailed explanation of recommendations from analysis.
        /// </summary>
        [ObservableProperty]
        private string recommendationExplanation = string.Empty;

        /// <summary>
        /// Gets or sets the filtered collection of metrics based on search criteria.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<AnalyticsMetric> filteredMetrics = new();

        /// <summary>
        /// Gets or sets the filtered collection of variances based on search criteria.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<VarianceAnalysis> filteredTopVariances = new();

        /// <summary>
        /// Gets or sets the search text for filtering metrics.
        /// </summary>
        [ObservableProperty]
        private string metricsSearchText = string.Empty;

        /// <summary>
        /// Gets or sets the search text for filtering variances.
        /// </summary>
        [ObservableProperty]
        private string variancesSearchText = string.Empty;

        /// <summary>
        /// Gets the command to perform exploratory analysis on budget data.
        /// Analyzes category breakdowns, top variances, trends, and generates insights.
        /// </summary>
        public IAsyncRelayCommand PerformAnalysisCommand { get; }

        /// <summary>
        /// Gets the command to run scenario analysis with rate adjustments.
        /// Projects revenue and reserve impacts based on rate, expense, and revenue parameters.
        /// </summary>
        public IAsyncRelayCommand RunScenarioCommand { get; }

        /// <summary>
        /// Gets the command to generate reserve forecast for future years.
        /// Provides predictive modeling with confidence intervals and risk assessment.
        /// </summary>
        public IAsyncRelayCommand GenerateForecastCommand { get; }

        /// <summary>
        /// Gets the command to refresh all analytics data.
        /// Executes all three analysis types in sequence: exploratory, scenario, and forecast.
        /// </summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalyticsViewModel"/> class.
        /// </summary>
        /// <param name="analyticsService">The analytics service for performing analysis operations.</param>
        /// <param name="logger">The logger instance for diagnostic logging.</param>
        /// <exception cref="ArgumentNullException">Thrown when analyticsService or logger is null.</exception>
        public AnalyticsViewModel(IAnalyticsService analyticsService, ILogger<AnalyticsViewModel> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PerformAnalysisCommand = new AsyncRelayCommand(PerformExploratoryAnalysisAsync);
            RunScenarioCommand = new AsyncRelayCommand(RunRateScenarioAsync);
            GenerateForecastCommand = new AsyncRelayCommand(GenerateReserveForecastAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAllDataAsync);
        }

        /// <summary>
        /// Explicit async initialization for AnalyticsViewModel. Call after construction.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await RefreshCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AnalyticsViewModel.InitializeAsync");
            }
        }
        /// <summary>
        /// Performs exploratory data analysis on budget data asynchronously.
        /// Analyzes fiscal year data to identify category breakdowns, top variances, trends, and insights.
        /// Falls back to sample data if service fails.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Runs rate scenario analysis to project financial impacts asynchronously.
        /// Models the effects of rate, expense, and revenue adjustments over multiple years.
        /// Falls back to sample data if service fails.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Generates reserve forecast for future years asynchronously.
        /// Provides predictive modeling with confidence intervals and risk assessment.
        /// Falls back to sample data if service fails.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Refreshes all analytics data by executing all analysis types in sequence.
        /// Performs exploratory analysis, scenario modeling, and forecast generation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshAllDataAsync()
        {
            await PerformExploratoryAnalysisAsync();
            await RunRateScenarioAsync();
            await GenerateReserveForecastAsync();
        }

        /// <summary>
        /// Updates all summary properties (totals and averages) based on current variance data.
        /// Calculates TotalBudgetedAmount, TotalActualAmount, TotalVarianceAmount, and AverageVariancePercentage.
        /// </summary>
        private void UpdateSummaries()
        {
            TotalBudgetedAmount = TopVariances.Sum(v => v.BudgetedAmount);
            TotalActualAmount = TopVariances.Sum(v => v.ActualAmount);
            TotalVarianceAmount = TopVariances.Sum(v => v.VarianceAmount);
            AverageVariancePercentage = TopVariances.Any() ? TopVariances.Average(v => v.VariancePercentage) : 0;
        }

        /// <summary>
        /// Updates filtered collections based on search criteria.
        /// Filters Metrics and TopVariances by MetricsSearchText and VariancesSearchText respectively.
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
        /// Called when the MetricsSearchText property changes.
        /// Triggers filtering of the metrics collection.
        /// </summary>
        /// <param name="value">The new search text value.</param>
        partial void OnMetricsSearchTextChanged(string value) => UpdateFilteredCollections();

        /// <summary>
        /// Called when the VariancesSearchText property changes.
        /// Triggers filtering of the variances collection.
        /// </summary>
        /// <param name="value">The new search text value.</param>
        partial void OnVariancesSearchTextChanged(string value) => UpdateFilteredCollections();

        /// <summary>
        /// Loads sample analytics data for design-time preview or service failure fallback.
        /// Populates Metrics, TopVariances, TrendData, and Insights with realistic test data.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Loads sample scenario projection data for service failure fallback.
        /// Generates multi-year projections with revenue, expenses, reserves, and risk levels.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Loads sample forecast data for service failure fallback.
        /// Generates monthly reserve predictions with confidence intervals for the projection period.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
