using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;
using QuickBooksAccount = Intuit.Ipp.Data.Account;
using QuickBooksAccountTypeEnum = Intuit.Ipp.Data.AccountTypeEnum;

namespace WileyWidget.ViewModels.Tests.RepositoryTests;

/// <summary>
/// Comprehensive tests for MunicipalAccountRepository covering all 45+ methods
/// Tests CRUD operations, queries, filtering, pagination, caching, QuickBooks sync,
/// hierarchy management, concurrency handling, and edge cases.
/// Target: 90%+ code coverage
/// </summary>
public class MunicipalAccountRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly MunicipalAccountRepository _repository;

    public MunicipalAccountRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_MunicipalAccount_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _contextFactory = new TestDbContextFactory(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _repository = new MunicipalAccountRepository(_contextFactory, _cache);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Seed departments
        var department1 = new Department { Id = 1, Name = "Public Works", DepartmentCode = "PW" };
        var department2 = new Department { Id = 2, Name = "Finance", DepartmentCode = "FIN" };
        var department3 = new Department { Id = 3, Name = "Parks & Recreation", DepartmentCode = "PARKS" };

        _context.Departments.AddRange(department1, department2, department3);

        // Seed budget periods
        var budgetPeriod = new BudgetPeriod
        {
            Id = 1,
            Name = "FY 2026",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            IsActive = true
        };
        _context.BudgetPeriods.Add(budgetPeriod);

        await _context.SaveChangesAsync();

        // Seed hierarchical municipal accounts
        var accounts = new List<MunicipalAccount>
        {
            // Root level accounts
            new MunicipalAccount
            {
                Id = 1,
                AccountNumber = new AccountNumber("100"),
                Name = "Assets",
                Type = AccountType.Asset,
                TypeDescription = AccountType.Asset.ToString(),
                Fund = MunicipalFundType.General,
                Balance = 100000m,
                BudgetAmount = 120000m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                IsActive = true,
                QuickBooksId = "QB-1",
                LastSyncDate = DateTime.UtcNow.AddDays(-1)
            },
            new MunicipalAccount
            {
                Id = 2,
                AccountNumber = new AccountNumber("200"),
                Name = "Liabilities",
                Type = AccountType.Payables,
                TypeDescription = AccountType.Payables.ToString(),
                Fund = MunicipalFundType.General,
Balance = 50000m,
                BudgetAmount = 60000m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                IsActive = true
            },
            new MunicipalAccount
            {
                Id = 3,
                AccountNumber = new AccountNumber("300"),
                Name = "Revenue",
                Type = AccountType.Sales,
                TypeDescription = AccountType.Sales.ToString(),
                Fund = MunicipalFundType.General,
Balance = 200000m,
                BudgetAmount = 250000m,
                DepartmentId = 2,
                BudgetPeriodId = 1,
                IsActive = true
            },

            // Child accounts (level 2)
            new MunicipalAccount
            {
                Id = 4,
                AccountNumber = new AccountNumber("100.1"),
                Name = "Cash",
                Type = AccountType.Cash,
                TypeDescription = AccountType.Cash.ToString(),
                Fund = MunicipalFundType.General,
Balance = 50000m,
                BudgetAmount = 60000m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                ParentAccountId = 1,
                IsActive = true
            },
            new MunicipalAccount
            {
                Id = 5,
                AccountNumber = new AccountNumber("100.2"),
                Name = "Investments",
                Type = AccountType.Asset,
                TypeDescription = AccountType.Asset.ToString(),
                Fund = MunicipalFundType.General,
Balance = 50000m,
                BudgetAmount = 60000m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                ParentAccountId = 1,
                IsActive = true
            },

            // Grandchild account (level 3)
            new MunicipalAccount
            {
                Id = 6,
                AccountNumber = new AccountNumber("100.1.1"),
                Name = "Petty Cash",
                Type = AccountType.Cash,
                TypeDescription = AccountType.Cash.ToString(),
                Fund = MunicipalFundType.General,
Balance = 1000m,
                BudgetAmount = 2000m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                ParentAccountId = 4,
                IsActive = true
            },

            // Utility fund accounts
            new MunicipalAccount
            {
                Id = 7,
                AccountNumber = new AccountNumber("400"),
                Name = "Water Utility",
                Type = AccountType.Sales,
                TypeDescription = AccountType.Sales.ToString(),
                Fund = MunicipalFundType.Utility,
Balance = 150000m,
                BudgetAmount = 180000m,
                DepartmentId = 3,
                BudgetPeriodId = 1,
                IsActive = true
            },

            // Enterprise fund account
            new MunicipalAccount
            {
                Id = 8,
                AccountNumber = new AccountNumber("500"),
                Name = "Electric Utility",
                Type = AccountType.Sales,
                TypeDescription = AccountType.Sales.ToString(),
                Fund = MunicipalFundType.Enterprise,
Balance = 300000m,
                BudgetAmount = 350000m,
                DepartmentId = 3,
                BudgetPeriodId = 1,
                IsActive = true
            },

            // Inactive account
            new MunicipalAccount
            {
                Id = 9,
                AccountNumber = new AccountNumber("999"),
                Name = "Closed Account",
                Type = AccountType.Asset,
                TypeDescription = AccountType.Asset.ToString(),
                Fund = MunicipalFundType.General,
Balance = 0m,
                BudgetAmount = 0m,
                DepartmentId = 1,
                BudgetPeriodId = 1,
                IsActive = false
            },

            // Account with special characters (testing search)
            new MunicipalAccount
            {
                Id = 10,
                AccountNumber = new AccountNumber("101"),
                Name = "Special Reserve Fund",
                Type = AccountType.Asset,
                Fund = MunicipalFundType.SpecialRevenue,
Balance = 75000m,
                BudgetAmount = 80000m,
                DepartmentId = 2,
                BudgetPeriodId = 1,
                IsActive = true
            }
        };

        _context.MunicipalAccounts.AddRange(accounts);
        await _context.SaveChangesAsync();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDbContextFactory_CreatesRepository()
    {
        // Arrange & Act
        var repository = new MunicipalAccountRepository(_contextFactory, _cache);

        // Assert
        repository.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MunicipalAccountRepository(null!, _cache));
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MunicipalAccountRepository(_contextFactory, null!));
    }

    [Fact]
    public void Constructor_WithDbContextOptions_CreatesRepository()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_Options_{Guid.NewGuid()}")
            .Options;

        // Act
        var repository = new MunicipalAccountRepository(options);

        // Assert
        repository.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MunicipalAccountRepository((DbContextOptions<AppDbContext>)null!));
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllAccounts_OrderedByAccountNumber()
    {
        // Act
        var accounts = await _repository.GetAllAsync();

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().HaveCount(10);

        // Verify ordering
        accountList.Should().BeInAscendingOrder(a => a.AccountNumber!.Value);
    }

    [Fact]
    public async Task GetAllAsync_UsesCaching()
    {
        // Act - First call
        var accounts1 = await _repository.GetAllAsync();

        // Act - Second call (should use cache)
        var accounts2 = await _repository.GetAllAsync();

        // Assert
        accounts1.Should().BeSameAs(accounts2, "second call should return cached results");
    }

    [Fact]
    public async Task GetAllAsync_WithTypeFilter_ReturnsFilteredAccounts()
    {
        // Arrange
        var typeFilter = "Cash";

        // Act
        var accounts = await _repository.GetAllAsync(typeFilter);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.TypeDescription == typeFilter);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsAccount()
    {
        // Arrange
        const int accountId = 1;

        // Act
        var account = await _repository.GetByIdAsync(accountId);

        // Assert
        account.Should().NotBeNull();
        account!.Id.Should().Be(accountId);
        account.Name.Should().Be("Assets");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int invalidId = 99999;

        // Act
        var account = await _repository.GetByIdAsync(invalidId);

        // Assert
        account.Should().BeNull();
    }

    #endregion

    #region GetByAccountNumberAsync Tests

    [Fact]
    public async Task GetByAccountNumberAsync_WithValidNumber_ReturnsAccount()
    {
        // Arrange
        const string accountNumber = "100";

        // Act
        var account = await _repository.GetByAccountNumberAsync(accountNumber);

        // Assert
        account.Should().NotBeNull();
        account!.AccountNumber!.Value.Should().Be(accountNumber);
        account.Name.Should().Be("Assets");
    }

    [Fact]
    public async Task GetByAccountNumberAsync_WithInvalidNumber_ReturnsNull()
    {
        // Arrange
        const string invalidNumber = "999999";

        // Act
        var account = await _repository.GetByAccountNumberAsync(invalidNumber);

        // Assert
        account.Should().BeNull();
    }

    [Fact]
    public async Task GetByAccountNumberAsync_WithHierarchicalNumber_ReturnsChildAccount()
    {
        // Arrange
        const string accountNumber = "100.1";

        // Act
        var account = await _repository.GetByAccountNumberAsync(accountNumber);

        // Assert
        account.Should().NotBeNull();
        account!.AccountNumber!.Value.Should().Be(accountNumber);
        account.Name.Should().Be("Cash");
        account.ParentAccountId.Should().Be(1);
    }

    #endregion

    #region GetActiveAsync Tests

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveAccounts()
    {
        // Act
        var accounts = await _repository.GetActiveAsync();

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.IsActive);
        accountList.Should().NotContain(a => a.Id == 9, "inactive account should be excluded");
    }

    #endregion

    #region GetByFundAsync Tests

    [Fact]
    public async Task GetByFundAsync_WithGeneralFund_ReturnsGeneralFundAccounts()
    {
        // Arrange
        var fund = MunicipalFundType.General;

        // Act
        var accounts = await _repository.GetByFundAsync(fund);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Fund == fund && a.IsActive);
    }

    [Fact]
    public async Task GetByFundAsync_WithUtilityFund_ReturnsUtilityFundAccounts()
    {
        // Arrange
        var fund = MunicipalFundType.Utility;

        // Act
        var accounts = await _repository.GetByFundAsync(fund);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Fund == fund && a.IsActive);
        accountList.Should().Contain(a => a.Name == "Water Utility");
    }

    [Fact]
    public async Task GetByFundAsync_WithEnterpriseFund_ReturnsEnterpriseFundAccounts()
    {
        // Arrange
        var fund = MunicipalFundType.Enterprise;

        // Act
        var accounts = await _repository.GetByFundAsync(fund);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Fund == fund && a.IsActive);
    }

    #endregion

    #region GetByTypeAsync Tests

    [Fact]
    public async Task GetByTypeAsync_WithCashType_ReturnsCashAccounts()
    {
        // Arrange
        var type = AccountType.Cash;

        // Act
        var accounts = await _repository.GetByTypeAsync(type);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Type == type && a.IsActive);
    }

    [Fact]
    public async Task GetByTypeAsync_WithAssetType_ReturnsAssetAccounts()
    {
        // Arrange
        var type = AccountType.Asset;

        // Act
        var accounts = await _repository.GetByTypeAsync(type);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Type == type && a.IsActive);
    }

    [Fact]
    public async Task GetByTypeAsync_WithSalesType_ReturnsSalesAccounts()
    {
        // Arrange
        var type = AccountType.Sales;

        // Act
        var accounts = await _repository.GetByTypeAsync(type);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Type == type && a.IsActive);
    }

    #endregion

    #region GetByDepartmentAsync Tests

    [Fact]
    public async Task GetByDepartmentAsync_WithValidDepartmentId_ReturnsAccountsForDepartment()
    {
        // Arrange
        const int departmentId = 1;

        // Act
        var accounts = await _repository.GetByDepartmentAsync(departmentId);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.DepartmentId == departmentId && a.IsActive);
    }

    [Fact]
    public async Task GetByDepartmentAsync_WithInvalidDepartmentId_ReturnsEmpty()
    {
        // Arrange
        const int invalidDepartmentId = 99999;

        // Act
        var accounts = await _repository.GetByDepartmentAsync(invalidDepartmentId);

        // Assert
        accounts.Should().BeEmpty();
    }

    #endregion

    #region GetByFundClassAsync Tests

    [Fact]
    public async Task GetByFundClassAsync_WithGovernmentalClass_ReturnsGovernmentalAccounts()
    {
        // Arrange
        var fundClass = FundClass.Governmental;

        // Act
        var accounts = await _repository.GetByFundClassAsync(fundClass);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.FundClass == fundClass && a.IsActive);
    }

    [Fact]
    public async Task GetByFundClassAsync_WithProprietaryClass_ReturnsProprietaryAccounts()
    {
        // Arrange
        var fundClass = FundClass.Proprietary;

        // Act
        var accounts = await _repository.GetByFundClassAsync(fundClass);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.FundClass == fundClass && a.IsActive);
    }

    #endregion

    #region GetByAccountTypeAsync Tests

    [Fact]
    public async Task GetByAccountTypeAsync_WithValidType_ReturnsAccounts()
    {
        // Arrange
        var accountType = AccountType.Asset;

        // Act
        var accounts = await _repository.GetByAccountTypeAsync(accountType);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Type == accountType && a.IsActive);
    }

    #endregion

    #region Hierarchy Tests

    [Fact]
    public async Task GetChildAccountsAsync_WithParentAccount_ReturnsChildAccounts()
    {
        // Arrange
        const int parentAccountId = 1; // "100" has children "100.1" and "100.2"

        // Act
        var childAccounts = await _repository.GetChildAccountsAsync(parentAccountId);

        // Assert
        var childList = childAccounts.ToList();
        childList.Should().NotBeEmpty();
        childList.Should().HaveCountGreaterOrEqualTo(2);
        childList.Should().OnlyContain(a => a.ParentAccountId == parentAccountId && a.IsActive);
        childList.Should().Contain(a => a.AccountNumber!.Value == "100.1");
        childList.Should().Contain(a => a.AccountNumber!.Value == "100.2");
    }

    [Fact]
    public async Task GetChildAccountsAsync_WithLeafAccount_ReturnsEmpty()
    {
        // Arrange
        const int leafAccountId = 6; // "100.1.1" has no children

        // Act
        var childAccounts = await _repository.GetChildAccountsAsync(leafAccountId);

        // Assert
        childAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountHierarchyAsync_WithRootAccount_ReturnsFullHierarchy()
    {
        // Arrange
        const int rootAccountId = 1; // "100"

        // Act
        var hierarchy = await _repository.GetAccountHierarchyAsync(rootAccountId);

        // Assert
        var hierarchyList = hierarchy.ToList();
        hierarchyList.Should().NotBeEmpty();
        hierarchyList.Should().Contain(a => a.AccountNumber!.Value == "100");
        hierarchyList.Should().Contain(a => a.AccountNumber!.Value == "100.1");
        hierarchyList.Should().Contain(a => a.AccountNumber!.Value == "100.2");
        hierarchyList.Should().Contain(a => a.AccountNumber!.Value == "100.1.1");
    }

    [Fact]
    public async Task GetAccountHierarchyAsync_WithInvalidId_ReturnsEmpty()
    {
        // Arrange
        const int invalidId = 99999;

        // Act
        var hierarchy = await _repository.GetAccountHierarchyAsync(invalidId);

        // Assert
        hierarchy.Should().BeEmpty();
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchByNameAsync_WithMatchingTerm_ReturnsMatchingAccounts()
    {
        // Arrange
        const string searchTerm = "Cash";

        // Act
        var accounts = await _repository.SearchByNameAsync(searchTerm);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Name.Contains(searchTerm) && a.IsActive);
        accountList.Should().Contain(a => a.Name == "Cash");
        accountList.Should().Contain(a => a.Name == "Petty Cash");
    }

    [Fact]
    public async Task SearchByNameAsync_WithNonMatchingTerm_ReturnsEmpty()
    {
        // Arrange
        const string searchTerm = "NonExistentAccount";

        // Act
        var accounts = await _repository.SearchByNameAsync(searchTerm);

        // Assert
        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByNameAsync_WithPartialMatch_ReturnsMatchingAccounts()
    {
        // Arrange
        const string searchTerm = "Fund";

        // Act
        var accounts = await _repository.SearchByNameAsync(searchTerm);

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Name.Contains(searchTerm) && a.IsActive);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task AccountNumberExistsAsync_WithExistingNumber_ReturnsTrue()
    {
        // Arrange
        const string accountNumber = "100";

        // Act
        var exists = await _repository.AccountNumberExistsAsync(accountNumber);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AccountNumberExistsAsync_WithNonExistingNumber_ReturnsFalse()
    {
        // Arrange
        const string accountNumber = "999999";

        // Act
        var exists = await _repository.AccountNumberExistsAsync(accountNumber);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AccountNumberExistsAsync_WithExcludedId_ReturnsCorrectResult()
    {
        // Arrange
        const string accountNumber = "100";
        const int excludeId = 1; // Exclude the account with number "100"

        // Act
        var exists = await _repository.AccountNumberExistsAsync(accountNumber, excludeId);

        // Assert
        exists.Should().BeFalse("the account with this number is excluded");
    }

    #endregion

    #region Count Tests

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Act
        var count = await _repository.GetCountAsync();

        // Assert
        count.Should().Be(10);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task GetPagedAsync_FirstPage_ReturnsCorrectResults()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 5;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        // Assert
        var itemList = items.ToList();
        itemList.Should().HaveCount(5);
        totalCount.Should().Be(10);
        itemList.Should().BeInAscendingOrder(a => a.AccountNumber!.Value);
    }

    [Fact]
    public async Task GetPagedAsync_SecondPage_ReturnsCorrectResults()
    {
        // Arrange
        const int pageNumber = 2;
        const int pageSize = 5;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        // Assert
        var itemList = items.ToList();
        itemList.Should().HaveCount(5);
        totalCount.Should().Be(10);
    }

    [Fact]
    public async Task GetPagedAsync_WithSortByName_SortsCorrectly()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        const string sortBy = "name";

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, sortBy);

        // Assert
        var itemList = items.ToList();
        itemList.Should().BeInAscendingOrder(a => a.Name);
    }

    [Fact]
    public async Task GetPagedAsync_WithSortByBalance_SortsCorrectly()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        const string sortBy = "balance";

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, sortBy);

        // Assert
        var itemList = items.ToList();
        itemList.Should().BeInAscendingOrder(a => a.Balance);
    }

    [Fact]
    public async Task GetPagedAsync_WithDescendingSort_ReturnsDescendingOrder()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        const bool sortDescending = true;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, sortDescending: sortDescending);

        // Assert
        var itemList = items.ToList();
        itemList.Should().BeInDescendingOrder(a => a.AccountNumber!.Value);
    }

    #endregion

    #region GetQueryableAsync Tests

    [Fact]
    public async Task GetQueryableAsync_ReturnsQueryable()
    {
        // Act
        var queryable = await _repository.GetQueryableAsync();

        // Assert
        queryable.Should().NotBeNull();
        queryable.Should().BeAssignableTo<IQueryable<MunicipalAccount>>();
    }

    #endregion

    #region GetAllWithRelatedAsync Tests

    [Fact]
    public async Task GetAllWithRelatedAsync_IncludesDepartmentAndBudgetEntries()
    {
        // Act
        var accounts = await _repository.GetAllWithRelatedAsync();

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.Department != null);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidAccount_AddsToDatabase()
    {
        // Arrange
        var newAccount = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("600"),
            Name = "New Test Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
Balance = 10000m,
            BudgetAmount = 12000m,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            IsActive = true
        };

        // Act
        var addedAccount = await _repository.AddAsync(newAccount);

        // Assert
        addedAccount.Should().NotBeNull();
        addedAccount.Id.Should().BeGreaterThan(0);

        // Verify it was added to database
        var retrieved = await _repository.GetByIdAsync(addedAccount.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("New Test Account");
    }

    [Fact]
    public async Task AddAsync_WithDuplicateAccountNumber_ThrowsException()
    {
        // Arrange
        var newAccount = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("100"), // Duplicate
            Name = "Duplicate Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
Balance = 1000m,
            BudgetAmount = 1200m,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            IsActive = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _repository.AddAsync(newAccount));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidAccount_UpdatesInDatabase()
    {
        // Arrange
        var account = await _repository.GetByIdAsync(1);
        account.Should().NotBeNull();

        var originalName = account!.Name;
        account.Name = "Updated Assets";
        account.Balance = 150000m;

        // Act
        var updatedAccount = await _repository.UpdateAsync(account);

        // Assert
        updatedAccount.Should().NotBeNull();
        updatedAccount.Name.Should().Be("Updated Assets");
        updatedAccount.Balance.Should().Be(150000m);

        // Verify in database
        var retrieved = await _repository.GetByIdAsync(1);
        retrieved!.Name.Should().Be("Updated Assets");
        retrieved.Balance.Should().Be(150000m);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingAccount_ThrowsException()
    {
        // Arrange
        var nonExistingAccount = new MunicipalAccount
        {
            Id = 99999,
            AccountNumber = new AccountNumber("999999"),
            Name = "Non-Existing Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            Balance = 1000m,
            BudgetAmount = 1200m,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            IsActive = true
        };

        // Act & Assert - Repository wraps DbUpdateConcurrencyException in ConcurrencyConflictException
        await Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await _repository.UpdateAsync(nonExistingAccount));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesAccount()
    {
        // Arrange
        const int accountIdToDelete = 9; // Inactive account with no dependencies

        // Act
        var result = await _repository.DeleteAsync(accountIdToDelete);

        // Assert
        result.Should().BeTrue();

        // Verify deletion
        var deletedAccount = await _repository.GetByIdAsync(accountIdToDelete);
        deletedAccount.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        const int invalidId = 99999;

        // Act
        var result = await _repository.DeleteAsync(invalidId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Budget Analysis Tests

    [Fact]
    public async Task GetBudgetAccountsAsync_ReturnsAccountsWithBudgetData()
    {
        // Act
        var accounts = await _repository.GetBudgetAccountsAsync();

        // Assert
        var accountList = accounts.ToList();
        accountList.Should().NotBeEmpty();
        accountList.Should().OnlyContain(a => a.IsActive && a.BudgetAmount != 0);
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_WithPeriodId_ReturnsAccounts()
    {
        // Arrange
        const int periodId = 1;

        // Act
        var result = await _repository.GetBudgetAnalysisAsync(periodId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<MunicipalAccount>>();
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_WithoutPeriodId_ReturnsAccounts()
    {
        // Act
        var accounts = await _repository.GetBudgetAnalysisAsync();

        // Assert
        accounts.Should().NotBeEmpty();
        accounts.Should().OnlyContain(a => a.IsActive && a.BudgetAmount != 0);
    }

    [Fact]
    public async Task GetBalanceAtFiscalYearStartAsync_ReturnsCorrectBalance()
    {
        // Arrange
        const int accountId = 1;
        var fiscalYearStart = new DateTime(2026, 1, 1);

        // Act
        var balance = await _repository.GetBalanceAtFiscalYearStartAsync(accountId, fiscalYearStart);

        // Assert
        balance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBalanceAtFiscalYearStartAsync_WithInvalidId_ReturnsZero()
    {
        // Arrange
        const int invalidId = 99999;
        var fiscalYearStart = new DateTime(2026, 1, 1);

        // Act
        var balance = await _repository.GetBalanceAtFiscalYearStartAsync(invalidId, fiscalYearStart);

        // Assert
        balance.Should().Be(0m);
    }

    #endregion

    #region GetCurrentActiveBudgetPeriodAsync Tests

    [Fact]
    public async Task GetCurrentActiveBudgetPeriodAsync_ReturnsActivePeriod()
    {
        // Act
        var budgetPeriod = await _repository.GetCurrentActiveBudgetPeriodAsync();

        // Assert
        budgetPeriod.Should().NotBeNull();
        budgetPeriod!.IsActive.Should().BeTrue();
        budgetPeriod.Year.Should().Be(2026);
    }

    #endregion

    #region QuickBooks Sync Tests (Basic)

    [Fact]
    public async Task SyncFromQuickBooksAsync_NoOp_CompletesSuccessfully()
    {
        // Act & Assert (should not throw)
        await _repository.SyncFromQuickBooksAsync();
    }

    [Fact]
    public async Task SyncFromQuickBooksAsync_WithNullAccounts_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _repository.SyncFromQuickBooksAsync(null!));
    }

    [Fact]
    public async Task SyncFromQuickBooksAsync_WithValidAccounts_CreatesNewAccounts()
    {
        // Arrange
        var qbAccounts = new List<Intuit.Ipp.Data.Account>
        {
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-NEW-1",
                AcctNum = "700",
                Name = "QuickBooks Test Account",
                // AccountType omitted - will be mapped by repository
                Active = true,
                CurrentBalance = 25000m
            }
        };

        // Act
        await _repository.SyncFromQuickBooksAsync(qbAccounts);

        // Assert
        var newAccount = await _repository.GetByAccountNumberAsync("700");
        newAccount.Should().NotBeNull();
        newAccount!.Name.Should().Be("QuickBooks Test Account");
        newAccount.QuickBooksId.Should().Be("QB-NEW-1");
        newAccount.Balance.Should().Be(25000m);
    }

    [Fact]
    public async Task SyncFromQuickBooksAsync_WithExistingAccount_UpdatesAccount()
    {
        // Arrange
        var qbAccounts = new List<Intuit.Ipp.Data.Account>
        {
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-1",
                AcctNum = "100",
                Name = "Updated Assets Name",
                // AccountType omitted - will be mapped by repository
                Active = true,
                CurrentBalance = 150000m
            }
        };

        // Act
        await _repository.SyncFromQuickBooksAsync(qbAccounts);

        // Assert
        var updatedAccount = await _repository.GetByAccountNumberAsync("100");
        updatedAccount.Should().NotBeNull();
        updatedAccount!.Name.Should().Be("Updated Assets Name");
        updatedAccount.Balance.Should().Be(150000m);
        updatedAccount.LastSyncDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region ImportChartOfAccountsAsync Tests

    [Fact]
    public async Task ImportChartOfAccountsAsync_WithNullChartAccounts_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.ImportChartOfAccountsAsync(null!));
    }

    [Fact]
    public async Task ImportChartOfAccountsAsync_WithEmptyList_ThrowsArgumentException()
    {
        // Arrange
        var emptyList = new List<Intuit.Ipp.Data.Account>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.ImportChartOfAccountsAsync(emptyList));
    }

    [Fact]
    public async Task ImportChartOfAccountsAsync_WithInvalidAccountNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidAccounts = new List<Intuit.Ipp.Data.Account>
        {
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-INVALID",
                AcctNum = "ABC-XYZ", // Invalid format
                Name = "Invalid Account",
                // AccountType omitted
                Active = true
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _repository.ImportChartOfAccountsAsync(invalidAccounts));
    }

    [Fact]
    public async Task ImportChartOfAccountsAsync_WithDuplicateAccountNumbers_ThrowsInvalidOperationException()
    {
        // Arrange
        var duplicateAccounts = new List<Intuit.Ipp.Data.Account>
        {
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-DUP-1",
                AcctNum = "800",
                Name = "Duplicate 1",
                // AccountType omitted
                Active = true
            },
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-DUP-2",
                AcctNum = "800", // Duplicate
                Name = "Duplicate 2",
                // AccountType omitted
                Active = true
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _repository.ImportChartOfAccountsAsync(duplicateAccounts));
    }

    [Fact]
    public async Task ImportChartOfAccountsAsync_WithValidAccounts_ImportsSuccessfully()
    {
        // Arrange
        var validAccounts = new List<Intuit.Ipp.Data.Account>
        {
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-IMPORT-1",
                AcctNum = "800",
                Name = "Import Test Parent",
                // AccountType omitted
                Active = true,
                CurrentBalance = 50000m
            },
            new Intuit.Ipp.Data.Account
            {
                Id = "QB-IMPORT-2",
                AcctNum = "800.1",
                Name = "Import Test Child",
                // AccountType omitted
                Active = true,
                CurrentBalance = 25000m
            }
        };

        // Act
        await _repository.ImportChartOfAccountsAsync(validAccounts);

        // Assert
        var parentAccount = await _repository.GetByAccountNumberAsync("800");
        parentAccount.Should().NotBeNull();
        parentAccount!.Name.Should().Be("Import Test Parent");

        var childAccount = await _repository.GetByAccountNumberAsync("800.1");
        childAccount.Should().NotBeNull();
        childAccount!.Name.Should().Be("Import Test Child");
        childAccount.ParentAccountId.Should().Be(parentAccount.Id);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesResourcesCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_Dispose_{Guid.NewGuid()}")
            .Options;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new TestDbContextFactory(options);
        var repository = new MunicipalAccountRepository(factory, cache);

        // Act
        repository.Dispose();

        // Assert - Should not throw
        repository.Dispose(); // Second dispose should be safe
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repository?.Dispose();
            _cache?.Dispose();
            _context?.Dispose();
        }
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Test implementation of IDbContextFactory for in-memory testing
    /// </summary>
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppDbContext(_options));
        }
    }

    #endregion
}
