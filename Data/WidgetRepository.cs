#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Widget operations
/// Provides data access functionality using Entity Framework Core
/// </summary>
public class WidgetRepository : IWidgetRepository
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public WidgetRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets all widgets asynchronously
    /// </summary>
    public async Task<IEnumerable<Widget>> GetAllAsync()
    {
        return await _context.Widgets
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active widgets asynchronously
    /// </summary>
    public async Task<IEnumerable<Widget>> GetActiveAsync()
    {
        return await _context.Widgets
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a widget by ID asynchronously
    /// </summary>
    public async Task<Widget?> GetByIdAsync(int id)
    {
        return await _context.Widgets.FindAsync(id);
    }

    /// <summary>
    /// Gets widgets by category asynchronously
    /// </summary>
    public async Task<IEnumerable<Widget>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return await GetAllAsync();
        }

        return await _context.Widgets
            .Where(w => w.Category.Contains(category))
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Searches widgets by name or description asynchronously
    /// </summary>
    public async Task<IEnumerable<Widget>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        return await _context.Widgets
            .Where(w => w.Name.Contains(searchTerm) ||
                       w.Description.Contains(searchTerm) ||
                       w.SKU.Contains(searchTerm))
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new widget asynchronously
    /// </summary>
    public async Task<Widget> AddAsync(Widget widget)
    {
        if (widget == null)
        {
            throw new ArgumentNullException(nameof(widget));
        }

        _context.Widgets.Add(widget);
        await _context.SaveChangesAsync();
        return widget;
    }

    /// <summary>
    /// Updates an existing widget asynchronously
    /// </summary>
    public async Task UpdateAsync(Widget widget)
    {
        if (widget == null)
        {
            throw new ArgumentNullException(nameof(widget));
        }

        _context.Entry(widget).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a widget by ID asynchronously
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var widget = await GetByIdAsync(id);
        if (widget == null)
        {
            return false;
        }

        _context.Widgets.Remove(widget);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a widget exists by ID asynchronously
    /// </summary>
    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Widgets.AnyAsync(w => w.Id == id);
    }

    /// <summary>
    /// Gets the total count of widgets asynchronously
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        return await _context.Widgets.CountAsync();
    }

    /// <summary>
    /// Gets widgets with pagination asynchronously
    /// </summary>
    public async Task<(IEnumerable<Widget> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var totalCount = await GetCountAsync();
        var items = await _context.Widgets
            .OrderBy(w => w.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
