#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Comprehensive unit tests for <see cref="AccountsViewModel"/>.
    /// Tests cover LoadAccountsAsync, SaveAccountAsync, DeleteAccountAsync, filtering, validation,
    /// exception handling, and logging enhancements.
    /// Uses in-memory EF Core provider and Moq for ILogger verification.
    /// </summary>
    public sealed class AccountsViewModelTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<AccountsViewModel>> _mockLogger;

        public AccountsViewModelTests()
        {
            // Create unique in-memory database for each test to ensure isolation
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _mockLogger = new Mock<ILogger<AccountsViewModel>>();
            _mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Verifiable();
        }

        public void Dispose()
        {
            // Clean up in-memory database
            using var context = new AppDbContext(_dbOptions);
            context.Database.EnsureDeleted();
            GC.SuppressFinalize(this);
        }

        #region Helper Methods

        private AppDbContext CreateContext() => new AppDbContext(_dbOptions);

        private async Task SeedTestDataAsync(int accountCount = 5, bool includeInactive = false)
        {
            await using var context = CreateContext();

            // Seed required reference data
            var department = new Department { Id = 1, Name = "Test Department" };
            var budgetPeriod = new BudgetPeriod
            {
                Id = 1,
                Name = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true
            };

            context.Departments.Add(department);
            context.BudgetPeriods.Add(budgetPeriod);

            for (int i = 1; i <= accountCount; i++)
            {
                var account = new MunicipalAccount
                {
                    Id = i,
                    AccountNumber = new AccountNumber($"{1000 + i}"),
                    Name = $"Test Account {i}",
                    DepartmentId = 1,
                    BudgetPeriodId = 1,
                    Balance = i * 1000m,
                    BudgetAmount = i * 1500m,
                    IsActive = includeInactive ? (i % 2 == 0) : true,
                    Fund = i % 2 == 0 ? MunicipalFundType.General : MunicipalFundType.Enterprise,
                    Type = i % 3 == 0 ? AccountType.Revenue : AccountType.Asset
                };
                context.MunicipalAccounts.Add(account);
            }

            await context.SaveChangesAsync();
        }

        private AccountsViewModel CreateViewModel(AppDbContext context)
        {
            return new AccountsViewModel(_mockLogger.Object, context);
        }

        #endregion

        #region LoadAccountsAsync Tests

        [Fact]
        public async Task LoadAccountsAsync_WithNoData_ReturnsEmptyCollection()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.Accounts.Should().BeEmpty();
            vm.TotalBalance.Should().Be(0);
            vm.ActiveAccountCount.Should().Be(0);
        }

        [Fact]
        public async Task LoadAccountsAsync_WithData_LoadsAllActiveAccounts()
        {
            // Arrange
            await SeedTestDataAsync(5);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.Accounts.Should().HaveCount(5);
            vm.TotalBalance.Should().Be(15000m); // 1000 + 2000 + 3000 + 4000 + 5000
            vm.ActiveAccountCount.Should().Be(5);
        }

        [Fact]
        public async Task LoadAccountsAsync_ExcludesInactiveAccounts()
        {
            // Arrange
            await SeedTestDataAsync(5, includeInactive: true);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert - only even IDs are active (2, 4)
            vm.Accounts.Should().HaveCount(2);
        }

        [Fact]
        public async Task LoadAccountsAsync_LogsInformationWithFilterContext()
        {
            // Arrange
            await SeedTestDataAsync(3);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert - Verify structured logging with filters was called
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Loading municipal accounts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task LoadAccountsAsync_SetsIsLoadingDuringOperation()
        {
            // Arrange
            await SeedTestDataAsync(3);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            bool wasLoading = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
                    wasLoading = true;
            };

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            wasLoading.Should().BeTrue();
            vm.IsLoading.Should().BeFalse(); // Should be false after completion
        }

        #endregion

        #region Filter Tests

        [Fact]
        public async Task LoadAccountsAsync_WithFundFilter_FiltersCorrectly()
        {
            // Arrange
            await SeedTestDataAsync(6);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            vm.SelectedFund = MunicipalFundType.General;
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert - Even numbered accounts have General fund
            vm.Accounts.Should().OnlyContain(a => a.Fund == MunicipalFundType.General.ToString());
        }

        [Fact]
        public async Task LoadAccountsAsync_WithAccountTypeFilter_FiltersCorrectly()
        {
            // Arrange
            await SeedTestDataAsync(6);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            vm.SelectedAccountType = AccountType.Revenue;
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert - Accounts divisible by 3 have Revenue type
            vm.Accounts.Should().OnlyContain(a => a.Type == AccountType.Revenue.ToString());
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void ValidateAccount_WithNullAccount_ReturnsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            var errors = vm.ValidateAccount(null!).ToList();

            // Assert
            errors.Should().Contain("Account cannot be null.");
        }

        [Fact]
        public void ValidateAccount_WithValidAccount_ReturnsNoErrors()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var account = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1001"),
                Name = "Valid Account",
                DepartmentId = 1,
                BudgetPeriodId = 1,
                Balance = 1000m,
                BudgetAmount = 1500m
            };

            // Act
            var errors = vm.ValidateAccount(account).ToList();

            // Assert
            errors.Should().BeEmpty();
        }

        [Fact]
        public void ValidateAccount_WithMissingAccountNumber_ReturnsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var account = new MunicipalAccount
            {
                Name = "Test Account",
                DepartmentId = 1,
                BudgetPeriodId = 1
            };

            // Act
            var errors = vm.ValidateAccount(account).ToList();

            // Assert
            errors.Should().Contain(e => e.Contains("Account number"));
        }

        [Fact]
        public void ValidateAccount_WithMissingName_ReturnsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var account = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1001"),
                Name = "",
                DepartmentId = 1,
                BudgetPeriodId = 1
            };

            // Act
            var errors = vm.ValidateAccount(account).ToList();

            // Assert
            errors.Should().Contain(e => e.Contains("name"));
        }

        [Fact]
        public void ValidateAccount_WithInvalidDepartment_ReturnsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var account = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1001"),
                Name = "Test Account",
                DepartmentId = 0,
                BudgetPeriodId = 1
            };

            // Act
            var errors = vm.ValidateAccount(account).ToList();

            // Assert
            errors.Should().Contain(e => e.Contains("Department"));
        }

        [Fact]
        public void ValidateAccount_WithNegativeBudgetAmount_ReturnsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var account = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1001"),
                Name = "Test Account",
                DepartmentId = 1,
                BudgetPeriodId = 1,
                BudgetAmount = -100m
            };

            // Act
            var errors = vm.ValidateAccount(account).ToList();

            // Assert
            errors.Should().Contain(e => e.Contains("Budget amount") || e.Contains("negative"));
        }

        #endregion

        #region SaveAccountAsync Tests

        [Fact]
        public async Task SaveAccountAsync_WithInvalidAccount_ReturnsFalseAndSetsError()
        {
            // Arrange
            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var invalidAccount = new MunicipalAccount { Name = "" };

            // Act
            var result = await vm.SaveAccountAsync(invalidAccount);

            // Assert
            result.Should().BeFalse();
            vm.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task SaveAccountAsync_WithNewValidAccount_CreatesAndReturnsTrue()
        {
            // Arrange - Seed required reference data
            await using (var ctx = CreateContext())
            {
                ctx.Departments.Add(new Department { Id = 1, Name = "Test Dept" });
                ctx.BudgetPeriods.Add(new BudgetPeriod
                {
                    Id = 1,
                    Name = "FY2024",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddYears(1),
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var newAccount = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("9999"),
                Name = "New Test Account",
                DepartmentId = 1,
                BudgetPeriodId = 1,
                Balance = 5000m,
                BudgetAmount = 7500m,
                IsActive = true
            };

            // Act
            var result = await vm.SaveAccountAsync(newAccount);

            // Assert
            result.Should().BeTrue();

            // Verify account was persisted
            await using var verifyCtx = CreateContext();
            var savedAccount = await verifyCtx.MunicipalAccounts.FirstOrDefaultAsync(a => a.AccountNumber!.Value == "9999");
            savedAccount.Should().NotBeNull();
            savedAccount!.Name.Should().Be("New Test Account");
        }

        [Fact]
        public async Task SaveAccountAsync_LogsAccountDetails()
        {
            // Arrange
            await using (var ctx = CreateContext())
            {
                ctx.Departments.Add(new Department { Id = 1, Name = "Test Dept" });
                ctx.BudgetPeriods.Add(new BudgetPeriod
                {
                    Id = 1,
                    Name = "FY2024",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddYears(1),
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            using var context = CreateContext();
            var vm = CreateViewModel(context);
            var newAccount = new MunicipalAccount
            {
                AccountNumber = new AccountNumber("8888"),
                Name = "Logged Account",
                DepartmentId = 1,
                BudgetPeriodId = 1,
                Fund = MunicipalFundType.General,
                Type = AccountType.Asset
            };

            // Act
            await vm.SaveAccountAsync(newAccount);

            // Assert - Verify structured logging was called
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Created account")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region DeleteAccountAsync Tests

        [Fact]
        public async Task DeleteAccountAsync_WithValidId_SoftDeletesAndReturnsTrue()
        {
            // Arrange
            await SeedTestDataAsync(3);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            var result = await vm.DeleteAccountAsync(1);

            // Assert
            result.Should().BeTrue();

            // Verify soft delete
            await using var verifyCtx = CreateContext();
            var deletedAccount = await verifyCtx.MunicipalAccounts.FindAsync(1);
            deletedAccount!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithInvalidId_ReturnsFalse()
        {
            // Arrange
            await SeedTestDataAsync(1);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            var result = await vm.DeleteAccountAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAccountAsync_LogsAccountDetails()
        {
            // Arrange
            await SeedTestDataAsync(1);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.DeleteAccountAsync(1);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Deleted") || o.ToString()!.Contains("deactivated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Null Handling Tests

        [Fact]
        public async Task LoadAccountsAsync_WithNullAccountNumber_DisplaysNAPlaceholder()
        {
            // Arrange
            await using (var ctx = CreateContext())
            {
                ctx.Departments.Add(new Department { Id = 1, Name = "Test Dept" });
                ctx.BudgetPeriods.Add(new BudgetPeriod { Id = 1, Name = "FY2024", StartDate = DateTime.Now, EndDate = DateTime.Now.AddYears(1), IsActive = true });
                ctx.MunicipalAccounts.Add(new MunicipalAccount
                {
                    Id = 1,
                    AccountNumber = null, // Null account number
                    Name = "Test Account",
                    DepartmentId = 1,
                    BudgetPeriodId = 1,
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.Accounts.Should().HaveCount(1);
            vm.Accounts[0].AccountNumber.Should().Be("N/A");
        }

        [Fact(Skip = "Requires ViewModel to support DepartmentId = 0 which may be filtered out by validation")]
        public async Task LoadAccountsAsync_WithNullDepartment_DisplaysUnassignedPlaceholder()
        {
            // Arrange
            await using (var ctx = CreateContext())
            {
                ctx.BudgetPeriods.Add(new BudgetPeriod { Id = 1, Name = "FY2024", StartDate = DateTime.Now, EndDate = DateTime.Now.AddYears(1), IsActive = true });
                ctx.MunicipalAccounts.Add(new MunicipalAccount
                {
                    Id = 1,
                    AccountNumber = new AccountNumber("1001"),
                    Name = "Test Account",
                    DepartmentId = 0, // No department
                    BudgetPeriodId = 1,
                    IsActive = true
                });
                await ctx.SaveChangesAsync();
            }

            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.Accounts.Should().HaveCount(1);
            vm.Accounts[0].Department.Should().Be("(Unassigned)");
        }

        #endregion

        #region Summary Calculator Tests

        [Fact]
        public async Task TotalBalance_CalculatesCorrectSum()
        {
            // Arrange
            await SeedTestDataAsync(5);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.TotalBalance.Should().Be(vm.Accounts.Sum(a => a.Balance));
        }

        [Fact]
        public async Task ActiveAccountCount_CountsCorrectly()
        {
            // Arrange
            await SeedTestDataAsync(5);
            using var context = CreateContext();
            var vm = CreateViewModel(context);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(null);

            // Assert
            vm.ActiveAccountCount.Should().Be(vm.Accounts.Count);
        }

        #endregion
    }
}
