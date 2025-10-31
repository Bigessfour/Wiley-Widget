#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data
{
    /// <summary>
    /// Repository implementation for MunicipalAccount data operations
    /// </summary>
    public class MunicipalAccountRepository : IMunicipalAccountRepository, IDisposable
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private bool _disposed;

        // Primary constructor for DI with IDbContextFactory
        public MunicipalAccountRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // Convenience constructor for unit tests that supply an AppDbContext directly
        public MunicipalAccountRepository(AppDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            throw new NotSupportedException("Use the constructor that takes DbContextOptions<AppDbContext> instead. This constructor is deprecated.");
        }

        // Convenience constructor for unit tests that supply DbContextOptions
        public MunicipalAccountRepository(DbContextOptions<AppDbContext> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Create a factory that creates new contexts with the same options
            _contextFactory = new TestDbContextFactory(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

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
        }        public async Task<IEnumerable<MunicipalAccount>> GetAllAsync()
        {
            const string cacheKey = "MunicipalAccounts_All";

            if (!_cache.TryGetValue(cacheKey, out IEnumerable<MunicipalAccount>? accounts))
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                accounts = await context.MunicipalAccounts
                    .OrderBy(ma => ma.AccountNumber!.Value)
                    .ToListAsync();

                _cache.Set(cacheKey, accounts, TimeSpan.FromMinutes(5));
            }

            return accounts!;
        }

        /// <summary>
        /// Gets paged municipal accounts with sorting support
        /// </summary>
        public async Task<(IEnumerable<MunicipalAccount> Items, int TotalCount)> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = 50,
            string? sortBy = null,
            bool sortDescending = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.MunicipalAccounts.AsQueryable();

            // Apply sorting
            query = ApplySorting(query, sortBy, sortDescending);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply paging
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>
        /// Gets an IQueryable for flexible querying and paging
        /// </summary>
        public async Task<IQueryable<MunicipalAccount>> GetQueryableAsync()
        {
            var context = await _contextFactory.CreateDbContextAsync();
            return context.MunicipalAccounts.AsQueryable();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Include(ma => ma.Department)
                .Include(ma => ma.BudgetEntries)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAllAsync(string typeFilter)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(a => a.TypeDescription == typeFilter)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetActiveAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

    public async Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
        .Where(ma => ma.Fund == fund && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

    public async Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
        .Where(ma => ma.Type == type && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<MunicipalAccount?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts.FindAsync(id);
        }

        public async Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .FirstOrDefaultAsync(ma => ma.AccountNumber!.Value == accountNumber);
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.DepartmentId == departmentId && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByFundClassAsync(FundClass fundClass)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.FundClass == fundClass && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByAccountTypeAsync(AccountType accountType)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Type == accountType && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetChildAccountsAsync(int parentAccountId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.ParentAccountId == parentAccountId && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAccountHierarchyAsync(int rootAccountId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // This is a simplified implementation - in a real scenario you'd need
            // a recursive CTE or stored procedure to get the full hierarchy
            var rootAccount = await context.MunicipalAccounts.FindAsync(rootAccountId);
            if (rootAccount == null)
                return new List<MunicipalAccount>();

            return await context.MunicipalAccounts
                .Where(ma => ma.AccountNumber!.Value.StartsWith(rootAccount.AccountNumber!.Value) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> SearchByNameAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.Name.Contains(searchTerm) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.MunicipalAccounts.Where(ma => ma.AccountNumber!.Value == accountNumber);

            if (excludeId.HasValue)
                query = query.Where(ma => ma.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<int> GetCountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts.CountAsync();
        }

        // Note: This method is not applicable with the simplified budget schema
        // BudgetEntry no longer has MunicipalAccountId or BudgetPeriodId
        // public async Task<IEnumerable<MunicipalAccount>> GetAccountsWithBudgetEntriesAsync(int budgetPeriodId)
        // {
        //     using var context = await _contextFactory.CreateDbContextAsync();
        //     return await context.MunicipalAccounts
        //         .Where(ma => ma.IsActive &&
        //                    context.BudgetEntries.Any(be => be.MunicipalAccountId == ma.Id && be.BudgetPeriodId == budgetPeriodId))
        //         .OrderBy(ma => ma.AccountNumber)
        //         .ToListAsync();
        // }

        public async Task<MunicipalAccount> AddAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Add(account);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }
            return account;
        }

        public async Task<MunicipalAccount> UpdateAsync(MunicipalAccount account)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.MunicipalAccounts.Update(account);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }

            return account;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var account = await context.MunicipalAccounts.FindAsync(id);
            if (account != null)
            {
                context.MunicipalAccounts.Remove(account);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
                }
                catch (DbUpdateException ex)
                {
                    // Handle other update exceptions (e.g., FK constraints)
                    throw new InvalidOperationException($"Failed to delete MunicipalAccount with ID {id}: {ex.Message}", ex);
                }
                return true;
            }
            return false;
        }

        public async Task SyncFromQuickBooksAsync()
        {
            // No-op implementation - actual sync requires QB accounts parameter
            // This satisfies the interface requirement
            await Task.CompletedTask;
        }

        /// <summary>
        /// Imports chart of accounts data from QuickBooks for production use
        /// </summary>
        /// <param name="chartAccounts">List of QuickBooks accounts to import</param>
        /// <exception cref="ArgumentException">Thrown when chartAccounts is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when import validation fails</exception>
        public async Task ImportChartOfAccountsAsync(List<Intuit.Ipp.Data.Account> chartAccounts)
        {
            if (chartAccounts == null || chartAccounts.Count == 0)
            {
                throw new ArgumentException("Chart accounts list cannot be null or empty", nameof(chartAccounts));
            }

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // Validate chart structure before import
                var validationResult = await ValidateChartStructureAsync(chartAccounts);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Chart validation failed: {string.Join(", ", validationResult.Errors)}");
                }

                // Clear existing cache for accounts
                _cache.Remove("municipal_accounts_all");

                // Process accounts in hierarchical order (parents first)
                var processedAccounts = new Dictionary<string, MunicipalAccount>();
                var accountsToProcess = chartAccounts.OrderBy(a => (a.AcctNum ?? "").Split('.').Length).ToList();

                foreach (var qbAccount in accountsToProcess)
                {
                    var municipalAccount = await ProcessQuickBooksAccountAsync(qbAccount, processedAccounts, context);
                    if (municipalAccount != null)
                    {
                        processedAccounts[municipalAccount.AccountNumber!.Value] = municipalAccount;
                    }
                }

                // Update account hierarchies
                await UpdateAccountHierarchiesAsync(processedAccounts, context);

                // Validate final structure
                await ValidateImportedStructureAsync(processedAccounts.Values, context);

                await transaction.CommitAsync();

                // Clear all related caches
                await ClearAccountCachesAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts)
        {
            if (qbAccounts == null)
    {
        throw new ArgumentNullException(nameof(qbAccounts));
    }

    using var context = await _contextFactory.CreateDbContextAsync();
            foreach (var qbAccount in qbAccounts)
            {
                var existingAccount = await context.MunicipalAccounts
                    .FirstOrDefaultAsync(ma => ma.AccountNumber!.Value == (qbAccount.AcctNum ?? string.Empty));

                if (existingAccount == null)
                {
                    // Create new account
                        var newAccount = new MunicipalAccount
                        {
                            AccountNumber = new AccountNumber(qbAccount.AcctNum ?? $"QB-{qbAccount.Id}"),
                            Name = qbAccount.Name,
                            Type = MapQuickBooksAccountType(qbAccount.AccountType),
                            Fund = DetermineFundFromAccount(qbAccount),
                            Balance = qbAccount.CurrentBalance,
                            QuickBooksId = qbAccount.Id,
                            LastSyncDate = DateTime.UtcNow,
                            IsActive = qbAccount.Active
                        };
                    await AddAsync(newAccount);
                }
                else
                {
                    // Update existing account
                    existingAccount.Name = qbAccount.Name;
                    existingAccount.Balance = qbAccount.CurrentBalance;
                    existingAccount.LastSyncDate = DateTime.UtcNow;
                    existingAccount.IsActive = qbAccount.Active;
                    await UpdateAsync(existingAccount);
                }
            }
        }

        /// <summary>
        /// Get account balance at fiscal year start
        /// </summary>
        public async Task<decimal> GetBalanceAtFiscalYearStartAsync(int accountId, DateTime fiscalYearStart)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var account = await context.MunicipalAccounts.FindAsync(accountId);

            if (account == null) return 0m;

            // For simplicity, return budget amount
            // In a real implementation, this would query historical transaction data
            return account.BudgetAmount;
        }

        /// <summary>
        /// Get accounts with budget data for specific fiscal year
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetBudgetAccountsAsync()
        {

            using var context = await _contextFactory.CreateDbContextAsync();

            // Get accounts that have budget entries for this fiscal year
            var accounts = await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();

            return accounts;
        }

        public async Task<object> GetBudgetAnalysisAsync(int periodId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var accounts = await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
            return accounts;
        }

        public async Task<List<MunicipalAccount>> GetBudgetAnalysisAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MunicipalAccounts
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0 && ma.AccountNumber != null)
                .OrderBy(static ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        private AccountType MapQuickBooksAccountType(Intuit.Ipp.Data.AccountTypeEnum? qbType)
        {
            if (qbType == null) return AccountType.Cash;

            // Try to map based on the enum value name
            var typeName = qbType.ToString();
            return typeName switch
            {
                "Asset" => AccountType.Cash,
                "Liability" => AccountType.Payables,
                "Equity" => AccountType.FundBalance,
                "Revenue" => AccountType.Sales,
                "Expense" => AccountType.Supplies,
                _ => AccountType.Cash // Default to Cash for unknown types
            };
        }

        private MunicipalFundType DetermineFundFromAccount(Intuit.Ipp.Data.Account qbAccount)
        {
            // Simple logic to determine fund based on account number or name
            // This can be enhanced based on specific municipal accounting practices
            var accountNumber = qbAccount.AcctNum?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var accountName = qbAccount.Name?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";

            if (accountNumber.Contains("water", StringComparison.Ordinal) || accountName.Contains("water", StringComparison.Ordinal))
                return MunicipalFundType.Utility;
            if (accountNumber.Contains("sewer", StringComparison.Ordinal) || accountName.Contains("sewer", StringComparison.Ordinal))
                return MunicipalFundType.Utility;
            if (accountNumber.Contains("trash", StringComparison.Ordinal) || accountName.Contains("trash", StringComparison.Ordinal) || accountName.Contains("garbage", StringComparison.Ordinal))
                return MunicipalFundType.Utility;

            // Check for enterprise fund indicators
            if (accountNumber.StartsWith("4", StringComparison.Ordinal) || accountNumber.StartsWith("5", StringComparison.Ordinal) ||
                accountName.Contains("enterprise", StringComparison.Ordinal) || accountName.Contains("utility", StringComparison.Ordinal))
                return MunicipalFundType.Enterprise;

            return MunicipalFundType.General;
        }

        private IQueryable<MunicipalAccount> ApplySorting(IQueryable<MunicipalAccount> query, string? sortBy, bool sortDescending)
        {
            if (string.IsNullOrEmpty(sortBy))
            {
                return sortDescending
                    ? query.OrderByDescending(ma => ma.AccountNumber!.Value)
                    : query.OrderBy(ma => ma.AccountNumber!.Value);
            }

            return sortBy.ToLowerInvariant() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(ma => ma.Name)
                    : query.OrderBy(ma => ma.Name),
                "balance" => sortDescending
                    ? query.OrderByDescending(ma => ma.Balance)
                    : query.OrderBy(ma => ma.Balance),
                "type" => sortDescending
                    ? query.OrderByDescending(ma => ma.Type)
                    : query.OrderBy(ma => ma.Type),
                "fund" => sortDescending
                    ? query.OrderByDescending(ma => ma.Fund)
                    : query.OrderBy(ma => ma.Fund),
                _ => sortDescending
                    ? query.OrderByDescending(ma => ma.AccountNumber!.Value)
                    : query.OrderBy(ma => ma.AccountNumber!.Value)
            };
        }

        /// <summary>
        /// Validates the chart of accounts structure before import
        /// </summary>
        private async Task<ChartValidationResult> ValidateChartStructureAsync(List<Intuit.Ipp.Data.Account> chartAccounts)
        {
            var errors = new List<string>();
            var accountNumbers = new HashSet<string>();

            foreach (var account in chartAccounts)
            {
                // Validate account number format
                var accountNumber = account.AcctNum ?? "";
                if (string.IsNullOrEmpty(accountNumber))
                {
                    errors.Add($"Account '{account.Name}' has no account number");
                    continue;
                }

                // Check for duplicates
                if (!accountNumbers.Add(accountNumber))
                {
                    errors.Add($"Duplicate account number: {accountNumber}");
                }

                // Validate account number format (should be numeric with optional dots/hyphens)
                if (!System.Text.RegularExpressions.Regex.IsMatch(accountNumber, @"^\d+([.-]\d+)*$"))
                {
                    errors.Add($"Invalid account number format: {accountNumber}");
                }
            }

            await Task.CompletedTask; // Make method properly async
            return new ChartValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        /// <summary>
        /// Processes a single QuickBooks account into a MunicipalAccount
        /// </summary>
        private async Task<MunicipalAccount?> ProcessQuickBooksAccountAsync(
            Intuit.Ipp.Data.Account qbAccount,
            Dictionary<string, MunicipalAccount> processedAccounts,
            AppDbContext context)
        {
            var accountNumber = qbAccount.AcctNum ?? "";
            if (string.IsNullOrEmpty(accountNumber))
                return null;

            // Check if account already exists
            var existingAccount = await context.MunicipalAccounts
                .FirstOrDefaultAsync(ma => ma.AccountNumber!.Value == accountNumber);

            if (existingAccount != null)
            {
                // Update existing account
                existingAccount.Name = qbAccount.Name ?? existingAccount.Name;
                existingAccount.Type = MapQuickBooksAccountType(qbAccount.AccountType);
                existingAccount.Fund = DetermineFundFromAccount(qbAccount);
                existingAccount.Balance = qbAccount.CurrentBalance;
                existingAccount.LastSyncDate = DateTime.UtcNow;
                existingAccount.IsActive = qbAccount.Active;
                existingAccount.QuickBooksId = qbAccount.Id;

                return existingAccount;
            }
            else
            {
                // Create new account
                var newAccount = new MunicipalAccount
                {
                    AccountNumber = new AccountNumber(accountNumber),
                    Name = qbAccount.Name ?? $"QB Account {accountNumber}",
                    Type = MapQuickBooksAccountType(qbAccount.AccountType),
                    Fund = DetermineFundFromAccount(qbAccount),
                    Balance = qbAccount.CurrentBalance,
                    QuickBooksId = qbAccount.Id,
                    LastSyncDate = DateTime.UtcNow,
                    IsActive = qbAccount.Active,
                    DepartmentId = 1, // Default department, should be configurable
                    BudgetPeriodId = 1 // Default budget period, should be configurable
                };

                // Set type description
                newAccount.TypeDescription = newAccount.Type.ToString();
                newAccount.FundDescription = newAccount.Fund.ToString();

                context.MunicipalAccounts.Add(newAccount);
                await context.SaveChangesAsync();

                return newAccount;
            }
        }

        /// <summary>
        /// Updates account hierarchies after all accounts are processed
        /// </summary>
        private async Task UpdateAccountHierarchiesAsync(
            Dictionary<string, MunicipalAccount> processedAccounts,
            AppDbContext context)
        {
            foreach (var account in processedAccounts.Values)
            {
                var parentNumber = account.AccountNumber!.GetParentNumber();
                if (!string.IsNullOrEmpty(parentNumber) && processedAccounts.TryGetValue(parentNumber, out var parentAccount))
                {
                    account.ParentAccountId = parentAccount.Id;
                    account.ParentAccount = parentAccount;
                }
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Validates the imported account structure
        /// </summary>
        private async Task ValidateImportedStructureAsync(
            IEnumerable<MunicipalAccount> accounts,
            AppDbContext context)
        {
            // Validate that all parent accounts exist
            var accountNumbers = accounts.Select(a => a.AccountNumber!.Value).ToHashSet();
            var orphanedAccounts = accounts.Where(a =>
                a.ParentAccountId.HasValue &&
                !accountNumbers.Contains(a.AccountNumber!.GetParentNumber() ?? "")).ToList();

            if (orphanedAccounts.Any())
            {
                throw new InvalidOperationException(
                    $"Orphaned accounts found: {string.Join(", ", orphanedAccounts.Select(a => a.AccountNumber!.Value))}");
            }

            // Additional validations can be added here
            await Task.CompletedTask; // Make method properly async
        }

        /// <summary>
        /// Clears all account-related caches
        /// </summary>
        private async Task ClearAccountCachesAsync()
        {
            _cache.Remove("municipal_accounts_all");
            // Add other cache keys as needed
            await Task.CompletedTask; // Make method properly async
        }

        /// <summary>
        /// Result of chart validation
        /// </summary>
        private class ChartValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        /// <summary>
        /// Disposes the repository and its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    (_cache as IDisposable)?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
