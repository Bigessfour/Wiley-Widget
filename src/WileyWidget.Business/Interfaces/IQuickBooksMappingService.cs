using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Service for managing QuickBooks to WileyWidget mapping configurations
/// </summary>
public interface IQuickBooksMappingService
{
    /// <summary>
    /// Gets all active mapping configurations
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetAllMappingsAsync();

    /// <summary>
    /// Gets mapping configuration by ID
    /// </summary>
    Task<QBMappingConfiguration?> GetMappingByIdAsync(int id);

    /// <summary>
    /// Gets mappings for a specific QuickBooks entity type and ID
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetMappingsByQBEntityAsync(string entityType, string entityId);

    /// <summary>
    /// Gets the budget entry ID for a given QuickBooks entity
    /// </summary>
    Task<int?> GetBudgetEntryIdForQBEntityAsync(string entityType, string entityId);

    /// <summary>
    /// Attempts to resolve a BudgetEntryId for a QuickBooks Invoice using configured mappings and strategies.
    /// Implementations may inspect CustomField, ClassRef, DepartmentRef, CustomerRef, ItemRef, and RuleBased mappings.
    /// </summary>
    Task<int?> ResolveBudgetEntryIdForInvoiceAsync(Intuit.Ipp.Data.Invoice invoice);

    /// <summary>
    /// Creates a new mapping configuration
    /// </summary>
    Task<QBMappingConfiguration> CreateMappingAsync(QBMappingConfiguration mapping);

    /// <summary>
    /// Updates an existing mapping configuration
    /// </summary>
    Task UpdateMappingAsync(QBMappingConfiguration mapping);

    /// <summary>
    /// Deletes a mapping configuration
    /// </summary>
    Task DeleteMappingAsync(int id);

    /// <summary>
    /// Gets mappings for a specific budget entry
    /// </summary>
    Task<IEnumerable<QBMappingConfiguration>> GetMappingsByBudgetEntryAsync(int budgetEntryId);

    /// <summary>
    /// Validates if a mapping configuration is valid
    /// </summary>
    Task<bool> ValidateMappingAsync(QBMappingConfiguration mapping);
}
