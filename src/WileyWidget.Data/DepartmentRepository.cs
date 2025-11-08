#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Department data operations
/// </summary>
public class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public DepartmentRepository(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Gets all departments
    /// </summary>
    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        const string cacheKey = "Departments_All";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<Department>? departments))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            departments = await context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();

            _cache.Set(cacheKey, departments, TimeSpan.FromMinutes(15));
        }

        return departments!;
    }

    /// <summary>
    /// Gets paged departments with sorting support
    /// </summary>
    public async Task<(IEnumerable<Department> Items, int TotalCount)> GetPagedAsync(
        int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Departments.AsQueryable();

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
    public async Task<IQueryable<Department>> GetQueryableAsync()
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return context.Departments.AsQueryable();
    }

    /// <summary>
    /// Gets a department by ID
    /// </summary>
    public async Task<Department?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>
    /// Gets a department by name
    /// </summary>
    public async Task<Department?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Department code cannot be null or empty", nameof(code));

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentCode == code);
    }

    /// <summary>
    /// Adds a new department
    /// </summary>
    public async Task AddAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Departments.Add(department);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing department
    /// </summary>
    public async Task UpdateAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Departments.Update(department);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a department by ID
    /// Returns true when an entity was removed, false when not found
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var department = await context.Departments.FindAsync(id);
        if (department != null)
        {
            context.Departments.Remove(department);
            await context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks existence of a department by code
    /// </summary>
    public async Task<bool> ExistsByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments.AnyAsync(d => d.DepartmentCode == code);
    }

    /// <summary>
    /// Gets departments that have no parent (root nodes)
    /// </summary>
    public async Task<IEnumerable<Department>> GetRootDepartmentsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Where(d => d.ParentId == null)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets children departments for a given parent id
    /// </summary>
    public async Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Where(d => d.ParentId == parentId)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    private IQueryable<Department> ApplySorting(IQueryable<Department> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return sortDescending
                ? query.OrderByDescending(d => d.Name)
                : query.OrderBy(d => d.Name);
        }

        return sortBy.ToLowerInvariant() switch
        {
            "name" => sortDescending
                ? query.OrderByDescending(d => d.Name)
                : query.OrderBy(d => d.Name),
            "departmentcode" => sortDescending
                ? query.OrderByDescending(d => d.DepartmentCode)
                : query.OrderBy(d => d.DepartmentCode),
            _ => sortDescending
                ? query.OrderByDescending(d => d.Name)
                : query.OrderBy(d => d.Name)
        };
    }
}
