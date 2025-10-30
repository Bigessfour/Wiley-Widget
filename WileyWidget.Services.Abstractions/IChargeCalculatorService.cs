using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    public interface IChargeCalculatorService
    {
        Task<ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId);
        Task<WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0);
    }
}
// NOTE: Removed duplicate IChargeCalculatorService declaration that caused a
// duplicate-type error during build. If a synchronous CalculateCharge method
// is required, consider adding it to the single interface above.
