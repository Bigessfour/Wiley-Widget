#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for ChartOfAccountEntry data operations
/// </summary>
public class ChartOfAccountsRepository : IChartOfAccountsRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private const string CacheKeyAll = "ChartOfAccounts_All";

    public ChartOfAccountsRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .OrderBy(c => c.Fund!.FundCode)
            .ThenBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetActiveAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Where(c => c.IsActive)
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .OrderBy(c => c.Fund!.FundCode)
            .ThenBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<ChartOfAccountEntry?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .Include(c => c.ParentAccount)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetByFundIdAsync(int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Where(c => c.FundId == fundId)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetByFundCodeAsync(string fundCode)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .Where(c => c.Fund!.FundCode == fundCode)
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetByAccountTypeIdAsync(int accountTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Where(c => c.AccountTypeId == accountTypeId)
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .OrderBy(c => c.Fund!.FundCode)
            .ThenBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<ChartOfAccountEntry?> GetByAccountNumberAndFundAsync(string accountNumber, int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber && c.FundId == fundId);
    }

    public async Task AddAsync(ChartOfAccountEntry entry)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        entry.CreatedAt = DateTime.UtcNow;
        await context.ChartOfAccounts.AddAsync(entry);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task AddRangeAsync(IEnumerable<ChartOfAccountEntry> entries)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            entry.CreatedAt = now;
        }
        await context.ChartOfAccounts.AddRangeAsync(entries);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task UpdateAsync(ChartOfAccountEntry entry)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        entry.UpdatedAt = DateTime.UtcNow;
        context.ChartOfAccounts.Update(entry);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.ChartOfAccounts.FindAsync(id);
        if (entry == null)
            return false;

        context.ChartOfAccounts.Remove(entry);
        await context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool> ExistsAsync(string accountNumber, int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .AnyAsync(c => c.AccountNumber == accountNumber && c.FundId == fundId);
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetIncomeAccountsAsync(int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.AccountType)
            .Where(c => c.FundId == fundId && c.AccountType!.TypeName == "Income")
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetExpenseAccountsAsync(int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.AccountType)
            .Where(c => c.FundId == fundId && c.AccountType!.TypeName == "Expense")
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetHierarchyAsync(int fundId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Where(c => c.FundId == fundId)
            .Include(c => c.AccountType)
            .Include(c => c.ParentAccount)
            .Include(c => c.ChildAccounts)
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetChildrenAsync(int parentAccountId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Where(c => c.ParentAccountId == parentAccountId)
            .Include(c => c.AccountType)
            .AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<ChartOfAccountEntry>> GetWithRelatedAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .Include(c => c.AccountType)
            .Include(c => c.ParentAccount)
            .AsNoTracking()
            .OrderBy(c => c.Fund!.FundCode)
            .ThenBy(c => c.AccountNumber)
            .ToListAsync();
    }

    public async Task<IDictionary<string, int>> GetAccountCountsByFundAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ChartOfAccounts
            .Include(c => c.Fund)
            .GroupBy(c => c.Fund!.FundCode)
            .Select(g => new { FundCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FundCode, x => x.Count);
    }

    private void InvalidateCache()
    {
        _cache.Remove(CacheKeyAll);
    }
}
