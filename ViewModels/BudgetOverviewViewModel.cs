using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.ViewModels
{
    public class FinancialMetric
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }

    public partial class BudgetOverviewViewModel : ObservableObject
    {
        private readonly StartupOrchestrator _orchestrator;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<BudgetOverviewViewModel> _logger;
        private readonly Tracer _tracer;

        [ObservableProperty]
        private ObservableCollection<FinancialMetric> metrics;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public BudgetOverviewViewModel(
            StartupOrchestrator orchestrator,
            ITelemetryService telemetry,
            ILogger<BudgetOverviewViewModel> logger)
        {
            _orchestrator = orchestrator;
            _telemetry = telemetry;
            _logger = logger;
            _tracer = TracerProvider.Default.GetTracer("BudgetOverviewViewModel");

            Metrics = new ObservableCollection<FinancialMetric>();
        }

        /// <summary>
        /// Load budget data with startup orchestration and telemetry.
        /// Awaits startup completion before loading data.
        /// </summary>
        public async Task LoadBudgetDataAsync()
        {
            using var span = _tracer.StartActiveSpan("BudgetOverview.Load", SpanKind.Client);
            _logger.LogInformation("Loading Budget Overview data with startup orchestration");

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                // Await startup completion (ensures DB, secrets, telemetry are ready)
                await _orchestrator.CompletionTask;

                // Placeholder: Replace with actual repository/service call
                await Task.Delay(500); // Simulate data loading

                var budgetMetrics = new[]
                {
                    new FinancialMetric { Category = "Revenue", Amount = 100000 },
                    new FinancialMetric { Category = "Expenses", Amount = 75000 },
                    new FinancialMetric { Category = "Profit", Amount = 25000 }
                };

                Metrics = new ObservableCollection<FinancialMetric>(budgetMetrics);

                span.SetAttribute("items.count", budgetMetrics.Length);
                _telemetry.RecordMetric("BudgetOverview.Load.Success", 1);
                _logger.LogInformation("Budget Overview loaded successfully: {Count} items", budgetMetrics.Length);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load budget data. Please check logs.";
                _telemetry.RecordException(ex, ("source", "BudgetOverviewViewModel"), ("operation", "Load"));
                _logger.LogError(ex, "Failed to load Budget Overview");
                span.SetStatus(Status.Error.WithDescription(ex.Message));
                _telemetry.RecordMetric("BudgetOverview.Load.Error", 1);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Initialize budget overview on navigation (Prism pattern).
        /// Call this method from your navigation handler or view loaded event.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadBudgetDataAsync();
        }
    }
}
