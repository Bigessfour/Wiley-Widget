using WileyWidget.Services;
using WileyWidget.Models;
using Xunit;
using FluentAssertions;
using System;
using Microsoft.EntityFrameworkCore;

/*
namespace WileyWidget.Integration.Tests.Services
{
    public class BudgetServiceTests : IntegrationTestBase
    {
        protected override async Task SeedTestDataAsync()
        {
            var budgetEntry = new BudgetEntry
            {
                FundType = FundType.EnterpriseFund,
                FiscalYear = 2025,
                BudgetedAmount = 1000,
                ActualAmount = 0,
                StartPeriod = new DateTime(2026, 1, 1),
                EndPeriod = new DateTime(2026, 12, 31)
            };
            DbContext.BudgetEntries.Add(budgetEntry);

            var transaction = new Transaction
            {
                BudgetEntryId = 1,
                Amount = 200,
                TransactionDate = DateTime.Now,
                Description = "Test transaction"
            };
            DbContext.Transactions.Add(transaction);
            await DbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateBudget_NewPeriod_CreatesEntityAndCategories()
        {
            // Arrange
            var budgetService = GetRequiredService<IBudgetService>();
            var budgetData = new BudgetData
            {
                FiscalYear = 2026,
                FundType = FundType.EnterpriseFund,
                BudgetedAmount = 1500
            };

            // Act
            var result = await budgetService.CreateBudgetAsync(budgetData);

            // Assert
            result.Should().NotBeNull();
            var created = await DbContext.BudgetEntries.FindAsync(result.Id);
            created.Should().NotBeNull();
            created.BudgetedAmount.Should().Be(1500);
        }

        [Fact]
        public async Task CalculateRemainingBudget_AfterTransactions_ReturnsCorrectValue()
        {
            // Arrange
            await SeedTestDataAsync();
            var budgetService = GetRequiredService<IBudgetService>();

            // Act
            var remaining = await budgetService.CalculateRemainingBudgetAsync(1);

            // Assert
            remaining.Should().Be(800); // 1000 - 200
        }

        [Fact]
        public async Task CalculateRemainingBudget_Overdrawn_ReturnsNegative()
        {
            // Arrange
            await SeedTestDataAsync();
            var budgetService = GetRequiredService<IBudgetService>();
            // Add more transactions to exceed budget
            var extraTxn = new Transaction
            {
                BudgetEntryId = 1,
                Amount = 900,
                TransactionDate = DateTime.Now,
                Description = "Extra transaction"
            };
            DbContext.Transactions.Add(extraTxn);
            await DbContext.SaveChangesAsync();

            // Act
            var remaining = await budgetService.CalculateRemainingBudgetAsync(1);

            // Assert
            remaining.Should().Be(-100); // 1000 - 200 - 900
        }

        [Fact]
        public async Task RollOverBudget_PreviousToNew_CopiesUnspentAmounts()
        {
            // Arrange
            await SeedTestDataAsync();
            var budgetService = GetRequiredService<IBudgetService>();

            // Act
            var success = await budgetService.RollOverBudgetAsync(2025, 2026);

            // Assert
            success.Should().BeTrue();
            var newBudget = await DbContext.BudgetEntries.FirstOrDefaultAsync(b => b.FiscalYear == 2026);
            newBudget.Should().NotBeNull();
            newBudget.BudgetedAmount.Should().Be(800); // Rolled over remaining
        }

        [Fact]
        public async Task GetBudgetProgress_Percentage_CalculatesAccurately()
        {
            // Arrange
            await SeedTestDataAsync();
            var budgetService = GetRequiredService<IBudgetService>();

            // Act
            var progress = await budgetService.GetBudgetProgressAsync(1);

            // Assert
            progress.Should().Be(20); // 200 / 1000 * 100
        }

        [Fact]
        public async Task UpdateBudgetLimit_RecalculatesRemainingCorrectly()
        {
            // Arrange
            await SeedTestDataAsync();
            var budgetService = GetRequiredService<IBudgetService>();

            // Act
            var success = await budgetService.UpdateBudgetLimitAsync(1, 1200);

            // Assert
            success.Should().BeTrue();
            var updated = await DbContext.BudgetEntries.FindAsync(1);
            updated.BudgetedAmount.Should().Be(1200);
            // Remaining should be recalculated: 1200 - 200 = 1000
        }
    }
}
*/
