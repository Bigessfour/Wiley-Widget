using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for Enterprise data operations
/// </summary>
public interface IEnterpriseRepository
{
    /// <summary>
    /// Gets all enterprises
    /// </summary>
    Task<IEnumerable<Enterprise>> GetAllAsync();

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    Task<Enterprise> GetByIdAsync(int id);

    /// <summary>
    /// Gets an enterprise by name
    /// </summary>
    Task<Enterprise> GetByNameAsync(string name);

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    Task<Enterprise> AddAsync(Enterprise enterprise);

    /// <summary>
    /// Updates an existing enterprise
    /// </summary>
    Task<Enterprise> UpdateAsync(Enterprise enterprise);

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if an enterprise exists by name
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null);

    /// <summary>
    /// Gets the total number of enterprises
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets enterprises with their budget interactions
    /// </summary>
    Task<IEnumerable<Enterprise>> GetWithInteractionsAsync();
}
