using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    public interface IChargeCalculatorService
    {
        Task<ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId, CancellationToken cancellationToken = default);
        Task<WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0, CancellationToken cancellationToken = default);
    }
}
// NOTE: Removed duplicate IChargeCalculatorService declaration that caused a
// duplicate-type error during build. If a synchronous CalculateCharge method
// is required, consider adding it to the single interface above.
