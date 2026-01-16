#nullable enable

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
        public int Year { get; set; }
        public decimal ProjectedRate { get; set; }
        public decimal ProjectedRevenue { get; set; }
        public decimal ProjectedExpenses { get; set; }
        public decimal ProjectedBalance { get; set; }
        public decimal ReserveLevel { get; set; }
    }

    /// <summary>
    /// Department impact model for rate increase analysis.
    /// </summary>
    public class DepartmentImpact
    {
        public string DepartmentName { get; set; } = string.Empty;
        public decimal CurrentBudget { get; set; }
        public decimal ProjectedBudget { get; set; }
        public decimal ImpactAmount { get; set; }
        public decimal ImpactPercentage { get; set; }
    }

    /// <summary>
    /// War Room View Model for what-if scenario analysis using Grok AI.
    /// Integrates GrokAgentService to analyze rate scenarios and financial projections.
    /// Supports multi-year "what-if" planning with voice input hints and visual analytics.
    /// </summary>
    public partial class WarRoomViewModel : ViewModelBase, IWarRoomViewModel
    {
        private readonly GrokAgentService? _grokService;
        private readonly RateScenarioTools? _rateScenarioTools;

        #region Observable Properties

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

            RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync);
            ResetCommand = new RelayCommand(Reset);

            Logger.LogInformation("WarRoomViewModel initialized");
        }

        /// <summary>
        /// Executes the scenario analysis.
        /// Calls GrokAgentService with ScenarioInput, expects RateScenarioTools results.
        /// </summary>
        private async Task RunScenarioAsync()
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
                // Mock scenario data (in production, would call _grokService)
                // For now, generate synthetic projections for demonstration
                GenerateScenarioResults(12.4m, 4m, 5);

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
