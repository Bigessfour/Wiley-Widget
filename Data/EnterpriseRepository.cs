using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System.Globalization;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Enterprise data operations
/// </summary>
public class EnterpriseRepository : IEnterpriseRepository
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets all enterprises
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetAllAsync()
    {
        return await _context.Enterprises
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    public async Task<Enterprise> GetByIdAsync(int id)
    {
        return await _context.Enterprises
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    /// Gets an enterprise by name
    /// </summary>
    public async Task<Enterprise> GetByNameAsync(string name)
    {
        return await _context.Enterprises
            .FirstOrDefaultAsync(e => e.Name.ToLower(CultureInfo.InvariantCulture) == name.ToLower(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    public async Task<Enterprise> AddAsync(Enterprise enterprise)
    {
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Updates an existing enterprise
    /// </summary>
    public async Task<Enterprise> UpdateAsync(Enterprise enterprise)
    {
        _context.Enterprises.Update(enterprise);
        await _context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var enterprise = await GetByIdAsync(id);
        if (enterprise == null)
            return false;

        _context.Enterprises.Remove(enterprise);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if an enterprise exists by name
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        var query = _context.Enterprises.Where(e => e.Name.ToLower(CultureInfo.InvariantCulture) == name.ToLower(CultureInfo.InvariantCulture));

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of enterprises
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        return await _context.Enterprises.CountAsync();
    }

    /// <summary>
    /// Gets enterprises with their budget interactions
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetWithInteractionsAsync()
    {
        return await _context.Enterprises
            .Include(e => e.BudgetInteractions)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}
