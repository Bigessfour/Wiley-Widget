using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

public interface IChargeCalculatorService
{
    Task<ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId);
    Task<WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0);
}
