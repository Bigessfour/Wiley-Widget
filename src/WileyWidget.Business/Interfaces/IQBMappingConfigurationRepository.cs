using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for QBMappingConfiguration operations
/// </summary>
public interface IQBMappingConfigurationRepository
{
    /// <summary>
    /// Gets all mapping configurations
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetAllAsync();

    /// <summary>
    /// Gets mapping configuration by ID
    /// </summary>
    Task<QBMappingConfiguration?> GetByIdAsync(int id);

    /// <summary>
    /// Gets mappings by QuickBooks entity type and ID
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetByQBEntityAsync(string entityType, string entityId);

    /// <summary>
    /// Gets mappings by budget entry ID
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetByBudgetEntryIdAsync(int budgetEntryId);

    /// <summary>
    /// Adds a new mapping configuration
    /// </summary>
    Task AddAsync(QBMappingConfiguration mapping);

    /// <summary>
    /// Updates an existing mapping configuration
    /// </summary>
    Task UpdateAsync(QBMappingConfiguration mapping);

    /// <summary>
    /// Deletes a mapping configuration
    /// </summary>
    Task DeleteAsync(int id);
}
