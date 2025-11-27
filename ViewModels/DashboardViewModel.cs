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
    public class DashboardMetric
    {
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly StartupOrchestrator _orchestrator;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly Tracer _tracer;

        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public DashboardViewModel(
            StartupOrchestrator orchestrator,
            ITelemetryService telemetry,
            ILogger<DashboardViewModel> logger,
            IDashboardService dashboardService)
        {
            _orchestrator = orchestrator;
            _telemetry = telemetry;
            _logger = logger;
            _dashboardService = dashboardService;
            _tracer = TracerProvider.Default.GetTracer("DashboardViewModel");

            Metrics = new ObservableCollection<DashboardMetric>();
        }

        /// <summary>
        /// Load dashboard data with startup orchestration and telemetry.
        /// Awaits startup completion before loading data.
        /// </summary>
        public async Task LoadDashboardAsync()
        {
            using var span = _tracer.StartActiveSpan("Dashboard.Load", SpanKind.Client);
            _logger.LogInformation("Loading Dashboard data with startup orchestration");

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                // Await startup completion (ensures DB, secrets, telemetry are ready)
                await _orchestrator.CompletionTask;

                var data = await _dashboardService.GetDashboardDataAsync();
                var dashboardMetrics = data.Select(m => new DashboardMetric
                {
                    Name = m.Name,
                    Value = (double)m.Value
                }).ToList();

                Metrics = new ObservableCollection<DashboardMetric>(dashboardMetrics);

                span.SetAttribute("items.count", dashboardMetrics.Count);
                _telemetry.RecordMetric("Dashboard.Load.Success", 1);
                _logger.LogInformation("Dashboard loaded successfully: {Count} items", dashboardMetrics.Count);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load dashboard data. Please check logs.";
                _telemetry.RecordException(ex, ("source", "DashboardViewModel"), ("operation", "Load"));
                _logger.LogError(ex, "Failed to load Dashboard");
                span.SetStatus(Status.Error.WithDescription(ex.Message));
                _telemetry.RecordMetric("Dashboard.Load.Error", 1);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Initialize dashboard on navigation (Prism pattern).
        /// Call this method from your navigation handler or view loaded event.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadDashboardAsync();
        }
    }
}
