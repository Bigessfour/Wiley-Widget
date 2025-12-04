#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.ViewModels;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Baseline unit tests for <see cref="BudgetOverviewViewModel"/>.
    /// Verifies that data is loaded from service correctly
    /// and observable properties function as expected.
    /// </summary>
    public sealed class BudgetOverviewViewModelTests
    {
        private readonly Mock<ILogger<BudgetOverviewViewModel>> _mockLogger;
        private readonly Mock<IMainDashboardService> _mockDashboardService;

        public BudgetOverviewViewModelTests()
        {
            _mockLogger = new Mock<ILogger<BudgetOverviewViewModel>>();
            _mockDashboardService = new Mock<IMainDashboardService>();

            // Setup default dashboard data
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DashboardDto(100000m, 75000m, 25000m, 10, 5, DateTime.Now.ToString("g")));
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadDataFromService()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);

            // Act
            await viewModel.InitializeAsync();

            // Assert
            viewModel.Should().NotBeNull();
            viewModel.Metrics.Should().NotBeNull();
            viewModel.Metrics.Should().HaveCount(3);
            viewModel.TotalBudget.Should().Be(100000m);
            viewModel.TotalActual.Should().Be(75000m);
            viewModel.Variance.Should().Be(25000m);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadBudgetMetric()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);

            // Act
            await viewModel.InitializeAsync();

            // Assert
            var budget = viewModel.Metrics.FirstOrDefault(m => m.Category == "Total Budget");
            budget.Should().NotBeNull();
            budget!.Amount.Should().Be(100000m);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadActualMetric()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);

            // Act
            await viewModel.InitializeAsync();

            // Assert
            var actual = viewModel.Metrics.FirstOrDefault(m => m.Category == "Total Actual");
            actual.Should().NotBeNull();
            actual!.Amount.Should().Be(75000m);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadVarianceMetric()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);

            // Act
            await viewModel.InitializeAsync();

            // Assert
            var variance = viewModel.Metrics.FirstOrDefault(m => m.Category == "Variance");
            variance.Should().NotBeNull();
            variance!.Amount.Should().Be(25000m);
        }

        [Fact]
        public async Task Metrics_Property_ShouldBeObservable()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);
            await viewModel.InitializeAsync();

            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BudgetOverviewViewModel.Metrics))
                    propertyChangedRaised = true;
            };

            // Act
            viewModel.Metrics = new System.Collections.ObjectModel.ObservableCollection<FinancialMetric>
            {
                new FinancialMetric { Category = "Test", Amount = 1000 }
            };

            // Assert
            propertyChangedRaised.Should().BeTrue();
            viewModel.Metrics.Should().HaveCount(1);
        }

        [Fact]
        public async Task Metrics_ShouldSupportAddingNewItems()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);
            await viewModel.InitializeAsync();
            var initialCount = viewModel.Metrics.Count;

            // Act
            viewModel.Metrics.Add(new FinancialMetric { Category = "Assets", Amount = 500000 });

            // Assert
            viewModel.Metrics.Should().HaveCount(initialCount + 1);
            viewModel.Metrics.Should().Contain(m => m.Category == "Assets" && m.Amount == 500000);
        }

        [Fact]
        public async Task Metrics_ShouldSupportRemovingItems()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);
            await viewModel.InitializeAsync();
            var firstMetric = viewModel.Metrics.First();

            // Act
            viewModel.Metrics.Remove(firstMetric);

            // Assert
            viewModel.Metrics.Should().HaveCount(2);
        }

        [Fact]
        public async Task Metrics_ShouldSupportClearing()
        {
            // Arrange
            var viewModel = new BudgetOverviewViewModel(_mockLogger.Object, _mockDashboardService.Object);
            await viewModel.InitializeAsync();

            // Act
            viewModel.Metrics.Clear();

            // Assert
            viewModel.Metrics.Should().BeEmpty();
        }

        [Fact]
        public void FinancialMetric_ShouldInitializeWithEmptyCategory()
        {
            // Act
            var metric = new FinancialMetric();

            // Assert
            metric.Category.Should().Be(string.Empty);
            metric.Amount.Should().Be(0);
        }

        [Fact]
        public void FinancialMetric_ShouldAllowPropertyModification()
        {
            // Arrange
            var metric = new FinancialMetric { Category = "Original", Amount = 100 };

            // Act
            metric.Category = "Modified";
            metric.Amount = 200;

            // Assert
            metric.Category.Should().Be("Modified");
            metric.Amount.Should().Be(200);
        }
    }
}
