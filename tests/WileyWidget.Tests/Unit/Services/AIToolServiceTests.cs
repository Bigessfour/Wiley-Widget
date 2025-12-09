using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services;

namespace WileyWidget.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for AIToolService - Grok function calling implementation
    /// Tests budget data retrieval, trend analysis, and financial recommendations
    /// </summary>
    public class AIToolServiceTests
    {
        private readonly Mock<IBudgetRepository> _mockBudgetRepo;
        private readonly Mock<IMunicipalAccountRepository> _mockAccountRepo;
        private readonly Mock<IUtilityBillRepository> _mockBillRepo;
        private readonly Mock<IUtilityCustomerRepository> _mockCustomerRepo;
        private readonly Mock<ILogger<AIToolService>> _mockLogger;
        private readonly AIToolService _service;

        public AIToolServiceTests()
        {
            _mockBudgetRepo = new Mock<IBudgetRepository>();
            _mockAccountRepo = new Mock<IMunicipalAccountRepository>();
            _mockBillRepo = new Mock<IUtilityBillRepository>();
            _mockCustomerRepo = new Mock<IUtilityCustomerRepository>();
            _mockLogger = new Mock<ILogger<AIToolService>>();

            _service = new AIToolService(
                _mockBudgetRepo.Object,
                _mockAccountRepo.Object,
                _mockBillRepo.Object,
                _mockCustomerRepo.Object,
                _mockLogger.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenBudgetRepoIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AIToolService(
                null!,
                _mockAccountRepo.Object,
                _mockBillRepo.Object,
                _mockCustomerRepo.Object,
                _mockLogger.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenAccountRepoIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AIToolService(
                _mockBudgetRepo.Object,
                null!,
                _mockBillRepo.Object,
                _mockCustomerRepo.Object,
                _mockLogger.Object));
        }

        #endregion

        #region GetBudgetDataAsync Tests

        [Fact]
        public async Task GetBudgetDataAsync_WithValidFiscalYear_ReturnsBudgetData()
        {
            // Arrange
            int fiscalYear = 2025;
            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry
                {
                    Id = 1,
                    FiscalYear = fiscalYear,
                    BudgetedAmount = 10000m,
                    ActualAmount = 9500m
                },
                new BudgetEntry
                {
                    Id = 2,
                    FiscalYear = fiscalYear,
                    BudgetedAmount = 15000m,
                    ActualAmount = 14800m
                }
            };

            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ReturnsAsync(budgetEntries);

            // Act
            var result = await _service.GetBudgetDataAsync(fiscalYear);

            // Assert
            Assert.NotNull(result);
            _mockBudgetRepo.Verify(r => r.GetByFiscalYearAsync(fiscalYear), Times.Once);
        }

        [Fact]
        public async Task GetBudgetDataAsync_WithFundTypeFilter_FiltersResults()
        {
            // Arrange
            int fiscalYear = 2025;
            string fundType = "General";
            var generalFund = new Fund { Id = 1, Name = "General Fund" };
            var sewageFund = new Fund { Id = 2, Name = "Sewage Fund" };

            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry
                {
                    Id = 1,
                    FiscalYear = fiscalYear,
                    Fund = generalFund,
                    BudgetedAmount = 10000m
                },
                new BudgetEntry
                {
                    Id = 2,
                    FiscalYear = fiscalYear,
                    Fund = sewageFund,
                    BudgetedAmount = 15000m
                }
            };

            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ReturnsAsync(budgetEntries);

            // Act
            var result = await _service.GetBudgetDataAsync(fiscalYear, fundType);

            // Assert
            Assert.NotNull(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(fundType)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetBudgetDataAsync_WithRepositoryException_ReturnsErrorObject()
        {
            // Arrange
            int fiscalYear = 2025;
            _mockBudgetRepo.Setup(r => r.GetByFiscalYearAsync(fiscalYear))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.GetBudgetDataAsync(fiscalYear);

            // Assert
            Assert.NotNull(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region AnalyzeBudgetTrendsAsync Tests

        [Fact]
        public async Task AnalyzeBudgetTrendsAsync_WithValidAccountId_ReturnsTrends()
        {
            // Arrange
            int accountId = 1;
            var account = new MunicipalAccount
            {
                Id = accountId,
                AccountNumber = new AccountNumber("ACC-001"),
                Name = "Test Account"
            };

            _mockAccountRepo.Setup(r => r.GetByIdAsync(accountId))
                .ReturnsAsync(account);

            // Act
            var result = await _service.AnalyzeBudgetTrendsAsync(accountId, "monthly");

            // Assert
            Assert.NotNull(result);
            _mockAccountRepo.Verify(r => r.GetByIdAsync(accountId), Times.Once);
        }

        [Fact]
        public async Task AnalyzeBudgetTrendsAsync_WithInvalidAccountId_ReturnsEmptyList()
        {
            // Arrange
            int accountId = 999;
            _mockAccountRepo.Setup(r => r.GetByIdAsync(accountId))
                .ReturnsAsync((MunicipalAccount)null!);

            // Act
            var result = await _service.AnalyzeBudgetTrendsAsync(accountId, "monthly");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("monthly")]
        [InlineData("quarterly")]
        [InlineData("yearly")]
        public async Task AnalyzeBudgetTrendsAsync_WithDifferentPeriods_HandlesCorrectly(string period)
        {
            // Arrange
            int accountId = 1;
            var account = new MunicipalAccount { Id = accountId, Name = "Test" };
            _mockAccountRepo.Setup(r => r.GetByIdAsync(accountId))
                .ReturnsAsync(account);

            // Act
            var result = await _service.AnalyzeBudgetTrendsAsync(accountId, period);

            // Assert
            Assert.NotNull(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(period)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task GetBudgetDataAsync_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            int fiscalYear = 2025;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await _service.GetBudgetDataAsync(fiscalYear, null, cts.Token));
        }

        #endregion
    }
}
