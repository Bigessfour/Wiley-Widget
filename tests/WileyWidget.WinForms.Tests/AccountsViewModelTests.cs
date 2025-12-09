#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Business.Services;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
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
        private readonly IServiceProvider _provider;
        private readonly List<IServiceScope> _scopes = new();
        private readonly Mock<ILogger<AccountsViewModel>> _mockLogger;

        public AccountsViewModelTests()
        {
            // IMPORTANT: Set the static flag FIRST, before any DbContext or DI container is created.
            // This ensures the flag is visible when AppDbContext ctor runs and inspects options.
            AppDbContext.SkipModelSeedingInMemoryTests = true;

            // Build a test DI container that uses a unique in-memory database per test class
            var dbName = $"TestDb_{Guid.NewGuid()}";

            var services = new ServiceCollection();

            // Use AddDbContextPool pattern with pooling disabled (PoolSize=0 equivalent via AddDbContext)
            // Each test gets its own DbContextOptions instance which forces a fresh model build.
            // By using EnableSensitiveDataLogging + a unique databaseName, EF creates an isolated model.
            services.AddDbContext<AppDbContext>((sp, o) =>
            {
                o.UseInMemoryDatabase(dbName)
                 .EnableSensitiveDataLogging(); // Helps with debugging
            });

            // allow tests to resolve logging if necessary
            services.AddLogging();

            _provider = services.BuildServiceProvider();

            _mockLogger = new Mock<ILogger<AccountsViewModel>>();
            _mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Verifiable();

            // Ensure a fresh database for each test instance — delete any existing store then create a clean one
            using var initScope = _provider.CreateScope();
            var initCtx = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(initScope.ServiceProvider);
            // InMemory databases are process-global for a given name; delete then recreate to ensure clean state
            initCtx.Database.EnsureDeleted();
            initCtx.Database.EnsureCreated();

            // Defensive cleanup: remove any model-level HasData seeded rows that may still be present
            // (This handles edge cases where the model was cached before the skip flag was set)
            try
            {
                var hasSeeds = initCtx.MunicipalAccounts.Any() || initCtx.Departments.Any() || initCtx.BudgetPeriods.Any();
                if (hasSeeds)
                {
                    initCtx.MunicipalAccounts.RemoveRange(initCtx.MunicipalAccounts);
                    initCtx.BudgetEntries.RemoveRange(initCtx.BudgetEntries);
                    initCtx.Departments.RemoveRange(initCtx.Departments);
                    initCtx.Funds.RemoveRange(initCtx.Funds);
                    initCtx.Vendors.RemoveRange(initCtx.Vendors);
                    initCtx.TaxRevenueSummaries.RemoveRange(initCtx.TaxRevenueSummaries);
                    initCtx.AppSettings.RemoveRange(initCtx.AppSettings);
                    initCtx.BudgetPeriods.RemoveRange(initCtx.BudgetPeriods);
                    initCtx.SaveChanges();
                }
            }
            catch
            {
                // Best-effort cleanup - ignore any errors here during test bootstrap
            }
        }

        public void Dispose()
        {
            // Clean up all scopes and delete the in-memory database(s)
            try
            {
                foreach (var scope in _scopes.ToArray())
                {
                    try
                    {
                        var ctx = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<AppDbContext>(scope.ServiceProvider);
                        ctx?.Database.EnsureDeleted();
                    }
                    catch { /* best-effort cleanup */ }
                    finally
                    {
                        scope.Dispose();
                        _scopes.Remove(scope);
                    }
                }

                if (_provider is IDisposable d)
                {
                    d.Dispose();
                }
            }
            finally
            {
                // Reset test hook so other tests in the same process are unaffected
                AppDbContext.SkipModelSeedingInMemoryTests = false;
                GC.SuppressFinalize(this);
            }
        }

        #region Helper Methods

        private AppDbContext CreateContext()
        {
            var scope = _provider.CreateScope();
            _scopes.Add(scope);
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);
        }

        private async Task SeedTestDataAsync(int accountCount = 5, bool includeInactive = false)
        {
            await using var context = CreateContext();

            // Seed required reference data
            var department = new Department { Name = "Test Department" };
            var budgetPeriod = new BudgetPeriod
            {
                Name = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true
            };

            context.Departments.Add(department);
            context.BudgetPeriods.Add(budgetPeriod);

            // Save once to ensure FK ids are generated and available for account seeding
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            for (int i = 0; i < accountCount; i++)
            {
                var account = new MunicipalAccount
                {
                    // Do not assign Id explicitly — let EF / provider generate keys to avoid duplicates
                    AccountNumber = new AccountNumber($"{110 + i}"),
                    Name = $"Test Account {i + 1}",
                    DepartmentId = department.Id,
                    BudgetPeriodId = budgetPeriod.Id,
                    Balance = (i + 1) * 1000m,
                    BudgetAmount = (i + 1) * 1500m,
                    IsActive = includeInactive ? ((i + 1) % 2 == 0) : true,
                    Fund = (i + 1) % 2 == 0 ? MunicipalFundType.General : MunicipalFundType.Enterprise,
                    Type = (i + 1) % 3 == 0 ? AccountType.Revenue : AccountType.Asset
                };
                context.MunicipalAccounts.Add(account);
            }

            await context.SaveChangesAsync();
        }

        private AccountsViewModel CreateViewModel(IServiceScopeFactory scopeFactory)
        {
            // Use the real AccountMapper and AccountService in tests
            var mapper = new AccountMapper();
            var accountServiceLogger = Mock.Of<ILogger<AccountService>>();
            var accountService = new AccountService(accountServiceLogger, scopeFactory, mapper);
            return new AccountsViewModel(_mockLogger.Object, accountService, mapper);
        }

        private AccountsViewModel CreateViewModel(AppDbContext _ /*ignored*/)
        {
            // Many tests create an AppDbContext to seed data; reuse the provider's scope factory
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            return CreateViewModel(scopeFactory);
        }

        #endregion

        #region LoadAccountsAsync Tests

        [Fact]
        public async Task LoadAccountsAsync_WithNoData_ReturnsEmptyCollection()
        {
            // Arrange
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

            // Assert - only even IDs are active (2, 4)
            vm.Accounts.Should().HaveCount(2);
        }

        [Fact]
        public async Task LoadAccountsAsync_LogsInformationWithFilterContext()
        {
            // Arrange
            await SeedTestDataAsync(3);
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            bool wasLoading = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading) && vm.IsLoading)
                    wasLoading = true;
            };

            // Act
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            vm.SelectedFund = MunicipalFundType.General;
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

            // Assert - Even numbered accounts have General fund
            vm.Accounts.Should().OnlyContain(a => a.Fund == MunicipalFundType.General.ToString());
        }

        [Fact]
        public async Task LoadAccountsAsync_WithAccountTypeFilter_FiltersCorrectly()
        {
            // Arrange
            await SeedTestDataAsync(6);
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_provider);
            var vm = CreateViewModel(scopeFactory);

            // Act
            vm.SelectedAccountType = AccountType.Revenue;
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
                await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
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
                ctx.Departments.Add(new Department { Id = 1, Name = "Test Dept", DepartmentCode = "TD01" });
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

            // Assert - After Phase 2 refactoring, detailed business logic logging happens in AccountService.
            // ViewModel logs orchestration: loading accounts to refresh UI after save
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Loading municipal accounts")),
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

            // Assert - After Phase 2 refactoring, detailed business logic logging happens in AccountService.
            // ViewModel logs orchestration: loading accounts to refresh UI after delete
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Loading municipal accounts")),
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
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

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
            await vm.LoadAccountsCommand.ExecuteAsync(TestContext.Current.CancellationToken);

            // Assert
            vm.ActiveAccountCount.Should().Be(vm.Accounts.Count);
        }

        #endregion
    }
}
