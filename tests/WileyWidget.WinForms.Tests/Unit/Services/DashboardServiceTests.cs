using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetEnterpriseSnapshotsAsync_EnrichesRateStudyMetrics_FromBudgetAndEnterpriseData()
    {
        var budgetRepository = new Mock<IBudgetRepository>();
        budgetRepository
            .Setup(repository => repository.GetTownOfWileyBudgetDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TownOfWileyBudget2026>
            {
                new()
                {
                    MappedDepartment = "Water",
                    Category = "Revenue",
                    PriorYearActual = 900_000m,
                    EstimateCurrentYr = 1_050_000m,
                    BudgetYear = 1_100_000m,
                    ActualYTD = 980_000m
                },
                new()
                {
                    MappedDepartment = "Water",
                    Category = "Expense",
                    PriorYearActual = 850_000m,
                    EstimateCurrentYr = 990_000m,
                    BudgetYear = 1_020_000m,
                    ActualYTD = 940_000m
                },
                new()
                {
                    MappedDepartment = "Sewer",
                    Category = "Revenue",
                    PriorYearActual = 700_000m,
                    EstimateCurrentYr = 820_000m,
                    BudgetYear = 840_000m,
                    ActualYTD = 790_000m
                },
                new()
                {
                    MappedDepartment = "Sewer",
                    Category = "Expense",
                    PriorYearActual = 760_000m,
                    EstimateCurrentYr = 910_000m,
                    BudgetYear = 920_000m,
                    ActualYTD = 870_000m
                },
                new()
                {
                    MappedDepartment = "Apartment Housing",
                    Category = "Revenue",
                    PriorYearActual = 350_000m,
                    EstimateCurrentYr = 390_000m,
                    BudgetYear = 405_000m,
                    ActualYTD = 360_000m
                },
                new()
                {
                    MappedDepartment = "Apartment Housing",
                    Category = "Expense",
                    PriorYearActual = 320_000m,
                    EstimateCurrentYr = 355_000m,
                    BudgetYear = 365_000m,
                    ActualYTD = 332_000m
                }
            });

        var accountRepository = new Mock<IMunicipalAccountRepository>();
        var enterpriseRepository = new Mock<IEnterpriseRepository>();
        enterpriseRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Enterprise { Id = 1, Name = "Water", Type = "Water", CurrentRate = 48.50m, MonthlyExpenses = 20_000m, TotalBudget = 90_000m, CitizenCount = 1200 },
                new Enterprise { Id = 2, Name = "Sewer", Type = "Sewer", CurrentRate = 39.00m, MonthlyExpenses = 25_000m, TotalBudget = 50_000m, CitizenCount = 900 },
                new Enterprise { Id = 3, Name = "Apartments", Type = "Apartments", CurrentRate = 0m, MonthlyExpenses = 18_000m, TotalBudget = 72_000m, CitizenCount = 1 }
            });

        var chargeCalculator = new Mock<IChargeCalculatorService>();
        chargeCalculator
            .Setup(service => service.CalculateRecommendedChargeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceChargeRecommendation
            {
                CurrentRate = 48.50m,
                RecommendedRate = 52.75m,
                RateValidation = new RateValidationResult { DebtServiceRatio = 0.18m }
            });
        chargeCalculator
            .Setup(service => service.CalculateRecommendedChargeAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceChargeRecommendation
            {
                CurrentRate = 39.00m,
                RecommendedRate = 46.00m,
                RateValidation = new RateValidationResult { DebtServiceRatio = 0.24m }
            });
        chargeCalculator
            .Setup(service => service.CalculateRecommendedChargeAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceChargeRecommendation
            {
                CurrentRate = 0m,
                RecommendedRate = 0m,
                RateValidation = new RateValidationResult()
            });

        var service = new DashboardService(
            NullLogger<DashboardService>.Instance,
            budgetRepository.Object,
            accountRepository.Object,
            cacheService: null,
            configuration: null,
            enterpriseRepository: enterpriseRepository.Object,
            chargeCalculatorService: chargeCalculator.Object);

        var snapshots = await service.GetEnterpriseSnapshotsAsync();

        snapshots.Should().HaveCount(4);

        var water = snapshots.Single(snapshot => snapshot.Name == "Water");
        water.PriorYearRevenue.Should().Be(900_000m);
        water.CurrentYearEstimatedExpenses.Should().Be(990_000m);
        water.BudgetYearRevenue.Should().Be(1_100_000m);
        water.CurrentRate.Should().Be(48.50m);
        water.RecommendedRate.Should().Be(52.75m);
        water.ReserveCoverageMonths.Should().Be(4.5m);
        water.CoverageRatio.Should().BeGreaterThan(1m);

        var sewer = snapshots.Single(snapshot => snapshot.Name == "Sewer");
        sewer.IsSelfSustaining.Should().BeFalse();
        sewer.RequiresAttention.Should().BeTrue();
        sewer.CrossSubsidyNote.Should().Contain("masked by positive enterprise margins");

        var apartments = snapshots.Single(snapshot => snapshot.Name == "Apartments");
        apartments.DisplayCategory.Should().Be("Operations / income support");
        apartments.InsightSummary.Should().Contain("mask utility losses");
    }
}
