using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task RunRateScenarioAsync_UsesPortfolioCurrentRateBaseline()
    {
        var budgetRepository = new Mock<IBudgetRepository>();
        budgetRepository
            .Setup(repository => repository.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(
            [
                new BudgetEntry { BudgetedAmount = 2000m, ActualAmount = 1800m, AccountNumber = "4000" },
                new BudgetEntry { BudgetedAmount = 1500m, ActualAmount = 1200m, AccountNumber = "5000" }
            ]);

        var analyticsRepository = new Mock<IAnalyticsRepository>();
        analyticsRepository
            .Setup(repository => repository.GetPortfolioCurrentRateAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(12.50m);

        var budgetAnalyticsRepository = new Mock<IBudgetAnalyticsRepository>();

        var service = new AnalyticsService(
            budgetRepository.Object,
            analyticsRepository.Object,
            budgetAnalyticsRepository.Object,
            NullLogger<AnalyticsService>.Instance);

        var result = await service.RunRateScenarioAsync(new RateScenarioParameters
        {
            RateIncreasePercentage = 0.10m,
            ExpenseIncreasePercentage = 0.05m,
            ProjectionYears = 2
        });

        result.CurrentRate.Should().Be(12.50m);
        result.ProjectedRate.Should().Be(13.75m);
        result.Projections.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunRateScenarioAsync_ThrowsWhenPortfolioRateIsUnavailable()
    {
        var budgetRepository = new Mock<IBudgetRepository>();
        budgetRepository
            .Setup(repository => repository.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(
            [
                new BudgetEntry { BudgetedAmount = 2000m, ActualAmount = 1800m, AccountNumber = "4000" }
            ]);

        var analyticsRepository = new Mock<IAnalyticsRepository>();
        analyticsRepository
            .Setup(repository => repository.GetPortfolioCurrentRateAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        var budgetAnalyticsRepository = new Mock<IBudgetAnalyticsRepository>();

        var service = new AnalyticsService(
            budgetRepository.Object,
            analyticsRepository.Object,
            budgetAnalyticsRepository.Object,
            NullLogger<AnalyticsService>.Instance);

        var act = async () => await service.RunRateScenarioAsync(new RateScenarioParameters
        {
            RateIncreasePercentage = 0.05m,
            ExpenseIncreasePercentage = 0.02m,
            ProjectionYears = 1
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No enterprise rate data available*");
    }
}
