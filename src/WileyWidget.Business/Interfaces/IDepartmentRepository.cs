#nullable enable

using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for Department entities.
/// Defines data access operations for municipal departments.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Gets all departments.
    /// </summary>
    Task<IEnumerable<Department>> GetAllAsync();

    /// <summary>
    /// Gets a department by ID.
    /// </summary>
    Task<Department?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a department by code.
    /// </summary>
    Task<Department?> GetByCodeAsync(string code);

    /// <summary>
    /// Adds a new department.
    /// </summary>
    Task AddAsync(Department department);

    /// <summary>
    /// Updates an existing department.
    /// </summary>
    Task UpdateAsync(Department department);

    /// <summary>
    /// Deletes a department by ID. Returns true when an entity was removed.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks existence of a department by code.
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code);

    /// <summary>
    /// Gets departments that have no parent (root nodes)
    /// </summary>
    Task<IEnumerable<Department>> GetRootDepartmentsAsync();

    /// <summary>
    /// Gets children departments for a given parent id
    /// </summary>
    Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentId);

    /// <summary>
    /// Gets paged departments with sorting support.
    /// </summary>
    Task<(IEnumerable<Department> Items, int TotalCount)> GetPagedAsync(int pageNumber = 1, int pageSize = 50, string? sortBy = null, bool sortDescending = false);
}
