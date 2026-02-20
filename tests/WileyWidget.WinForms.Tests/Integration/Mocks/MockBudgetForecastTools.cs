#nullable enable

// NOTE: BudgetForecastTools is sealed — this mock CANNOT inherit from it.
// WarRoomViewModel holds a BudgetForecastTools? field directly. Since all service dependencies
// in WarRoomViewModel are nullable, integration tests can pass null to skip real forecast logic,
// or use this standalone fake to drive ViewModel state directly (see ExportForecastAsync).
//
// This class mirrors the public method signature of BudgetForecastTools.ForecastNextYearBudget
// and returns deterministic data — no DB, no scope factory, no real services required.

using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Tests.Integration.Mocks;

/// <summary>
/// Standalone fake that mirrors <see cref="WileyWidget.WinForms.Plugins.BudgetForecastTools.ForecastNextYearBudget"/>.
/// Returns a deterministic <see cref="BudgetForecastResult"/> without touching DB or AI services.
/// Cannot subclass <c>BudgetForecastTools</c> (sealed class).
/// </summary>
public sealed class MockBudgetForecastTools
{
    /// <summary>
    /// Mirrors <c>BudgetForecastTools.ForecastNextYearBudget</c> and returns a canned valid result.
    /// Parameter name <c>useAiRecommendations</c> matches the real method signature.
    /// </summary>
    public Task<BudgetForecastResult> ForecastNextYearBudget(
        int enterpriseId,
        int currentFiscalYear,
        decimal? inflationRate = null,
        string? goals = null,
        bool useAiRecommendations = true,
        CancellationToken cancellationToken = default)
    {
        var result = new BudgetForecastResult
        {
            EnterpriseId = enterpriseId,
            EnterpriseName = "Wiley Utilities (Mock)",
            CurrentFiscalYear = currentFiscalYear,
            ProposedFiscalYear = currentFiscalYear + 1,
            TotalCurrentBudget = 2_250_000m,
            TotalProposedBudget = 2_450_000m,
            TotalIncrease = 200_000m,
            TotalIncreasePercent = 8.5m,
            InflationRate = inflationRate ?? 0.035m,
            Goals = goals is not null ? new System.Collections.Generic.List<string>(goals.Split(',')) : new System.Collections.Generic.List<string>(),
            Summary = "Mock forecast — no DB or AI required. Projected surplus: healthy.",
            IsValid = true,
            ProposedLineItems = new System.Collections.Generic.List<ProposedLineItem>
            {
                new() { Category = "Water Operations", Description = "Operating costs", CurrentAmount = 850_000m, ProposedAmount = 922_250m, Increase = 72_250m, IncreasePercent = 8.5m, Justification = "Inflation adjustment." },
                new() { Category = "Capital Projects",  Description = "Infrastructure",  CurrentAmount = 400_000m, ProposedAmount = 434_000m, Increase = 34_000m,  IncreasePercent = 8.5m, Justification = "Pipe replacement schedule." }
            }
        };

        return Task.FromResult(result);
    }
}
