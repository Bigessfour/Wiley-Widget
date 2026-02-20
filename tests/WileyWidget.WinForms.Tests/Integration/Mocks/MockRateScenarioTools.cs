#nullable enable

// NOTE: RateScenarioTools is sealed — this mock CANNOT inherit from it.
// WarRoomViewModel holds a RateScenarioTools? field directly. Since the constructor null-guards
// this dependency, integration tests can either:
//   (a) Pass null for RateScenarioTools (ViewModel gracefully degrades to fallback parse logic), or
//   (b) Use this standalone fake's return value by reflecting it into ViewModel state
//       after construction, or refactor WarRoomViewModel to depend on an interface.
//
// This class mirrors the public method signature of RateScenarioTools.RunWhatIfScenario
// and returns deterministic data — no DB, no scope factory, no real services required.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.WinForms.Plugins;

namespace WileyWidget.WinForms.Tests.Integration.Mocks;

/// <summary>
/// Standalone fake that mirrors <see cref="RateScenarioTools.RunWhatIfScenario"/>.
/// Returns deterministic mock data without touching database or real services.
/// Cannot subclass <c>RateScenarioTools</c> (sealed class).
/// </summary>
public sealed class MockRateScenarioTools
{
    /// <summary>
    /// Mirrors <see cref="RateScenarioTools.RunWhatIfScenario"/> and returns a canned result.
    /// </summary>
    public Task<RateScenarioTools.WhatIfScenarioResult> RunWhatIfScenario(
        string scenarioDescription,
        decimal rateIncreasePct,
        int years = 5,
        CancellationToken cancellationToken = default)
    {
        var baseYear = DateTime.Now.Year;

        var progression = new List<RateScenarioTools.MonthlyProjection>();
        for (int y = 0; y < years; y++)
        {
            for (int m = 1; m <= 12; m++)
            {
                progression.Add(new RateScenarioTools.MonthlyProjection
                {
                    Year = baseYear + y,
                    Month = m,
                    MonthlyRate = Math.Round(48.50m * (1m + rateIncreasePct / 100m * (y + 1) / years), 2),
                    MonthlyRevenue = 120_000m + 2_500m * (y * 12 + m),
                    MonthlyExpenses = 105_000m + 1_000m * (y * 12 + m),
                    MonthlyBalance = 15_000m + 1_500m * (y * 12 + m),
                    CumulativeBalance = 15_000m + 1_500m * (y * 12 + m) * (y * 12 + m) / 2
                });
            }
        }

        var result = new RateScenarioTools.WhatIfScenarioResult
        {
            ScenarioName = $"Mock: {rateIncreasePct}% rate increase over {years} years",
            ScenarioDescription = scenarioDescription,
            RateIncreasePercentage = rateIncreasePct,
            BaselineRate = 48.50m,
            BaselineMonthlyExpenses = 105_000m,
            BaselineMonthlyBalance = 15_000m,
            ProjectionYears = years,
            ProjectedRate = Math.Round(48.50m * (1m + rateIncreasePct / 100m), 2),
            ProjectedMonthlyRevenue = 145_000m,
            ProjectedMonthlyExpenses = 110_000m,
            ProjectedMonthlyBalance = 35_000m,
            RiskLevel = "Low",
            Recommendations = new List<string> { "Maintain 90-day operating reserve.", "Review annually." },
            MonthlyProgression = progression
        };

        return Task.FromResult(result);
    }
}
