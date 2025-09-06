using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Interface for QuickBooks service operations.
/// </summary>
public interface IQuickBooksService : IDisposable
{
    /// <summary>
    /// Checks if the current access token is valid.
    /// </summary>
    bool HasValidAccessToken();

    /// <summary>
    /// Refreshes the access token if needed.
    /// </summary>
    Task RefreshTokenIfNeededAsync();

    /// <summary>
    /// Forces a refresh of the access token.
    /// </summary>
    Task RefreshTokenAsync();

    /// <summary>
    /// Gets all customers from QuickBooks.
    /// </summary>
    Task<List<QboCustomer>> GetCustomersAsync();

    /// <summary>
    /// Gets invoices from QuickBooks, optionally filtered by enterprise.
    /// </summary>
    Task<List<QboInvoice>> GetInvoicesAsync(string enterprise = null);

    /// <summary>
    /// Syncs an enterprise to a QuickBooks class.
    /// </summary>
    Task<string> SyncEnterpriseToQboClassAsync(Enterprise enterprise);

    /// <summary>
    /// Syncs a budget interaction to a QuickBooks account.
    /// </summary>
    Task<string> SyncBudgetInteractionToQboAccountAsync(BudgetInteraction interaction, string classId);
}
