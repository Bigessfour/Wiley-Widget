using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Xunit;
using FluentAssertions;
using System;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Integration.Tests.Repositories
{
    public class TransactionRepositoryTests : SqliteIntegrationTestBase
    {
        protected int SeededBudgetEntryId { get; private set; }

        protected override async Task SeedTestDataAsync()
        {
            // Ensure required FK parent rows exist (AppDbContext has HasData for core lookups)
            // Create a minimal BudgetEntry that references seeded Department/MunicipalAccount rows (Id=1)
            var budgetEntry = new BudgetEntry
            {
                FundType = FundType.EnterpriseFund,
                FiscalYear = 2025,
                BudgetedAmount = 1000,
                ActualAmount = 0,
                StartPeriod = new DateTime(2025, 1, 1),
                EndPeriod = new DateTime(2025, 12, 31),
                DepartmentId = 1, // AppDbContext seeds Department Id=1
                MunicipalAccountId = 1 // AppDbContext seeds MunicipalAccount Id=1
            };

            DbContext.BudgetEntries.Add(budgetEntry);
            await DbContext.SaveChangesAsync();

            // Store the created id so tests don't rely on magic value 1
            SeededBudgetEntryId = budgetEntry.Id;

            // Debug: Verify it was saved
            var count = await DbContext.BudgetEntries.CountAsync();
            Console.WriteLine($"BudgetEntries count after seed: {count}");
        }

        [Fact]
        public async Task AddTransaction_Then_Save_And_QueryById_ReturnsSameEntity()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = SeededBudgetEntryId,
                Amount = 100,
                TransactionDate = DateTime.Now,
                Description = "Test transaction"
            };

            // Act
            await repo.AddAsync(transaction);
            await DbContext.SaveChangesAsync();
            var retrieved = await repo.GetByIdAsync(transaction.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Amount.Should().Be(100);
            retrieved.Description.Should().Be("Test transaction");
        }

        [Fact]
        public async Task AddTransaction_WithBudgetEntryId_SavesCorrectly()
        {
            // Arrange - Create BudgetEntry directly (ensure required FKs)
            var budgetEntry = new BudgetEntry
            {
                FundType = FundType.EnterpriseFund,
                FiscalYear = 2025,
                BudgetedAmount = 1000,
                ActualAmount = 0,
                StartPeriod = new DateTime(2025, 1, 1),
                EndPeriod = new DateTime(2025, 12, 31),
                DepartmentId = 1,
                MunicipalAccountId = 1
            };
            DbContext.BudgetEntries.Add(budgetEntry);
            await DbContext.SaveChangesAsync();

            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = budgetEntry.Id,  // Use the actual ID
                Amount = 50,
                TransactionDate = DateTime.Now,
                Description = "Category transaction"
            };

            // Act
            await repo.AddAsync(transaction);

            // Assert - Check that transaction was saved with correct data
            var allFromRepo = await repo.GetAllAsync();
            var transactionFromRepo = allFromRepo.First(t => t.Id == transaction.Id);
            transactionFromRepo.BudgetEntryId.Should().Be(budgetEntry.Id);
            transactionFromRepo.Amount.Should().Be(50);
            transactionFromRepo.Description.Should().Be("Category transaction");
        }

        [Fact]
        public async Task UpdateTransaction_Amount_UpdatesExistingEntity()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = SeededBudgetEntryId,
                Amount = 100,
                TransactionDate = DateTime.Now,
                Description = "Original"
            };
            await repo.AddAsync(transaction);
            await DbContext.SaveChangesAsync();

            // Act
            transaction.Amount = 200;
            transaction.Description = "Updated";
            await repo.UpdateAsync(transaction);
            await DbContext.SaveChangesAsync();
            var retrieved = await repo.GetByIdAsync(transaction.Id);

            // Assert
            retrieved.Amount.Should().Be(200);
            retrieved.Description.Should().Be("Updated");
        }

        [Fact]
        public async Task DeleteTransaction_RemovesFromDbSet()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = SeededBudgetEntryId,
                Amount = 75,
                TransactionDate = DateTime.Now,
                Description = "To delete"
            };
            await repo.AddAsync(transaction);
            await DbContext.SaveChangesAsync();

            // Act
            await repo.DeleteAsync(transaction.Id);
            await DbContext.SaveChangesAsync();
            var retrieved = await repo.GetByIdAsync(transaction.Id);

            // Assert
            retrieved.Should().BeNull();
        }

        [Fact]
        public async Task QueryTransactions_ByDateRange_ReturnsFilteredResults()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var date1 = new DateTime(2025, 1, 1);
            var date2 = new DateTime(2025, 1, 31);
            var transaction1 = new Transaction { BudgetEntryId = SeededBudgetEntryId, Amount = 100, TransactionDate = date1, Description = "Jan" };
            var transaction2 = new Transaction { BudgetEntryId = SeededBudgetEntryId, Amount = 200, TransactionDate = date2, Description = "Jan" };
            var transaction3 = new Transaction { BudgetEntryId = SeededBudgetEntryId, Amount = 300, TransactionDate = new DateTime(2025, 2, 1), Description = "Feb" };
            await repo.AddAsync(transaction1);
            await repo.AddAsync(transaction2);
            await repo.AddAsync(transaction3);
            await DbContext.SaveChangesAsync();

            // Act
            var results = await repo.GetByDateRangeAsync(date1, date2);

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(t => t.Description == "Jan");
        }

        [Fact]
        public async Task QueryTransactions_WithIncludes_LoadsNavigationProperties()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = SeededBudgetEntryId,
                Amount = 150,
                TransactionDate = DateTime.Now,
                Description = "With includes"
            };
            await repo.AddAsync(transaction);
            await DbContext.SaveChangesAsync();

            // Act
            var retrieved = await repo.GetByIdWithIncludesAsync(transaction.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.BudgetEntry.Should().NotBeNull();
        }

        [Fact]
        public async Task ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
        {
            // Arrange - ensure seed and create a transaction
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transaction = new Transaction
            {
                BudgetEntryId = SeededBudgetEntryId,
                Amount = 100,
                TransactionDate = DateTime.Now,
                Description = "Concurrent"
            };
            await repo.AddAsync(transaction);

            // Load two detached copies via repository (each update will use its own DbContext internally)
            var t1 = await repo.GetByIdAsync(transaction.Id);
            var t2 = await repo.GetByIdAsync(transaction.Id);

            // Detach them to simulate detached entities
            DbContext.Entry(t1!).State = EntityState.Detached;
            DbContext.Entry(t2!).State = EntityState.Detached;

            // Simulate old RowVersion in t2
            t2!.RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

            // First update succeeds
            t1!.Amount = 200;
            await repo.UpdateAsync(t1);

            // Simulate a concurrent change by updating the underlying row's RowVersion in a separate scope/context
            using (var scope = CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dbEntity = await ctx.Transactions.FindAsync(transaction.Id);
                if (dbEntity != null)
                {
                    dbEntity.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                    await ctx.SaveChangesAsync();
                }
            }

            // Second update should now fail with a concurrency exception
            t2!.Amount = 300;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repo.UpdateAsync(t2));
        }

        [Fact]
        public async Task BulkInsert_MultipleTransactions_SavesAllEfficiently()
        {
            // Arrange
            await SeedTestDataAsync();
            var repo = GetRequiredService<ITransactionRepository>();
            var transactions = new List<Transaction>
            {
                new() { BudgetEntryId = SeededBudgetEntryId, Amount = 10, TransactionDate = DateTime.Now, Description = "Bulk1" },
                new() { BudgetEntryId = SeededBudgetEntryId, Amount = 20, TransactionDate = DateTime.Now, Description = "Bulk2" },
                new() { BudgetEntryId = SeededBudgetEntryId, Amount = 30, TransactionDate = DateTime.Now, Description = "Bulk3" }
            };

            // Act
            await repo.BulkInsertAsync(transactions);
            await DbContext.SaveChangesAsync();
            var all = await repo.GetAllAsync();

            // Assert
            all.Should().HaveCount(3);
            all.Sum(t => t.Amount).Should().Be(60);
        }
    }
}
