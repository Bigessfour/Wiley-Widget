#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.Services.Tests
{
    /// <summary>
    /// Unit tests for AccountService.
    /// Verifies business logic extraction from ViewModel is correct.
    /// </summary>
    public sealed class AccountServiceTests : IDisposable
    {
        private readonly IServiceProvider _provider;
        private readonly Mock<ILogger<AccountService>> _mockLogger;
        private readonly Mock<IAccountMapper> _mockMapper;

        public AccountServiceTests()
        {
            AppDbContext.SkipModelSeedingInMemoryTests = true;

            var dbName = $"TestDb_{Guid.NewGuid()}";
            var services = new ServiceCollection();

            services.AddDbContext<AppDbContext>((sp, o) =>
            {
                o.UseInMemoryDatabase(dbName).EnableSensitiveDataLogging();
            });

            services.AddLogging();

            _provider = services.BuildServiceProvider();
            _mockLogger = new Mock<ILogger<AccountService>>();
            _mockMapper = new Mock<IAccountMapper>();

            // Initialize clean database
            using var initScope = _provider.CreateScope();
            var initCtx = initScope.ServiceProvider.GetRequiredService<AppDbContext>();
            initCtx.Database.EnsureDeleted();
            initCtx.Database.EnsureCreated();
        }

        [Fact]
        public async Task LoadAccountsAsync_WithNoFilters_ReturnsAllActiveAccounts()
        {
            // Arrange
            await SeedTestDataAsync();

            var displayAccounts = new List<MunicipalAccountDisplay>
            {
                new() { Id = 1, AccountNumber = "1000", Name = "General Fund", Balance = 10000m },
                new() { Id = 2, AccountNumber = "2000", Name = "Special Revenue", Balance = 20000m }
            };

            _mockMapper.Setup(m => m.MapToDisplay(It.IsAny<IEnumerable<MunicipalAccount>>()))
                .Returns(displayAccounts);

            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.LoadAccountsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Accounts.Should().HaveCount(2);
            result.TotalBalance.Should().Be(30000m);
            result.ActiveAccountCount.Should().Be(2);
        }

        [Fact]
        public async Task LoadAccountsAsync_WithFundFilter_ReturnsFilteredAccounts()
        {
            // Arrange
            await SeedTestDataAsync();

            var displayAccounts = new List<MunicipalAccountDisplay>
            {
                new() { Id = 1, AccountNumber = "1000", Name = "General Fund", Balance = 10000m }
            };

            _mockMapper.Setup(m => m.MapToDisplay(It.IsAny<IEnumerable<MunicipalAccount>>()))
                .Returns(displayAccounts);

            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.LoadAccountsAsync(MunicipalFundType.General);

            // Assert
            result.Accounts.Should().HaveCount(1);
            result.Accounts.First().Name.Should().Be("General Fund");
        }

        [Fact]
        public async Task SaveAccountAsync_WithValidAccount_ReturnsSuccess()
        {
            // Arrange
            await SeedTestDataAsync(); // Seed departments and budget periods first
            var account = CreateValidAccount();
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.SaveAccountAsync(account);

            // Assert
            result.Success.Should().BeTrue();
            result.ValidationErrors.Should().BeEmpty();

            // Verify account was saved
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var savedAccount = await context.MunicipalAccounts.FirstOrDefaultAsync(a => a.Name == "Test Account");
            savedAccount.Should().NotBeNull();
        }

        [Fact]
        public async Task SaveAccountAsync_WithInvalidAccount_ReturnsValidationErrors()
        {
            // Arrange
            var account = new MunicipalAccount { Name = "" }; // Invalid: empty name
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.SaveAccountAsync(account);

            // Assert
            result.Success.Should().BeFalse();
            result.ValidationErrors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithExistingAccount_SoftDeletesAccount()
        {
            // Arrange
            var accountId = await SeedSingleAccountAsync();
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.DeleteAccountAsync(accountId);

            // Assert
            result.Should().BeTrue();

            // Verify soft delete
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deletedAccount = await context.MunicipalAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
            deletedAccount.Should().NotBeNull();
            deletedAccount!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAccountAsync_WithNonExistentAccount_ReturnsFalse()
        {
            // Arrange
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var result = await service.DeleteAccountAsync(99999);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task LoadAccountsAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await service.LoadAccountsAsync(cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public void ValidateAccount_WithNullAccount_ReturnsError()
        {
            // Arrange
            var service = new AccountService(_mockLogger.Object, _provider.GetRequiredService<IServiceScopeFactory>(), _mockMapper.Object);

            // Act
            var errors = service.ValidateAccount(null!).ToList();

            // Assert
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("null"));
        }

        private async Task SeedTestDataAsync()
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var department = new Department { Id = 1, Name = "Finance", DepartmentCode = "FIN" };
            var budgetPeriod = new BudgetPeriod { Id = 1, Name = "FY2025", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddYears(1) };

            context.Departments.Add(department);
            context.BudgetPeriods.Add(budgetPeriod);
            await context.SaveChangesAsync();

            var accounts = new List<MunicipalAccount>
            {
                new()
                {
                    Name = "General Fund",
                    AccountNumber = new AccountNumber("1000"),
                    Fund = MunicipalFundType.General,
                    Type = AccountType.Asset,
                    Balance = 10000m,
                    BudgetAmount = 15000m,
                    IsActive = true,
                    DepartmentId = 1,
                    BudgetPeriodId = 1
                },
                new()
                {
                    Name = "Special Revenue",
                    AccountNumber = new AccountNumber("2000"),
                    Fund = MunicipalFundType.SpecialRevenue,
                    Type = AccountType.Revenue,
                    Balance = 20000m,
                    BudgetAmount = 25000m,
                    IsActive = true,
                    DepartmentId = 1,
                    BudgetPeriodId = 1
                }
            };

            context.MunicipalAccounts.AddRange(accounts);
            await context.SaveChangesAsync();
        }

        private async Task<int> SeedSingleAccountAsync()
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var department = new Department { Id = 1, Name = "Finance", DepartmentCode = "FIN" };
            var budgetPeriod = new BudgetPeriod { Id = 1, Name = "FY2025", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddYears(1) };

            context.Departments.Add(department);
            context.BudgetPeriods.Add(budgetPeriod);
            await context.SaveChangesAsync();

            var account = new MunicipalAccount
            {
                Name = "Test Account",
                AccountNumber = new AccountNumber("9999"),
                Fund = MunicipalFundType.General,
                Type = AccountType.Asset,
                Balance = 5000m,
                BudgetAmount = 10000m,
                IsActive = true,
                DepartmentId = 1,
                BudgetPeriodId = 1
            };

            context.MunicipalAccounts.Add(account);
            await context.SaveChangesAsync();
            return account.Id;
        }

        private MunicipalAccount CreateValidAccount()
        {
            return new MunicipalAccount
            {
                Name = "Test Account",
                AccountNumber = new AccountNumber("5000"),
                Fund = MunicipalFundType.General,
                Type = AccountType.Asset,
                Balance = 1000m,
                BudgetAmount = 2000m,
                IsActive = true,
                DepartmentId = 1,
                BudgetPeriodId = 1
            };
        }

        public void Dispose()
        {
            if (_provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
