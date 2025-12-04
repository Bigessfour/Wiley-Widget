using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions.Models;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service for managing municipal account operations.
    /// Handles CRUD operations, filtering, validation, and data mapping.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Loads accounts with optional filtering by fund type and account type.
        /// Returns display DTOs for UI binding.
        /// </summary>
        /// <param name="fundType">Optional fund type filter</param>
        /// <param name="accountType">Optional account type filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of account display DTOs with summary metrics</returns>
        Task<AccountsResult> LoadAccountsAsync(
            MunicipalFundType? fundType = null,
            AccountType? accountType = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a municipal account using FluentValidation.
        /// </summary>
        /// <param name="account">Account to validate</param>
        /// <returns>Collection of validation error messages (empty if valid)</returns>
        IEnumerable<string> ValidateAccount(MunicipalAccount account);

        /// <summary>
        /// Saves a municipal account (create or update).
        /// Validates before saving.
        /// </summary>
        /// <param name="account">Account to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result indicating success or validation errors</returns>
        Task<SaveResult> SaveAccountAsync(MunicipalAccount account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes an account by setting IsActive to false.
        /// </summary>
        /// <param name="id">Account ID to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteAccountAsync(int id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of loading accounts operation.
    /// </summary>
    public record AccountsResult(
        IReadOnlyList<MunicipalAccountDisplay> Accounts,
        decimal TotalBalance,
        int ActiveAccountCount);

    /// <summary>
    /// Result of save operation.
    /// </summary>
    public record SaveResult(
        bool Success,
        IReadOnlyList<string> ValidationErrors);
}
