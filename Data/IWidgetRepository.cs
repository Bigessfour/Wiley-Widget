#nullable enable

using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for Widget operations
/// Defines the contract for data access operations
/// </summary>
public interface IWidgetRepository
{
    /// <summary>
    /// Gets all widgets asynchronously
    /// </summary>
    Task<IEnumerable<Widget>> GetAllAsync();

    /// <summary>
    /// Gets all active widgets asynchronously
    /// </summary>
    Task<IEnumerable<Widget>> GetActiveAsync();

    /// <summary>
    /// Gets a widget by ID asynchronously
    /// </summary>
    Task<Widget?> GetByIdAsync(int id);

    /// <summary>
    /// Gets widgets by category asynchronously
    /// </summary>
    Task<IEnumerable<Widget>> GetByCategoryAsync(string category);

    /// <summary>
    /// Searches widgets by name or description asynchronously
    /// </summary>
    Task<IEnumerable<Widget>> SearchAsync(string searchTerm);

    /// <summary>
    /// Adds a new widget asynchronously
    /// </summary>
    Task<Widget> AddAsync(Widget widget);

    /// <summary>
    /// Updates an existing widget asynchronously
    /// </summary>
    Task UpdateAsync(Widget widget);

    /// <summary>
    /// Deletes a widget by ID asynchronously
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if a widget exists by ID asynchronously
    /// </summary>
    Task<bool> ExistsAsync(int id);

    /// <summary>
    /// Gets the total count of widgets asynchronously
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets widgets with pagination asynchronously
    /// </summary>
    Task<(IEnumerable<Widget> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize);
}
