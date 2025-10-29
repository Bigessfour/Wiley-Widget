using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Events;
using Xunit;
using WileyWidget.UI.ViewModels;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using Unit.TestHelpers;

namespace Unit.ViewModels
{
    /// <summary>
    /// Unit tests for AnalyticsViewModel
    /// </summary>
    public class AnalyticsViewModelTests
    {
        private readonly Mock<IDispatcherHelper> _dispatcherHelperMock;
        private readonly Mock<ILogger<AnalyticsViewModel>> _loggerMock;
        private readonly Mock<IBudgetRepository> _budgetRepositoryMock;
        private readonly Mock<IMunicipalAccountRepository> _municipalAccountRepositoryMock;
        private readonly Mock<IReportExportService> _reportExportServiceMock;
        private readonly Mock<IEnterpriseRepository> _enterpriseRepositoryMock;
        private readonly Mock<IEventAggregator> _eventAggregatorMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private readonly Mock<IGrokSupercomputer> _grokSupercomputerMock;

        public AnalyticsViewModelTests()
        {
            _dispatcherHelperMock = new Mock<IDispatcherHelper>();
            _loggerMock = new Mock<ILogger<AnalyticsViewModel>>();
            _budgetRepositoryMock = new Mock<IBudgetRepository>();
            _municipalAccountRepositoryMock = new Mock<IMunicipalAccountRepository>();
            _reportExportServiceMock = new Mock<IReportExportService>();
            _enterpriseRepositoryMock = new Mock<IEnterpriseRepository>();
            _eventAggregatorMock = new Mock<IEventAggregator>();
            _cacheServiceMock = new Mock<ICacheService>();
            _grokSupercomputerMock = new Mock<IGrokSupercomputer>();

            // Setup dispatcher helper to execute actions synchronously for testing
            _dispatcherHelperMock.Setup(d => d.Invoke(It.IsAny<Action>()))
                .Callback<Action>(action => action());
        }

        [Fact]
        public async Task GenerateInsightsCommand_ValidData_CallsGrokSupercomputer()
        {
            // Arrange
            var budgetSummary = new BudgetVarianceAnalysis
            {
                TotalBudgeted = 100000.0m,
                TotalActual = 95000.0m,
                TotalVariance = -5000.0m,
                TotalVariancePercentage = -5.0m
            };

            var municipalAccounts = new List<MunicipalAccount>
            {
                new MunicipalAccount { Id = 1, Name = "Test Account", Balance = 1000.0m }
            };

            var budgetInsights = new BudgetInsights
            {
                HealthScore = 85,
                Recommendations = new List<string> { "Test recommendation" }
            };

            _budgetRepositoryMock.Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(budgetSummary);

            _municipalAccountRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(municipalAccounts);

            _grokSupercomputerMock.Setup(g => g.AnalyzeBudgetDataAsync(It.IsAny<BudgetData>()))
                .ReturnsAsync(budgetInsights);

            _grokSupercomputerMock.Setup(g => g.AnalyzeMunicipalAccountsWithAIAsync(It.IsAny<IEnumerable<MunicipalAccount>>(), It.IsAny<BudgetData>()))
                .ReturnsAsync("AI account analysis result");

            var viewModel = CreateViewModel();

            // Set required properties for command to be enabled
            viewModel.SelectedChartType = "Budget vs Actual";
            viewModel.SelectedTimePeriod = "Current Year";
            viewModel.IsDataLoaded = true;

            // Act
            await Task.Run(() => viewModel.GenerateInsightsCommand.Execute(null));

            // Assert
            _grokSupercomputerMock.Verify(g => g.AnalyzeBudgetDataAsync(It.IsAny<BudgetData>()), Times.Once);
            _grokSupercomputerMock.Verify(g => g.AnalyzeMunicipalAccountsWithAIAsync(It.IsAny<IEnumerable<MunicipalAccount>>(), It.IsAny<BudgetData>()), Times.Once);
        }

        [Fact]
        public void GenerateInsightsCommand_CanExecute_WhenDataLoadedAndSupercomputerAvailable()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsDataLoaded = true;

            // Act
            var canExecute = viewModel.GenerateInsightsCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void GenerateInsightsCommand_CanExecute_ReturnsFalse_WhenDataNotLoaded()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsDataLoaded = false;

            // Act
            var canExecute = viewModel.GenerateInsightsCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public async Task GenerateInsightsCommand_NoBudgetData_LogsWarning()
        {
            // Arrange
            _budgetRepositoryMock.Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync((BudgetVarianceAnalysis)null!);

            var viewModel = CreateViewModel();
            viewModel.SelectedChartType = "Budget vs Actual";
            viewModel.SelectedTimePeriod = "Current Year";
            viewModel.IsDataLoaded = true;

            // Act
            await Task.Run(() => viewModel.GenerateInsightsCommand.Execute(null));

            // Assert
            _loggerMock.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("No budget summary available")), It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task GenerateInsightsCommand_ExceptionDuringAnalysis_LogsError()
        {
            // Arrange
            _budgetRepositoryMock.Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            var viewModel = CreateViewModel();
            viewModel.SelectedChartType = "Budget vs Actual";
            viewModel.SelectedTimePeriod = "Current Year";
            viewModel.IsDataLoaded = true;

            // Act
            await Task.Run(() => viewModel.GenerateInsightsCommand.Execute(null));

            // Assert
            _loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.Is<string>(s => s.Contains("Error generating AI insights")), It.IsAny<object[]>()), Times.Once);
        }

        private AnalyticsViewModel CreateViewModel()
        {
            return new AnalyticsViewModel(
                _dispatcherHelperMock.Object,
                _loggerMock.Object,
                _budgetRepositoryMock.Object,
                _municipalAccountRepositoryMock.Object,
                _reportExportServiceMock.Object,
                _enterpriseRepositoryMock.Object,
                _eventAggregatorMock.Object,
                _grokSupercomputerMock.Object,
                _cacheServiceMock.Object);
        }
    }
}
