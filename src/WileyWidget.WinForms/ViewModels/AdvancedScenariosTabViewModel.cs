using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Export;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// vNext scenarios tab ViewModel with AI narrative and export helpers.
/// </summary>
public partial class AdvancedScenariosTabViewModel : AnalyticsTabViewModelBase, IAdvancedScenariosTabViewModel
{
    private readonly IAIService _aiService;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<AdvancedScenariosTabViewModel> _logger;

    [ObservableProperty]
    private decimal rateIncreasePercent;

    [ObservableProperty]
    private decimal expenseIncreasePercent;

    [ObservableProperty]
    private decimal revenueTargetPercent;

    [ObservableProperty]
    private int projectionYears;

    [ObservableProperty]
    private ObservableCollection<YearlyProjection> projections = new();

    [ObservableProperty]
    private ObservableCollection<string> recommendations = new();

    [ObservableProperty]
    private string aiNarrative = "Run a scenario to generate an AI summary.";

    [ObservableProperty]
    private string statusMessage = "Ready";

    public IAsyncRelayCommand RunScenarioCommand { get; }
    public IAsyncRelayCommand ExportProjectionsCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }

    public AdvancedScenariosTabViewModel(
        IAnalyticsService analyticsService,
        IAIService aiService,
        IExcelExportService excelExportService,
        ILogger<AdvancedScenariosTabViewModel> logger)
        : base(analyticsService, logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RateIncreasePercent = 3.5m;
        ExpenseIncreasePercent = 2.8m;
        RevenueTargetPercent = 4.0m;
        ProjectionYears = 5;

        RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync);
        ExportProjectionsCommand = new AsyncRelayCommand(ExportProjectionsAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
    }

    protected override Task LoadDataAsync()
    {
        return ResetAsync();
    }

    private async Task RunScenarioAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Running scenario with AI analysis...";

        try
        {
            var parameters = new RateScenarioParameters
            {
                RateIncreasePercentage = RateIncreasePercent,
                ExpenseIncreasePercentage = ExpenseIncreasePercent,
                RevenueTargetPercentage = RevenueTargetPercent,
                ProjectionYears = ProjectionYears
            };

            var result = await AnalyticsService.RunRateScenarioAsync(parameters).ConfigureAwait(false);

            Projections.Clear();
            foreach (var projection in result.Projections ?? Enumerable.Empty<YearlyProjection>())
            {
                Projections.Add(projection);
            }

            Recommendations.Clear();
            foreach (var rec in result.Recommendations ?? Enumerable.Empty<string>())
            {
                Recommendations.Add(rec);
            }

            AiNarrative = await BuildNarrativeAsync(result).ConfigureAwait(false);
            StatusMessage = $"Scenario complete - {Projections.Count} year(s) projected";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error running scenario";
            _logger.LogError(ex, "Advanced scenarios run failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<string> BuildNarrativeAsync(RateScenarioResult result)
    {
        try
        {
            var last = result.Projections?.LastOrDefault();
            var reserves = last?.ProjectedReserves ?? 0m;
            var risk = last?.RiskLevel ?? 0m;
            var prompt = $"""
You are the Wiley Widget AI Analyst for Colorado municipalities.
Scenario: rate +{RateIncreasePercent:0.##}%, expense +{ExpenseIncreasePercent:0.##}%, revenue target {RevenueTargetPercent:0.##}%.
Latest projection: FY {last?.Year}, end reserves {reserves:C0}, risk {risk:P0}.
Provide a concise 3-sentence narrative highlighting drought/mill levy/utility board readiness.
""";

            var response = await _aiService.GetChatCompletionAsync(prompt, CancellationToken.None).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(response)
                ? "AI narrative unavailable. Review projections and recommendations."
                : response.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI narrative for advanced scenarios");
            return "AI narrative unavailable. Review projections and recommendations.";
        }
    }

    private async Task ExportProjectionsAsync()
    {
        if (!Projections.Any())
        {
            StatusMessage = "No projections to export";
            return;
        }

        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"WileyWidget_Scenario_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            var columns = new Dictionary<string, Func<YearlyProjection, object>>
            {
                { "Year", p => p.Year },
                { "Projected Revenue", p => p.ProjectedRevenue },
                { "Projected Expenses", p => p.ProjectedExpenses },
                { "Projected Reserves", p => p.ProjectedReserves },
                { "Risk", p => p.RiskLevel }
            };

            await _excelExportService.ExportGenericDataAsync(Projections, path, "Scenario Projections", columns).ConfigureAwait(false);
            StatusMessage = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed";
            _logger.LogError(ex, "Advanced scenarios export failed");
        }
    }

    private Task ResetAsync()
    {
        RateIncreasePercent = 3.5m;
        ExpenseIncreasePercent = 2.8m;
        RevenueTargetPercent = 4.0m;
        ProjectionYears = 5;
        Projections.Clear();
        Recommendations.Clear();
        AiNarrative = "Reset to base case. Run a new scenario.";
        StatusMessage = "Ready";
        return Task.CompletedTask;
    }
}
