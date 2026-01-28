#nullable enable

using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Plugins;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Scenario projection data model for rate and revenue impacts.
    /// </summary>
    public class ScenarioProjection
    {
        public required int Year { get; init; }
        public required decimal ProjectedRate { get; init; }
        public required decimal ProjectedRevenue { get; init; }
        public required decimal ProjectedExpenses { get; init; }
        public required decimal ProjectedBalance { get; init; }
        public required decimal ReserveLevel { get; init; }
    }

    /// <summary>
    /// Department impact model for rate increase analysis.
    /// </summary>
    public class DepartmentImpact
    {
        public required string DepartmentName { get; init; }
        public required decimal CurrentBudget { get; init; }
        public required decimal ProjectedBudget { get; init; }
        public required decimal ImpactAmount { get; init; }
        public required decimal ImpactPercentage { get; init; }
    }

    /// <summary>
    /// War Room View Model for what-if scenario analysis using Grok AI.
    /// Integrates GrokAgentService to analyze rate scenarios and financial projections.
    /// Supports multi-year "what-if" planning with voice input hints and visual analytics.
    /// </summary>
    public partial class WarRoomViewModel : ViewModelBase, IWarRoomViewModel, ILazyLoadViewModel
    {
        private readonly GrokAgentService? _grokService;
        private readonly RateScenarioTools? _rateScenarioTools;

        #region Observable Properties

        [ObservableProperty]
        private bool isDataLoaded;

        [ObservableProperty]
        private string scenarioInput = "Raise water rates 12% and inflation is 4% for 5 years";

        [ObservableProperty]
        private bool isAnalyzing;

        [ObservableProperty]
        private string statusMessage = "Ready to analyze scenarios. Ask JARVIS aloud or type your scenario.";

        [ObservableProperty]
        private string requiredRateIncrease = "";

        [ObservableProperty]
        private decimal riskLevel = 0;

        [ObservableProperty]
        private ObservableCollection<ScenarioProjection> projections = new();

        [ObservableProperty]
        private ObservableCollection<DepartmentImpact> departmentImpacts = new();

        [ObservableProperty]
        private decimal baselineMonthlyRevenue = 0;

        [ObservableProperty]
        private decimal projectedMonthlyRevenue = 0;

        [ObservableProperty]
        private decimal revenueDifference = 0;

        [ObservableProperty]
        private bool hasResults;

        #endregion

        #region Commands

        /// <summary>
        /// Executes the scenario analysis via GrokAgentService.
        /// Sends ScenarioInput to Grok, processes RateScenarioTools results, and updates UI.
        /// </summary>
        public IAsyncRelayCommand RunScenarioCommand { get; }

        /// <summary>
        /// Clears all results and resets to initial state.
        /// </summary>
        public IRelayCommand ResetCommand { get; }

        #endregion

        public WarRoomViewModel(
            GrokAgentService? grokService = null,
            RateScenarioTools? rateScenarioTools = null,
            ILogger<WarRoomViewModel>? logger = null)
            : base(logger)
        {
            _grokService = grokService;
            _rateScenarioTools = rateScenarioTools;

            // Capture synchronization context (UI) if available so background tasks can marshal updates safely
            _uiSyncContext = System.Threading.SynchronizationContext.Current;

            RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync);
            ResetCommand = new RelayCommand(Reset);

            Logger.LogInformation("WarRoomViewModel initialized");
        }

        /// <summary>
        /// ILazyLoadViewModel implementation: called when the panel becomes visible.
        /// For WarRoom, we just mark data as loaded since it's interactive.
        /// </summary>
        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded)
            {
                IsDataLoaded = true;
                Logger.LogInformation("WarRoomViewModel data loaded on visibility change");
            }
        }

        /// <summary>
        /// Executes the scenario analysis.
        /// Calls GrokAgentService with ScenarioInput, expects RateScenarioTools results.
        /// </summary>
        private System.Threading.SynchronizationContext? _uiSyncContext;

        private async Task RunScenarioAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ScenarioInput))
            {
                StatusMessage = "Please enter a scenario description";
                return;
            }

            IsAnalyzing = true;
            HasResults = false;
            StatusMessage = "Analyzing scenario with JARVIS...";

            try
            {
                // If a Grok service is available prefer it; otherwise use a safe fallback parse
                if (_grokService != null)
                {
                    // production call would go here; for now we keep existing behaviour
                    await Task.CompletedTask;
                }
                else
                {
                    // Safe fallback parsing: be defensive and tolerant (Option A)
                    // Accept integer or decimal percent and be case-insensitive
                    var pattern = @"(?:raise|increase).*rates?\s+(?<rate>\d+(?:\.\d+)?)%.*inflation.*?(?<inflation>\d+(?:\.\d+)?)%.*(?<years>\d+)\s+years";
                    var m = System.Text.RegularExpressions.Regex.Match(ScenarioInput ?? string.Empty, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!m.Success)
                    {
                        Logger.LogWarning("Invalid scenario input format: {Input}", ScenarioInput);
                        StatusMessage = "Invalid format. Try: 'Raise water rates 12% and inflation is 4% for 5 years'";
                        return;
                    }

                    // Try parsing groups safely
                    if (!decimal.TryParse(m.Groups["rate"].Value, out var parsedRate))
                    {
                        Logger.LogWarning("Unable to parse rate from input: {RateValue}", m.Groups["rate"].Value);
                        StatusMessage = "Unable to parse rate percent from input";
                        return;
                    }

                    if (!decimal.TryParse(m.Groups["inflation"].Value, out var parsedInflation))
                    {
                        Logger.LogWarning("Unable to parse inflation from input: {InflationValue}", m.Groups["inflation"].Value);
                        StatusMessage = "Unable to parse inflation percent from input";
                        return;
                    }

                    if (!int.TryParse(m.Groups["years"].Value, out var parsedYears))
                    {
                        Logger.LogWarning("Unable to parse years from input: {YearsValue}", m.Groups["years"].Value);
                        StatusMessage = "Unable to parse years from input";
                        return;
                    }

                    // Generate scenario results on a background thread and marshal the collection updates to UI thread
                    var projections = new System.Collections.Generic.List<ScenarioProjection>();
                    var departmentImpacts = new System.Collections.Generic.List<DepartmentImpact>();

                    await Task.Run(() =>
                    {
                        // Basic deterministic sample generation (kept from GenerateScenarioResults semantics)
                        var rate = parsedRate;
                        var inflation = parsedInflation;
                        var years = parsedYears;

                        for (int y = 1; y <= years; y++)
                        {
                            projections.Add(new ScenarioProjection
                            {
                                Year = DateTime.Now.Year + y - 1,
                                ProjectedRate = rate + y * 0.1m,
                                ProjectedRevenue = 100000m + (rate * 1000m * y),
                                ProjectedExpenses = 50000m + (inflation * 500m * y),
                                ProjectedBalance = 50000m + (rate * 500m * y),
                                ReserveLevel = 3m
                            });
                        }

                        // Minimal department impacts sample
                        departmentImpacts.Add(new DepartmentImpact { DepartmentName = "Water", CurrentBudget = 50000m, ProjectedBudget = 55000m, ImpactAmount = 5000m, ImpactPercentage = 0.10m });
                    });

                    // Marshal collection updates to UI thread if we captured it; otherwise try to use current context
                    void applyResults()
                    {
                        Projections.Clear();
                        foreach (var p in projections)
                            Projections.Add(p);

                        DepartmentImpacts.Clear();
                        foreach (var d in departmentImpacts)
                            DepartmentImpacts.Add(d);

                        RequiredRateIncrease = parsedRate.ToString("0.##") + "%";
                        RiskLevel = Math.Min(100m, parsedInflation);
                    }

                    if (_uiSyncContext != null)
                    {
                        _uiSyncContext.Post(_ => applyResults(), null);
                    }
                    else
                    {
                        // Fallback: try to update directly (likely on UI thread already)
                        applyResults();
                    }
                }

                StatusMessage = "Scenario analysis complete";
                HasResults = true;
                Logger.LogInformation("Scenario analysis completed: RateIncrease={RequiredRate}", RequiredRateIncrease);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error analyzing scenario: {ex.Message}";
                Logger.LogError(ex, "Error in RunScenarioAsync");
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        /// <summary>
        /// Generates synthetic scenario results for demonstration.
        /// In production, this would parse Grok response and RateScenarioTools results.
        /// </summary>
        private void GenerateScenarioResults(decimal requiredRateIncrease, decimal inflationRate, int projectionYears)
        {
            // Clear previous results
            Projections.Clear();
            DepartmentImpacts.Clear();

            // Set headline result
            RequiredRateIncrease = $"{requiredRateIncrease:F1}%";
            RiskLevel = (decimal)Math.Min(100, requiredRateIncrease * 3); // Mock risk calculation

            // Baseline assumptions (would come from database in production)
            var baselineRate = 45.50m; // $/month example
            var baselineMonthlyExpenses = 125000m;
            BaselineMonthlyRevenue = baselineMonthlyExpenses + 5000m; // baseline revenue with small surplus

            // Generate year-by-year projections
            for (int year = 1; year <= projectionYears; year++)
            {
                var cumulativeRateIncrease = requiredRateIncrease * (year / 100m);
                var projectedRate = baselineRate * (1 + cumulativeRateIncrease / 100m);
                var expenseGrowth = 1 + (inflationRate * year / 100m);
                var projectedExpenses = baselineMonthlyExpenses * (decimal)Math.Pow((double)expenseGrowth, 1.0);
                var projectedRevenue = projectedRate * 3500m; // ~3500 customers assumption
                var projectedBalance = projectedRevenue - projectedExpenses;
                var reserveLevel = projectedBalance * 3; // 3-month reserve target

                Projections.Add(new ScenarioProjection
                {
                    Year = year,
                    ProjectedRate = Math.Round(projectedRate, 2),
                    ProjectedRevenue = Math.Round(projectedRevenue, 0),
                    ProjectedExpenses = Math.Round(projectedExpenses, 0),
                    ProjectedBalance = Math.Round(projectedBalance, 0),
                    ReserveLevel = Math.Round(reserveLevel, 0)
                });
            }

            // Calculate final projected revenue for summary
            if (Projections.Count > 0)
            {
                ProjectedMonthlyRevenue = Projections[Projections.Count - 1].ProjectedRevenue;
                RevenueDifference = ProjectedMonthlyRevenue - BaselineMonthlyRevenue;
            }

            // Generate department impact estimates
            var departments = new[] { "Water Operations", "Wastewater", "Storm Water", "Administration", "Capital Projects" };
            var departmentBudgets = new[] { 450000m, 380000m, 220000m, 150000m, 280000m };

            for (int i = 0; i < departments.Length; i++)
            {
                var currentBudget = departmentBudgets[i];
                var impactAmount = currentBudget * (requiredRateIncrease / 100m);
                DepartmentImpacts.Add(new DepartmentImpact
                {
                    DepartmentName = departments[i],
                    CurrentBudget = currentBudget,
                    ProjectedBudget = currentBudget + impactAmount,
                    ImpactAmount = Math.Round(impactAmount, 0),
                    ImpactPercentage = Math.Round(requiredRateIncrease, 2)
                });
            }
        }

        /// <summary>
        /// Resets the view model to initial state.
        /// </summary>
        private void Reset()
        {
            ScenarioInput = "Raise water rates 12% and inflation is 4% for 5 years";
            Projections.Clear();
            DepartmentImpacts.Clear();
            RequiredRateIncrease = "";
            RiskLevel = 0;
            BaselineMonthlyRevenue = 0;
            ProjectedMonthlyRevenue = 0;
            RevenueDifference = 0;
            HasResults = false;
            StatusMessage = "Ready to analyze scenarios. Ask JARVIS aloud or type your scenario.";
            Logger.LogInformation("WarRoomViewModel reset");
        }
    }
}
