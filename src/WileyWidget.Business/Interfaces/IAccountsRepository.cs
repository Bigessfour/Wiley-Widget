using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for municipal account data operations.
/// Provides abstraction over data access for accounts, enabling testability and separation of concerns.
/// </summary>
public interface IAccountsRepository
{
    /// <summary>
    /// Retrieves all municipal accounts from the data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of all municipal accounts.</returns>
    Task<IReadOnlyList<MunicipalAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves municipal accounts filtered by fund type.
    /// </summary>
    /// <param name="fundType">The fund type to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of accounts matching the specified fund type.</returns>
    Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAsync(
        MunicipalFundType fundType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves municipal accounts filtered by account type.
    /// </summary>
    /// <param name="accountType">The account type to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of accounts matching the specified account type.</returns>
    Task<IReadOnlyList<MunicipalAccount>> GetAccountsByTypeAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves municipal accounts filtered by both fund type and account type.
    /// </summary>
    /// <param name="fundType">The fund type to filter by.</param>
    /// <param name="accountType">The account type to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of accounts matching both filter criteria.</returns>
    Task<IReadOnlyList<MunicipalAccount>> GetAccountsByFundAndTypeAsync(
        MunicipalFundType fundType,
        AccountType accountType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single municipal account by its unique identifier.
    /// </summary>
    /// <param name="accountId">The unique identifier of the account.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The account if found; otherwise null.</returns>
    Task<MunicipalAccount?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for municipal accounts matching the specified search term.
    /// Searches across account number, name, and description fields.
    /// </summary>
    /// <param name="searchTerm">The term to search for.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of accounts matching the search criteria.</returns>
    Task<IReadOnlyList<MunicipalAccount>> SearchAccountsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);
}
