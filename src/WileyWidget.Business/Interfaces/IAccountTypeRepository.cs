#nullable enable

using WileyWidget.Models.Entities;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for AccountTypeEntity entities.
/// Defines data access operations for chart of accounts type classifications.
/// </summary>
public interface IAccountTypeRepository
{
    /// <summary>
    /// Gets all account types.
    /// </summary>
    Task<IEnumerable<AccountTypeEntity>> GetAllAsync();

    /// <summary>
    /// Gets an account type by ID.
    /// </summary>
    Task<AccountTypeEntity?> GetByIdAsync(int id);

    /// <summary>
    /// Gets an account type by name.
    /// </summary>
    Task<AccountTypeEntity?> GetByNameAsync(string typeName);

    /// <summary>
    /// Adds a new account type.
    /// </summary>
    Task AddAsync(AccountTypeEntity accountType);

    /// <summary>
    /// Adds multiple account types.
    /// </summary>
    Task AddRangeAsync(IEnumerable<AccountTypeEntity> accountTypes);

    /// <summary>
    /// Updates an existing account type.
    /// </summary>
    Task UpdateAsync(AccountTypeEntity accountType);

    /// <summary>
    /// Deletes an account type by ID. Returns true when an entity was removed.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks existence of an account type by name.
    /// </summary>
    Task<bool> ExistsByNameAsync(string typeName);

    /// <summary>
    /// Gets all debit-type accounts (Asset, Expense).
    /// </summary>
    Task<IEnumerable<AccountTypeEntity>> GetDebitTypesAsync();

    /// <summary>
    /// Gets all credit-type accounts (Liability, Equity, Income).
    /// </summary>
    Task<IEnumerable<AccountTypeEntity>> GetCreditTypesAsync();
}
