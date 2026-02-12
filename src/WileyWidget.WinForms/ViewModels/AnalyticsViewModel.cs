using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for analytics functionality providing exploratory analysis, scenario modeling, and forecasting.
    /// Supports budget variance analysis, rate scenario projections, and reserve forecasting with AI-driven insights.
    /// </summary>
    public partial class AnalyticsViewModel : ObservableObject, IAnalyticsViewModel, IDisposable, ILazyLoadViewModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether data has been loaded.
        /// </summary>
        [ObservableProperty]
        private bool isDataLoaded;

        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded && !IsLoading)
            {
                await RefreshAllDataAsync(_lifecycleCts.Token);
                IsDataLoaded = true;
            }
        }

        private readonly IAnalyticsService _analyticsService;
        private readonly ICircuitBreakerService _circuitBreaker;
        private readonly ILogger<AnalyticsViewModel> _logger;
        private readonly CancellationTokenSource _lifecycleCts = new();
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private bool _disposed;

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

        /// <summary>Selected entity/fund name for scoping analytics (e.g., "Wiley Sanitation District").</summary>
        [ObservableProperty]
        private string? selectedEntity;

        /// <summary>Available entities/fund names for selection in UI.</summary>
        [ObservableProperty]
        private ObservableCollection<string> availableEntities = new();

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

        public AnalyticsViewModel(IAnalyticsService analyticsService, ICircuitBreakerService circuitBreaker, ILogger<AnalyticsViewModel> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PerformAnalysisCommand = new AsyncRelayCommand(() => PerformExploratoryAnalysisAsync(_lifecycleCts.Token));
            RunScenarioCommand = new AsyncRelayCommand(() => RunRateScenarioAsync(_lifecycleCts.Token));
            GenerateForecastCommand = new AsyncRelayCommand(() => GenerateReserveForecastAsync(_lifecycleCts.Token));
            RefreshCommand = new AsyncRelayCommand(() => RefreshAllDataAsync(_lifecycleCts.Token));

            _propertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedEntity))
                {
                    _ = PerformExploratoryAnalysisAsync(_lifecycleCts.Token);
                }
            };
            PropertyChanged += _propertyChangedHandler;

            // Optimization: Defer data loading until the associated panel becomes visible.
            // This is handled by ILazyLoadViewModel via OnVisibilityChangedAsync.
        }

        /// <summary>
        /// Performs exploratory data analysis asynchronously
        /// </summary>
        private async Task PerformExploratoryAnalysisAsync(CancellationToken cancellationToken)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusText = "Performing exploratory analysis...";

                var startDate = new DateTime(DateTime.Now.Year - 1, 7, 1);
                var endDate = new DateTime(DateTime.Now.Year, 6, 30);

                var result = await _circuitBreaker.ExecuteAsync(async (ct) =>
                    await _analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate, SelectedEntity, ct), cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

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

                ApplyAvailableEntities(result.AvailableEntities);

                UpdateSummaries();
                UpdateFilteredCollections();

                StatusText = "Analysis complete";
                _logger.LogInformation("Exploratory analysis completed successfully");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("temporarily unavailable"))
            {
                // Circuit breaker is open - show error
                StatusText = "Service unavailable - contact support. No data available.";
                _logger.LogWarning(ex, "Analytics service unavailable");
                // Do not load sample data
            }
            catch (OperationCanceledException)
            {
                StatusText = "Analysis cancelled";
            }
            catch (Exception ex)
            {
                StatusText = "Analysis failed - contact support. No real data available.";
                _logger.LogError(ex, "Error performing exploratory analysis");
                // Do not load sample data - never show fake data in production
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Runs rate scenario analysis asynchronously
        /// </summary>
        private async Task RunRateScenarioAsync(CancellationToken cancellationToken)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

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

                var result = await _circuitBreaker.ExecuteAsync(async (ct) =>
                    await _analyticsService.RunRateScenarioAsync(parameters, ct), cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

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
            catch (InvalidOperationException ex) when (ex.Message.Contains("temporarily unavailable"))
            {
                StatusText = "Service unavailable - no scenario data available.";
                _logger.LogWarning(ex, "Analytics service unavailable while running scenario analysis");
                await ClearScenarioDataAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Scenario cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Scenario failed: {ex.Message}";
                _logger.LogError(ex, "Error running rate scenario");
                await ClearScenarioDataAsync(cancellationToken);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates reserve forecast asynchronously
        /// </summary>
        private async Task GenerateReserveForecastAsync(CancellationToken cancellationToken)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusText = "Generating forecast...";

                var result = await _circuitBreaker.ExecuteAsync(async (ct) =>
                    await _analyticsService.GenerateReserveForecastAsync(ProjectionYears, ct), cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

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
            catch (InvalidOperationException ex) when (ex.Message.Contains("temporarily unavailable"))
            {
                StatusText = "Service unavailable - no forecast data available.";
                _logger.LogWarning(ex, "Analytics service unavailable while generating forecast");
                await ClearForecastDataAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Forecast cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Forecast failed: {ex.Message}";
                _logger.LogError(ex, "Error generating reserve forecast");
                await ClearForecastDataAsync(cancellationToken);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes all analytics data asynchronously
        /// </summary>
        private async Task RefreshAllDataAsync(CancellationToken cancellationToken)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await PerformExploratoryAnalysisAsync(cancellationToken);
            await RunRateScenarioAsync(cancellationToken);
            await GenerateReserveForecastAsync(cancellationToken);
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

        private async Task ClearAnalyticsDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ClearAnalyticsDataAsync called: clearing analytics collections.");
            Metrics.Clear();
            TopVariances.Clear();
            TrendData.Clear();
            Insights.Clear();
            UpdateSummaries();
            UpdateFilteredCollections();
            await Task.CompletedTask;
        }

        private async Task ClearScenarioDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ClearScenarioDataAsync called: clearing scenario collections.");
            ScenarioProjections.Clear();
            Recommendations.Clear();
            RecommendationExplanation = string.Empty;
            await Task.CompletedTask;
        }

        private async Task ClearForecastDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ClearForecastDataAsync called: clearing forecast collections.");
            ForecastData.Clear();
            RecommendationExplanation = string.Empty;
            await Task.CompletedTask;
        }

        private void ApplyAvailableEntities(IEnumerable<string> availableEntitiesFromResult)
        {
            AvailableEntities = new ObservableCollection<string>(
                availableEntitiesFromResult
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .Prepend("All Entities"));
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
            if (!disposing || _disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _lifecycleCts.Cancel();
            }
            catch
            {
                // Ignore cancellation race
            }

            PropertyChanged -= _propertyChangedHandler;
            _lifecycleCts.Dispose();
            _logger.LogDebug("AnalyticsViewModel disposed");
        }
    }

    /// <summary>
    /// Analytics metric data model for displaying metrics in the UI.
    /// </summary>
    public class AnalyticsMetric
    {
        /// <summary>
        /// Gets or sets the category name
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the metric value
        /// </summary>
        public required decimal Value { get; init; }

        /// <summary>
        /// Gets or sets the unit of measurement
        /// </summary>
        public required string Unit { get; init; }
    }
}
