using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Models.DTOs;
using WileyWidget.Business.Interfaces;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Enterprise data operations
/// </summary>
public class EnterpriseRepository : IEnterpriseRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<EnterpriseRepository> _logger;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<EnterpriseRepository> logger, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger.LogInformation("EnterpriseRepository constructed and DB factory injected");
    }

    /// <summary>
    /// Gets all enterprises
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetAllAsync()
    {
        const string cacheKey = "Enterprises_All";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<Enterprise>? enterprises))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            enterprises = await context.Enterprises
                .Where(e => !e.IsDeleted)
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToListAsync();

            // Set cache with proper size specification (required when SizeLimit is configured)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                Size = 1 // Logical size unit for cache eviction
            };
            _cache.Set(cacheKey, enterprises, cacheOptions);
        }

        return enterprises!;
    }

    /// <summary>
    /// Gets paged enterprises with sorting support
    /// </summary>
    public async Task<(IEnumerable<Enterprise> Items, int TotalCount)> GetPagedAsync(
        int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Enterprises.Where(e => !e.IsDeleted).AsQueryable();

        // Apply sorting
        query = ApplySorting(query, sortBy, sortDescending);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply paging
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Gets an IQueryable for flexible querying and paging
    /// </summary>
    public async Task<IQueryable<Enterprise>> GetQueryableAsync()
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return context.Enterprises.Where(e => !e.IsDeleted).AsQueryable();
    }

    /// <summary>
    /// Gets all enterprises including soft-deleted ones
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetAllIncludingDeletedAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    public async Task<Enterprise?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    /// <summary>
    /// Gets enterprises by type
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetByTypeAsync(string type)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .Where(e => !e.IsDeleted && e.Type == type)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    public async Task<Enterprise> AddAsync(Enterprise enterprise)
    {
        var context = await _contextFactory.CreateDbContextAsync();
        context.Enterprises.Add(enterprise);
        await context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Updates an enterprise
    /// </summary>
    public async Task<Enterprise> UpdateAsync(Enterprise enterprise)
    {
        ArgumentNullException.ThrowIfNull(enterprise);

        var context = await _contextFactory.CreateDbContextAsync();

        // Set audit fields
        enterprise.ModifiedDate = DateTime.UtcNow;
        enterprise.ModifiedBy = enterprise.ModifiedBy ?? "System";

        context.Enterprises.Update(enterprise);
        try
        {
            await context.SaveChangesAsync();
            return enterprise;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var databaseValues = await entry.GetDatabaseValuesAsync();
            var clientValues = entry.CurrentValues;
            throw new ConcurrencyConflictException(
                "Enterprise",
                ConcurrencyConflictException.ToDictionary(databaseValues),
                ConcurrencyConflictException.ToDictionary(clientValues),
                ex);
        }
    }

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var enterprise = await context.Enterprises.FindAsync(id);
        if (enterprise == null)
            return false;

        context.Enterprises.Remove(enterprise);
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var databaseValues = await entry.GetDatabaseValuesAsync();
            var clientValues = entry.CurrentValues;
            throw new ConcurrencyConflictException(
                "Enterprise",
                ConcurrencyConflictException.ToDictionary(databaseValues),
                ConcurrencyConflictException.ToDictionary(clientValues),
                ex);
        }
    }

    /// <summary>
    /// Gets the total count of enterprises
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises.Where(e => !e.IsDeleted).CountAsync();
    }

    /// <summary>
    /// Creates an enterprise from header mapping
    /// </summary>
    public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap)
    {
        if (headerValueMap == null)
            throw new ArgumentNullException(nameof(headerValueMap));

        var enterprise = new Enterprise();

        foreach (var kvp in headerValueMap)
        {
            var key = kvp.Key.ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal);
            var value = kvp.Value?.Trim();

            switch (key)
            {
                case "name":
                case "enterprisename":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Name = value;
                    break;
                case "description":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Description = value;
                    break;
                case "currentrate":
                case "rate":
                    if (decimal.TryParse(value, out var rate))
                        enterprise.CurrentRate = rate;
                    break;
                case "monthlyexpenses":
                case "expenses":
                    if (decimal.TryParse(value, out var expenses))
                        enterprise.MonthlyExpenses = expenses;
                    break;
                case "citizencount":
                case "citizens":
                    if (int.TryParse(value, out var count))
                        enterprise.CitizenCount = count;
                    break;
                case "type":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Type = value;
                    break;
                case "notes":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Notes = value;
                    break;
                case "totalbudget":
                case "budget":
                    if (decimal.TryParse(value, out var budget))
                        enterprise.TotalBudget = budget;
                    break;
            }
        }

        return enterprise;
    }
    public async Task<Enterprise?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var lowerName = name.ToLower(CultureInfo.InvariantCulture);
        return await context.Enterprises
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Name != null && e.Name.ToLower(CultureInfo.InvariantCulture) == lowerName)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Checks if an enterprise exists by name
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Enterprises.AsQueryable();
        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
            _logger.LogDebug("Checking if enterprise exists by name '{Name}' excluding ID {ExcludeId}", name, excludeId);
        }
        else
        {
            _logger.LogDebug("Checking if enterprise exists by name '{Name}'", name);
        }
        var exists = await query.AnyAsync(e => e.Name.ToLower(CultureInfo.InvariantCulture) == name.ToLower(CultureInfo.InvariantCulture));
        _logger.LogDebug("Enterprise exists by name '{Name}': {Exists}", name, exists);
        return exists;
    }

    /// <summary>
    /// Soft deletes an enterprise
    /// </summary>
    public async Task<bool> SoftDeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var enterprise = await context.Enterprises.FindAsync(id);
        if (enterprise == null)
            return false;

        enterprise.IsDeleted = true;
        enterprise.DeletedDate = DateTime.UtcNow;
        enterprise.DeletedBy = Environment.UserName;
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets all enterprises with their budget interactions
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetWithInteractionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .Where(e => !e.IsDeleted)
            .Include(e => e.BudgetInteractions)
            .OrderBy(e => e.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets enterprise summaries
    /// </summary>
    public async Task<IEnumerable<EnterpriseSummary>> GetSummariesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .Where(e => !e.IsDeleted)
            .AsNoTracking()
            .Select(e => new EnterpriseSummary
            {
                Id = e.Id,
                Name = e.Name,
                CurrentRate = e.CurrentRate,
                CitizenCount = e.CitizenCount,
                MonthlyRevenue = e.CitizenCount * e.CurrentRate,
                MonthlyExpenses = e.MonthlyExpenses,
                MonthlyBalance = (e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses,
                Status = ((e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses) > 0 ? "Surplus" :
                         ((e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses) < 0 ? "Deficit" : "Break-even"
            })
            .ToListAsync();
    }

    /// <summary>
    /// Gets active enterprise summaries
    /// </summary>
    public async Task<IEnumerable<EnterpriseSummary>> GetActiveSummariesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .Where(e => !e.IsDeleted)
            .AsNoTracking()
            .Select(e => new EnterpriseSummary
            {
                Id = e.Id,
                Name = e.Name,
                CurrentRate = e.CurrentRate,
                CitizenCount = e.CitizenCount,
                MonthlyRevenue = e.CitizenCount * e.CurrentRate,
                MonthlyExpenses = e.MonthlyExpenses,
                MonthlyBalance = (e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses,
                Status = ((e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses) > 0 ? "Surplus" :
                         ((e.CitizenCount * e.CurrentRate) - e.MonthlyExpenses) < 0 ? "Deficit" : "Break-even"
            })
            .ToListAsync();
    }

    /// <summary>
    /// Restores a soft-deleted enterprise
    /// </summary>
    public async Task<bool> RestoreAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var enterprise = await context.Enterprises.FindAsync(id);
        if (enterprise == null)
            return false;

        enterprise.IsDeleted = false;
        enterprise.DeletedDate = null;
        enterprise.DeletedBy = null;
        await context.SaveChangesAsync();
        return true;
    }

    private IQueryable<Enterprise> ApplySorting(IQueryable<Enterprise> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return sortDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name);
        }

        return sortBy.ToLowerInvariant() switch
        {
            "name" => sortDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "currentrate" => sortDescending
                ? query.OrderByDescending(e => e.CurrentRate)
                : query.OrderBy(e => e.CurrentRate),
            "citizencount" => sortDescending
                ? query.OrderByDescending(e => e.CitizenCount)
                : query.OrderBy(e => e.CitizenCount),
            "monthlyexpenses" => sortDescending
                ? query.OrderByDescending(e => e.MonthlyExpenses)
                : query.OrderBy(e => e.MonthlyExpenses),
            "type" => sortDescending
                ? query.OrderByDescending(e => e.Type)
                : query.OrderBy(e => e.Type),
            _ => sortDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name)
        };
    }
}
