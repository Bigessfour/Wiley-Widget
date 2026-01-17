#nullable enable
using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data
{
    /// <summary>
    /// Repository implementation for MunicipalAccount data operations
    /// Scoped repository that uses injected singleton IMemoryCache for performance.
    /// Never disposes the cache singleton - follows pattern in UtilityCustomerRepository.
    /// </summary>
    public sealed class MunicipalAccountRepository : IMunicipalAccountRepository, IDisposable
    {
        // Compiled queries to reduce first-query JIT/plan compilation overhead
        private static readonly Func<AppDbContext, List<MunicipalAccount>> CQ_GetAllOrdered =
            EF.CompileQuery((AppDbContext ctx) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .OrderBy(ma => ma.AccountNumber!.Value)
                   .ToList());

        private static readonly Func<AppDbContext, bool, List<MunicipalAccount>> CQ_GetAllActiveFlag =
            EF.CompileQuery((AppDbContext ctx, bool onlyActive) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .Where(ma => !onlyActive || ma.IsActive)
                   .OrderBy(ma => ma.AccountNumber!.Value)
                   .ToList());

        private static readonly Func<AppDbContext, MunicipalFundType, List<MunicipalAccount>> CQ_GetByFund =
            EF.CompileQuery((AppDbContext ctx, MunicipalFundType fund) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .Where(ma => ma.Fund == fund && ma.IsActive)
                   .OrderBy(ma => ma.AccountNumber!.Value)
                   .ToList());

        private static readonly Func<AppDbContext, AccountType, List<MunicipalAccount>> CQ_GetByType =
            EF.CompileQuery((AppDbContext ctx, AccountType type) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .Where(ma => ma.Type == type && ma.IsActive)
                   .OrderBy(ma => ma.AccountNumber!.Value)
                   .ToList());

        private static readonly Func<AppDbContext, int, List<MunicipalAccount>> CQ_GetByDepartment =
            EF.CompileQuery((AppDbContext ctx, int departmentId) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .Where(ma => ma.DepartmentId == departmentId && ma.IsActive)
                   .ToList());

        private static readonly Func<AppDbContext, int, List<MunicipalAccount>> CQ_GetChildren =
            EF.CompileQuery((AppDbContext ctx, int parentAccountId) =>
                ctx.MunicipalAccounts
                   .AsNoTracking()
                   .Where(ma => ma.ParentAccountId == parentAccountId && ma.IsActive)
                   .ToList());

        // For single row lookups, compiled sync query is the most efficient form
        private static readonly Func<AppDbContext, int, MunicipalAccount?> CQ_GetById_NoTracking =
            EF.CompileQuery((AppDbContext ctx, int id) =>
                ctx.MunicipalAccounts.AsNoTracking().SingleOrDefault(ma => ma.Id == id));

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MunicipalAccountRepository>? _logger;
        private readonly bool _ownsCache;

        // Primary constructor for DI with IDbContextFactory
        [ActivatorUtilitiesConstructor]
        public MunicipalAccountRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache, ILogger<MunicipalAccountRepository>? logger = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger;
            // We do not own the injected cache (do not dispose shared singleton)
            _ownsCache = false;
        }

        // Convenience constructor for unit tests that supply DbContextOptions
        internal MunicipalAccountRepository(DbContextOptions<AppDbContext> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Use a simple test factory to create contexts from options
            _contextFactory = new TestDbContextFactory(options);
            _cache = new MemoryCache(new MemoryCacheOptions());
            // In test mode we created the cache and therefore own it
            _ownsCache = true;
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
        }

        /// <summary>
        /// Retrieves all municipal accounts, with caching for performance.
        /// Falls back to database if cache is disposed.
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string cacheKey = "MunicipalAccounts";
            const int cacheExpirationMinutes = 10;

            try
            {
                if (_cache.TryGetValue(cacheKey, out var cachedAccounts))
                {
                    return (IEnumerable<MunicipalAccount>)cachedAccounts!;
                }
            }
            catch (ObjectDisposedException)
            {
                // Cache is disposed; log and proceed to DB fetch
                _logger?.LogWarning("MemoryCache is disposed; fetching municipal accounts directly from database.");
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var accounts = await context.MunicipalAccounts
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            try
            {
                var options = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(cacheExpirationMinutes));

                // Required when SizeLimit is configured: assign logical size
                // Use collection count if applicable, else 1
                long size = accounts switch
                {
                    System.Collections.ICollection collection => collection.Count,
                    _ => 1
                };
                options.SetSize(size);

                _cache.Set(cacheKey, accounts, options);
            }
            catch (ObjectDisposedException)
            {
                // Cache is disposed; skip caching but don't fail
                _logger?.LogWarning("MemoryCache is disposed; skipping cache update for municipal accounts.");
            }

            return accounts;
        }

        /// <summary>
        /// Gets paged municipal accounts with sorting support
        /// </summary>
        public async Task<(IEnumerable<MunicipalAccount> Items, int TotalCount)> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = 50,
            string? sortBy = null,
            bool sortDescending = false,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var query = context.MunicipalAccounts.AsNoTracking().AsQueryable();

            // Apply sorting
            query = ApplySorting(query, sortBy, sortDescending);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply paging (dynamic shapes are not compiled; keep as is)
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        /// <summary>
        /// Gets an IQueryable for flexible querying and paging
        /// NOTE: This returns an IQueryable tied to a DbContext created here; caller is responsible for materializing results promptly.
        /// </summary>
        public Task<IQueryable<MunicipalAccount>> GetQueryableAsync(CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            return Task.FromResult(context.MunicipalAccounts.AsQueryable());
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.MunicipalAccounts
                .AsNoTracking()
                .Include(ma => ma.Department)
                .Include(ma => ma.BudgetEntries)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAllAsync(string typeFilter, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            return await context.MunicipalAccounts
                .AsNoTracking()
                .Where(a => a.TypeDescription == typeFilter)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public Task<IEnumerable<MunicipalAccount>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            var list = CQ_GetAllActiveFlag(context, true);
            return Task.FromResult<IEnumerable<MunicipalAccount>>(list);
        }

        public Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund, CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            var list = CQ_GetByFund(context, fund);
            return Task.FromResult<IEnumerable<MunicipalAccount>>(list);
        }

        public Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type, CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            var list = CQ_GetByType(context, type);
            return Task.FromResult<IEnumerable<MunicipalAccount>>(list);
        }

        public async Task<MunicipalAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            // Compiled + NoTracking for fast first-hit performance
            var entity = CQ_GetById_NoTracking(context, id);
            return await Task.FromResult(entity);
        }

        public async Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.MunicipalAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(ma => ma.AccountNumber!.Value == accountNumber, cancellationToken);
        }

        public Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            var list = CQ_GetByDepartment(context, departmentId);
            // Ensure consistent sort
            return Task.FromResult<IEnumerable<MunicipalAccount>>(list.OrderBy(ma => ma.AccountNumber?.Value).ToList());
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByFundClassAsync(FundClass fundClass, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);

            IQueryable<MunicipalAccount> query = fundClass switch
            {
                FundClass.Governmental => context.MunicipalAccounts.AsNoTracking().Where(ma =>
                    (ma.Fund == MunicipalFundType.General ||
                     ma.Fund == MunicipalFundType.SpecialRevenue ||
                     ma.Fund == MunicipalFundType.CapitalProjects ||
                     ma.Fund == MunicipalFundType.DebtService) && ma.IsActive),

                FundClass.Proprietary => context.MunicipalAccounts.AsNoTracking().Where(ma =>
                    (ma.Fund == MunicipalFundType.Enterprise ||
                     ma.Fund == MunicipalFundType.InternalService) && ma.IsActive),

                FundClass.Fiduciary => context.MunicipalAccounts.AsNoTracking().Where(ma =>
                    (ma.Fund == MunicipalFundType.Trust ||
                     ma.Fund == MunicipalFundType.Agency) && ma.IsActive),

                _ => context.MunicipalAccounts.AsNoTracking().Where(ma => false)
            };

            var accounts = await query.ToListAsync();
            return accounts.OrderBy(ma => ma.AccountNumber?.Value).ToList();
        }

        public async Task<IEnumerable<MunicipalAccount>> GetByAccountTypeAsync(AccountType accountType, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            var accounts = await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.Type == accountType && ma.IsActive)
                .ToListAsync();

            return accounts.OrderBy(ma => ma.AccountNumber?.Value).ToList();
        }

        public Task<IEnumerable<MunicipalAccount>> GetChildAccountsAsync(int parentAccountId, CancellationToken cancellationToken = default)
        {
            var context = _contextFactory.CreateDbContext();
            var list = CQ_GetChildren(context, parentAccountId);
            return Task.FromResult<IEnumerable<MunicipalAccount>>(list.OrderBy(ma => ma.AccountNumber?.Value).ToList());
        }

        public async Task<IEnumerable<MunicipalAccount>> GetAccountHierarchyAsync(int rootAccountId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);

            var rootAccount = await context.MunicipalAccounts.FindAsync(rootAccountId);
            if (rootAccount == null)
                return new List<MunicipalAccount>();

            return await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.AccountNumber!.Value.StartsWith(rootAccount.AccountNumber!.Value) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<IEnumerable<MunicipalAccount>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            return await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.Name.Contains(searchTerm) && ma.IsActive)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            var query = context.MunicipalAccounts.Where(ma => ma.AccountNumber!.Value == accountNumber);

            if (excludeId.HasValue)
                query = query.Where(ma => ma.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        /// <summary>
        /// Gets the total count of municipal accounts.
        /// </summary>
        public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.MunicipalAccounts.CountAsync(cancellationToken);
        }

        public async Task<MunicipalAccount> AddAsync(MunicipalAccount account, CancellationToken cancellationToken = default)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Check for duplicate account number
            var existingAccount = await context.MunicipalAccounts
                .FirstOrDefaultAsync(a => a.AccountNumber != null && account.AccountNumber != null && a.AccountNumber.Value == account.AccountNumber.Value, cancellationToken);

            if (existingAccount != null)
            {
                throw new InvalidOperationException($"An account with number '{account.AccountNumber?.Value ?? "null"}' already exists.");
            }

            context.MunicipalAccounts.Add(account);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }

            return account;
        }

        public async Task<MunicipalAccount> UpdateAsync(MunicipalAccount account, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.MunicipalAccounts.Update(account);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await RepositoryConcurrencyHelper.HandleAsync(ex, nameof(MunicipalAccount)).ConfigureAwait(false);
            }

            return account;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var account = await context.MunicipalAccounts
                .Include(a => a.AccountNumber)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (account != null)
            {
                // Set navigation property to null before deleting to avoid FK constraint issues
                account.AccountNumber = null;

                context.MunicipalAccounts.Remove(account);
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
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

        public async Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts, CancellationToken cancellationToken = default)
        {
            if (qbAccounts == null)
            {
                throw new ArgumentNullException(nameof(qbAccounts));
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            foreach (var qbAccount in qbAccounts)
            {
                var acctNum = qbAccount.AcctNum ?? string.Empty;
                var existingAccount = await context.MunicipalAccounts
                    .FirstOrDefaultAsync(ma => ma.AccountNumber!.Value == acctNum, cancellationToken);

                if (existingAccount == null)
                {
                    var newAccount = new MunicipalAccount
                    {
                        AccountNumber = new AccountNumber(!string.IsNullOrEmpty(acctNum) ? acctNum : $"QB-{qbAccount.Id}"),
                        Name = qbAccount.Name,
                        Type = MapQuickBooksAccountType(qbAccount.AccountType),
                        Fund = DetermineFundFromAccount(qbAccount),
                        Balance = qbAccount.CurrentBalance,
                        QuickBooksId = qbAccount.Id,
                        LastSyncDate = DateTime.UtcNow,
                        IsActive = qbAccount.Active
                    };

                    context.MunicipalAccounts.Add(newAccount);
                }
                else
                {
                    existingAccount.Name = qbAccount.Name;
                    existingAccount.Balance = qbAccount.CurrentBalance;
                    existingAccount.LastSyncDate = DateTime.UtcNow;
                    existingAccount.IsActive = qbAccount.Active;
                    context.MunicipalAccounts.Update(existingAccount);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Get account balance at fiscal year start
        /// </summary>
        public async Task<decimal> GetBalanceAtFiscalYearStartAsync(int accountId, DateTime fiscalYearStart, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            var account = await context.MunicipalAccounts.FindAsync(accountId);

            if (account == null) return 0m;

            // For simplicity, return budget amount
            // In a real implementation, this would query historical transaction data
            return account.BudgetAmount;
        }

        /// <summary>
        /// Get accounts with budget data for specific fiscal year
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetBudgetAccountsAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);

            var accounts = await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync();

            return accounts;
        }

        public async Task<object> GetBudgetAnalysisAsync(int periodId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var accounts = await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0)
                .OrderBy(ma => ma.AccountNumber!.Value)
                .ToListAsync(cancellationToken);
            return accounts;
        }

        public async Task<List<MunicipalAccount>> GetBudgetAnalysisAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(CancellationToken.None);
            return await context.MunicipalAccounts
                .AsNoTracking()
                .Where(ma => ma.IsActive && ma.BudgetAmount != 0 && ma.AccountNumber != null)
                .OrderBy(static ma => ma.AccountNumber!.Value)
                .ToListAsync();
        }

        public async Task<BudgetPeriod?> GetCurrentActiveBudgetPeriodAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.BudgetPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(bp => bp.IsActive, cancellationToken);
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
            var accountNumber = qbAccount.AcctNum?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var accountName = qbAccount.Name?.ToLower(System.Globalization.CultureInfo.InvariantCulture) ?? "";

            if (accountNumber.Contains("water", StringComparison.Ordinal) || accountName.Contains("water", StringComparison.Ordinal))
                return MunicipalFundType.Utility;
            if (accountNumber.Contains("sewer", StringComparison.Ordinal) || accountName.Contains("sewer", StringComparison.Ordinal))
                return MunicipalFundType.Utility;
            if (accountNumber.Contains("trash", StringComparison.Ordinal) || accountName.Contains("trash", StringComparison.Ordinal) || accountName.Contains("garbage", StringComparison.Ordinal))
                return MunicipalFundType.Utility;

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
        private async Task<ChartValidationResult> ValidateChartStructureAsync(List<Intuit.Ipp.Data.Account> chartAccounts, CancellationToken cancellationToken = default)
        {
            var errors = new List<string>();
            var accountNumbers = new HashSet<string>();

            foreach (var account in chartAccounts)
            {
                var accountNumber = account.AcctNum ?? "";
                if (string.IsNullOrEmpty(accountNumber))
                {
                    errors.Add($"Account '{account.Name}' has no account number");
                    continue;
                }

                if (!accountNumbers.Add(accountNumber))
                {
                    errors.Add($"Duplicate account number: {accountNumber}");
                }

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
        private async Task<MunicipalAccount?> ProcessQuickBooksAccountAsync(Intuit.Ipp.Data.Account qbAccount,
            Dictionary<string, MunicipalAccount> processedAccounts,
            AppDbContext context, CancellationToken cancellationToken = default)
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
                // Get the current active budget period
                var currentBudgetPeriod = await context.BudgetPeriods
                    .FirstOrDefaultAsync(bp => bp.IsActive);

                if (currentBudgetPeriod == null)
                {
                    throw new InvalidOperationException("No active budget period found. Cannot create new municipal account.");
                }

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
                    BudgetPeriodId = currentBudgetPeriod.Id // Use current active budget period
                };

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
        private async Task UpdateAccountHierarchiesAsync(Dictionary<string, MunicipalAccount> processedAccounts,
            AppDbContext context, CancellationToken cancellationToken = default)
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
        private async Task ValidateImportedStructureAsync(IEnumerable<MunicipalAccount> accounts,
            AppDbContext context, CancellationToken cancellationToken = default)
        {
            var accountNumbers = accounts.Select(a => a.AccountNumber!.Value).ToHashSet();
            var orphanedAccounts = accounts.Where(a =>
                a.ParentAccountId.HasValue &&
                !accountNumbers.Contains(a.AccountNumber!.GetParentNumber() ?? "")).ToList();

            if (orphanedAccounts.Any())
            {
                throw new InvalidOperationException(
                    $"Orphaned accounts found: {string.Join(", ", orphanedAccounts.Select(a => a.AccountNumber!.Value))}");
            }

            await Task.CompletedTask; // Make method properly async
        }

        /// <summary>
        /// Clears all account-related caches
        /// </summary>
        private async Task ClearAccountCachesAsync(CancellationToken cancellationToken = default)
        {
            _cache.Remove("municipal_accounts_all");
            await Task.CompletedTask; // Make method properly async
        }

        /// <summary>
        /// Imports chart of accounts data from QuickBooks for production use
        /// </summary>
        public async Task ImportChartOfAccountsAsync(List<Intuit.Ipp.Data.Account> chartAccounts, CancellationToken cancellationToken = default)
        {
            // For now, delegate to SyncFromQuickBooksAsync
            await SyncFromQuickBooksAsync(chartAccounts, cancellationToken);
        }

        /// <summary>
        /// Result of chart validation
        /// </summary>
        private class ChartValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        // Repository implements IDisposable to allow disposing caches created by the test constructor.
        // For DI-injected caches we do not dispose (we set _ownsCache = false in normal constructor).
        public void Dispose()
        {
            if (_ownsCache)
            {
                try
                {
                    (_cache as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "MunicipalAccountRepository.Dispose: error disposing owned cache");
                }
            }
        }
    }
}
