#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Fund data operations
/// </summary>
public class FundRepository : IFundRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private const string CacheKeyAll = "Funds_All";
    private const string CacheKeyActive = "Funds_Active";

    public FundRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IEnumerable<Fund>> GetAllAsync()
    {
        if (!_cache.TryGetValue(CacheKeyAll, out IEnumerable<Fund>? funds))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            funds = await context.Funds
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .ToListAsync();

            _cache.Set(CacheKeyAll, funds, TimeSpan.FromMinutes(15));
        }

        return funds!;
    }

    public async Task<IEnumerable<Fund>> GetActiveAsync()
    {
        if (!_cache.TryGetValue(CacheKeyActive, out IEnumerable<Fund>? funds))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            funds = await context.Funds
                .Where(f => f.IsActive)
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .ToListAsync();

            _cache.Set(CacheKeyActive, funds, TimeSpan.FromMinutes(15));
        }

        return funds!;
    }

    public async Task<Fund?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Funds
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Fund?> GetByCodeAsync(string fundCode)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Funds
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FundCode == fundCode);
    }

    public async Task AddAsync(Fund fund)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        fund.CreatedAt = DateTime.UtcNow;
        await context.Funds.AddAsync(fund);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task AddRangeAsync(IEnumerable<Fund> funds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var fund in funds)
        {
            fund.CreatedAt = now;
        }
        await context.Funds.AddRangeAsync(funds);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task UpdateAsync(Fund fund)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        fund.UpdatedAt = DateTime.UtcNow;
        context.Funds.Update(fund);
        await context.SaveChangesAsync();
        InvalidateCache();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var fund = await context.Funds.FindAsync(id);
        if (fund == null)
            return false;

        context.Funds.Remove(fund);
        await context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool> ExistsByCodeAsync(string fundCode)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Funds.AnyAsync(f => f.FundCode == fundCode);
    }

    public async Task<IEnumerable<Fund>> GetByTypeAsync(FundType fundType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Funds
            .Where(f => f.Type == fundType)
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    private void InvalidateCache()
    {
        _cache.Remove(CacheKeyAll);
        _cache.Remove(CacheKeyActive);
    }
}
