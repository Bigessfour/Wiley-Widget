#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for AccountTypeEntity data operations
/// </summary>
public class AccountTypeRepository : IAccountTypeRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private const string CacheKeyAll = "AccountTypes_All";

    public AccountTypeRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IEnumerable<AccountTypeEntity>> GetAllAsync()
    {
        if (!_cache.TryGetValue(CacheKeyAll, out IEnumerable<AccountTypeEntity>? accountTypes))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            accountTypes = await context.AccountTypes
                .AsNoTracking()
                .OrderBy(at => at.TypeName)
                .ToListAsync();

            _cache.Set(CacheKeyAll, accountTypes, TimeSpan.FromMinutes(30));
        }

        return accountTypes!;
    }

    public async Task<AccountTypeEntity?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(at => at.Id == id);
    }

    public async Task<AccountTypeEntity?> GetByNameAsync(string typeName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(at => at.TypeName == typeName);
    }

    public async Task AddAsync(AccountTypeEntity accountType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        accountType.CreatedAt = DateTime.UtcNow;
        await context.AccountTypes.AddAsync(accountType);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task AddRangeAsync(IEnumerable<AccountTypeEntity> accountTypes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var accountType in accountTypes)
        {
            accountType.CreatedAt = now;
        }
        await context.AccountTypes.AddRangeAsync(accountTypes);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task UpdateAsync(AccountTypeEntity accountType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        accountType.UpdatedAt = DateTime.UtcNow;
        context.AccountTypes.Update(accountType);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var accountType = await context.AccountTypes.FindAsync(id);
        if (accountType == null)
            return false;

        context.AccountTypes.Remove(accountType);
        await context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool> ExistsByNameAsync(string typeName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountTypes.AnyAsync(at => at.TypeName == typeName);
    }

    public async Task<IEnumerable<AccountTypeEntity>> GetDebitTypesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountTypes
            .Where(at => at.IsDebit)
            .AsNoTracking()
            .OrderBy(at => at.TypeName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountTypeEntity>> GetCreditTypesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountTypes
            .Where(at => !at.IsDebit)
            .AsNoTracking()
            .OrderBy(at => at.TypeName)
            .ToListAsync();
    }

    private void InvalidateCache()
    {
        _cache.Remove(CacheKeyAll);
    }
}
