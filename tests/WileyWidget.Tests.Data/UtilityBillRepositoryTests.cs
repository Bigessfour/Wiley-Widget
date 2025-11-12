using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests.Data
{
    /// <summary>
    /// Unit tests for UtilityBillRepository
    /// Focus on testing repository methods that are currently untested
    /// </summary>
    public class UtilityBillRepositoryTests : IDisposable
    {
        private readonly DbConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly UtilityBillRepository _repository;
        private readonly IMemoryCache _cache;

        public UtilityBillRepositoryTests()
        {
            // Create and open SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            
            // Disable foreign key constraints for test flexibility
            var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
            
            // Create service collection with shared SQLite in-memory database
            var services = new ServiceCollection();
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite(_connection)
                       .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning)));
            services.AddMemoryCache();
            services.AddLogging();
            
            _serviceProvider = services.BuildServiceProvider();
            _contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            _cache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var logger = _serviceProvider.GetRequiredService<ILogger<UtilityBillRepository>>();
            
            _repository = new UtilityBillRepository(_contextFactory, logger, _cache);
            
            // Ensure database schema is created
            using var context = _contextFactory.CreateDbContext();
            context.Database.EnsureCreated();
            
            // SQLite workaround: Update RowVersion for all MunicipalAccounts with non-empty values
            // This prevents "NOT NULL constraint failed: MunicipalAccounts.RowVersion" errors
            context.MunicipalAccounts.ToList().ForEach(ma => 
            {
                if (ma.RowVersion == null || ma.RowVersion.Length == 0)
                {
                    ma.RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };
                }
            });
            context.SaveChanges();
            
            // Seed test UtilityCustomers that tests reference
            context.UtilityCustomers.AddRange(
                new UtilityCustomer { Id = 1, FirstName = "Test", LastName = "Customer1", MeterNumber = "METER-001", ServiceAddress = "123 Test St", AccountNumber = "ACC-001", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } },
                new UtilityCustomer { Id = 2, FirstName = "Test", LastName = "Customer2", MeterNumber = "METER-002", ServiceAddress = "456 Test Ave", AccountNumber = "ACC-002", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } }
            );
            context.SaveChanges();
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _connection.Dispose();
        }

        private async Task<AppDbContext> GetContextAsync()
        {
            return await _contextFactory.CreateDbContextAsync();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllUtilityBills()
        {
            // Arrange
            var context = await GetContextAsync();
            context.UtilityBills.AddRange(
                new UtilityBill { Id = 1, BillDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today, RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } },
                new UtilityBill { Id = 2, BillDate = new DateTime(2024, 7, 15), WaterCharges = 150.00m, BillNumber = "BILL-002", CustomerId = 1, DueDate = DateTime.Today, RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            
            // Cleanup
            await context.DisposeAsync();
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsUtilityBillById()
        {
            // Arrange
            var billId = 1;
            var expectedBill = new UtilityBill { Id = billId, BillDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today, RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(expectedBill);
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(billId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(billId, result.Id);
        }

        [Fact]
        public async Task GetByCustomerIdAsync_ReturnsBillsForCustomer()
        {
            // Arrange
            var customerId = 1;
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(
                new UtilityBill { Id = 1, CustomerId = customerId, BillDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, BillNumber = "BILL-001", DueDate = DateTime.Today }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCustomerIdAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(customerId, result.First().CustomerId);
        }

        [Fact]
        public async Task GetByBillNumberAsync_ReturnsBillByBillNumber()
        {
            // Arrange
            var billNumber = "BILL-001";
            var expectedBill = new UtilityBill { Id = 1, BillNumber = billNumber, BillDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, CustomerId = 1, DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(expectedBill);
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByBillNumberAsync(billNumber);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(billNumber, result.BillNumber);
        }

        [Fact]
        public async Task GetByStatusAsync_ReturnsBillsWithSpecificStatus()
        {
            // Arrange
            var status = BillStatus.Pending;
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(
                new UtilityBill { Id = 1, Status = status, BillDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByStatusAsync(status);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(status, result.First().Status);
        }

        [Fact]
        public async Task GetOverdueBillsAsync_ReturnsOverdueBills()
        {
            // Arrange
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(
                new UtilityBill { Id = 1, Status = BillStatus.Overdue, DueDate = DateTime.Today.AddDays(-5), WaterCharges = 100.00m, BillNumber = "BILL-001", CustomerId = 1 }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetOverdueBillsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetBillsDueInRangeAsync_ReturnsBillsDueInDateRange()
        {
            // Arrange
            var startDate = new DateTime(2024, 6, 1);
            var endDate = new DateTime(2024, 6, 30);
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(
                new UtilityBill { Id = 1, DueDate = new DateTime(2024, 6, 15), WaterCharges = 100.00m, BillNumber = "BILL-001", CustomerId = 1 }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetBillsDueInRangeAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetUnpaidBillsByCustomerIdAsync_ReturnsUnpaidBillsForCustomer()
        {
            // Arrange
            var customerId = 1;
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(
                new UtilityBill { Id = 1, CustomerId = customerId, Status = BillStatus.Pending, WaterCharges = 100.00m, BillNumber = "BILL-001", DueDate = DateTime.Today }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetUnpaidBillsByCustomerIdAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(customerId, result.First().CustomerId);
        }

        [Fact]
        public async Task GetCustomerBalanceAsync_ReturnsOutstandingBalance()
        {
            // Arrange
            var customerId = 1;
            await using var context = await GetContextAsync();
            context.UtilityBills.AddRange(
                new UtilityBill { Id = 1, CustomerId = customerId, Status = BillStatus.Pending, WaterCharges = 100.00m, AmountPaid = 20.00m, BillNumber = "BILL-001", DueDate = DateTime.Today },
                new UtilityBill { Id = 2, CustomerId = customerId, Status = BillStatus.Overdue, WaterCharges = 50.00m, AmountPaid = 0.00m, BillNumber = "BILL-002", DueDate = DateTime.Today }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetCustomerBalanceAsync(customerId);

            // Assert
            Assert.Equal(130.00m, result); // (100-20) + (50-0)
        }

        [Fact]
        public async Task GetChargesByBillIdAsync_ReturnsChargesForBill()
        {
            // Arrange
            var billId = 1;
            await using var context = await GetContextAsync();
            context.Charges.Add(
                new Charge { Id = 1, BillId = billId, ChargeType = "Water", Amount = 50.00m }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetChargesByBillIdAsync(billId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(billId, result.First().BillId);
        }

        [Fact]
        public async Task GetChargesByCustomerIdAsync_ReturnsChargesForCustomer()
        {
            // Arrange
            var customerId = 1;
            var bill = new UtilityBill { Id = 1, CustomerId = customerId, BillNumber = "BILL-001", DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(bill);
            await context.SaveChangesAsync();
            
            context.Charges.Add(
                new Charge { Id = 1, BillId = 1, ChargeType = "Water", Amount = 50.00m }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetChargesByCustomerIdAsync(customerId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task AddAsync_AddsNewBill()
        {
            // Arrange - use Id = 3 to avoid conflict with seeded customers (Ids 1 and 2)
            var customer = new UtilityCustomer { Id = 3, FirstName = "Test", LastName = "User", EmailAddress = "test@example.com", MeterNumber = "METER-003", ServiceAddress = "789 Test Blvd", AccountNumber = "ACC-003", RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } };
            var context = await GetContextAsync();
            context.UtilityCustomers.Add(customer);
            await context.SaveChangesAsync();

            var bill = new UtilityBill
            {
                CustomerId = 3,
                BillNumber = "BILL-001",
                BillDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                WaterCharges = 100.00m
            };

            // Act
            var result = await _repository.AddAsync(bill);

            // Assert
            Assert.Equal(bill, result);
            Assert.True(result.Id > 0);
            
            // Cleanup
            await context.DisposeAsync();
        }

        [Fact]
        public async Task AddChargeAsync_AddsNewCharge()
        {
            // Arrange
            var bill = new UtilityBill { Id = 1, CustomerId = 1, BillNumber = "BILL-001", DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(bill);
            await context.SaveChangesAsync();

            var charge = new Charge
            {
                BillId = 1,
                ChargeType = "Water",
                Amount = 50.00m
            };

            // Act
            var result = await _repository.AddChargeAsync(charge);

            // Assert
            Assert.Equal(charge, result);
            Assert.True(result.Id > 0);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesExistingBill()
        {
            // Arrange
            var existingBill = new UtilityBill { Id = 1, BillNumber = "BILL-001", WaterCharges = 100.00m, CustomerId = 1, DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(existingBill);
            await context.SaveChangesAsync();

            existingBill.WaterCharges = 150.00m;

            // Act
            var result = await _repository.UpdateAsync(existingBill);

            // Assert
            Assert.Equal(150.00m, result.WaterCharges);
        }

        [Fact]
        public async Task DeleteAsync_DeletesExistingBill()
        {
            // Arrange
            var billId = 1;
            var bill = new UtilityBill { Id = billId, BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(bill);
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.DeleteAsync(billId);

            // Assert - Use fresh context to verify deletion
            Assert.True(result);
            using var verifyContext = await _contextFactory.CreateDbContextAsync();
            Assert.Null(await verifyContext.UtilityBills.FindAsync(billId));
        }

        [Fact]
        public async Task RecordPaymentAsync_RecordsPaymentAndUpdatesStatus()
        {
            // Arrange
            var billId = 1;
            var paymentAmount = 50.00m;
            var paymentDate = DateTime.Today;
            var bill = new UtilityBill { Id = billId, WaterCharges = 100.00m, AmountPaid = 0.00m, Status = BillStatus.Pending, BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today };
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(bill);
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.RecordPaymentAsync(billId, paymentAmount, paymentDate);

            // Assert - Use fresh context to verify payment
            Assert.True(result);
            using var verifyContext = await _contextFactory.CreateDbContextAsync();
            var updatedBill = await verifyContext.UtilityBills.FindAsync(billId);
            Assert.Equal(paymentAmount, updatedBill!.AmountPaid);
        }

        [Fact]
        public async Task BillNumberExistsAsync_ReturnsTrueWhenBillNumberExists()
        {
            // Arrange
            var billNumber = "BILL-001";
            await using var context = await GetContextAsync();
            context.UtilityBills.Add(new UtilityBill { Id = 1, BillNumber = billNumber, CustomerId = 1, DueDate = DateTime.Today });
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.BillNumberExistsAsync(billNumber);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetCountAsync_ReturnsTotalBillCount()
        {
            // Arrange
            await using var context = await GetContextAsync();
            context.UtilityBills.AddRange(
                new UtilityBill { BillNumber = "BILL-001", CustomerId = 1, DueDate = DateTime.Today },
                new UtilityBill { BillNumber = "BILL-002", CustomerId = 1, DueDate = DateTime.Today },
                new UtilityBill { BillNumber = "BILL-003", CustomerId = 1, DueDate = DateTime.Today }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await _repository.GetCountAsync();

            // Assert
            Assert.Equal(3, result);
        }
    }
}
