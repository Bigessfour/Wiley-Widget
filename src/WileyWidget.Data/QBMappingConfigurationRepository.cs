#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.Models;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for QBMappingConfiguration data operations
/// </summary>
public class QBMappingConfigurationRepository : IQBMappingConfigurationRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ITelemetryService? _telemetryService;

    // Activity source for repository-level telemetry
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.QBMappingConfigurationRepository");

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public QBMappingConfigurationRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        IMemoryCache cache,
        ITelemetryService? telemetryService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Safely attempts to get a value from cache, handling disposed cache gracefully
    /// </summary>
    private bool TryGetFromCache<T>(string key, out T? value)
    {
        try
        {
            return _cache.TryGetValue(key, out value);
        }
        catch (ObjectDisposedException)
        {
            value = default;
            return false;
        }
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QBMappingConfigurations
            .Include(m => m.BudgetEntry)
            .OrderBy(m => m.QBEntityType)
            .ThenBy(m => m.QBEntityName)
            .ToListAsync();
    }

    public async Task<QBMappingConfiguration?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QBMappingConfigurations
            .Include(m => m.BudgetEntry)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetByQBEntityAsync(string entityType, string entityId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QBMappingConfigurations
            .Include(m => m.BudgetEntry)
            .Where(m => m.QBEntityType == entityType && m.QBEntityId == entityId && m.IsActive)
            .OrderByDescending(m => m.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<QBMappingConfiguration>> GetByBudgetEntryIdAsync(int budgetEntryId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QBMappingConfigurations
            .Include(m => m.BudgetEntry)
            .Where(m => m.BudgetEntryId == budgetEntryId && m.IsActive)
            .OrderBy(m => m.QBEntityType)
            .ThenBy(m => m.QBEntityName)
            .ToListAsync();
    }

    public async Task AddAsync(QBMappingConfiguration mapping)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.QBMappingConfigurations.Add(mapping);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(QBMappingConfiguration mapping)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.QBMappingConfigurations.Update(mapping);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var mapping = await context.QBMappingConfigurations.FindAsync(id);
        if (mapping != null)
        {
            context.QBMappingConfigurations.Remove(mapping);
            await context.SaveChangesAsync();
        }
    }
}
