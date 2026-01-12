using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using QBTask = Intuit.Ipp.Data.Task;

namespace WileyWidget.Services.Abstractions
{
    public interface IQuickBooksService
    {
        System.Threading.Tasks.Task<bool> AuthorizeAsync();

        /// <summary>
        /// Tests the QuickBooks API connection
        /// </summary>
        /// <returns>True if the connection test succeeds, false otherwise</returns>
        System.Threading.Tasks.Task<bool> TestConnectionAsync();

        /// <summary>
        /// Checks if the service is currently connected
        /// </summary>
        /// <returns>True if connected, false otherwise</returns>
        System.Threading.Tasks.Task<bool> IsConnectedAsync();

        System.Threading.Tasks.Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null);
        System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync();
        System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null);
        System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync();
        System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        System.Threading.Tasks.Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync();
        System.Threading.Tasks.Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<WileyWidget.Models.QuickBooksBudget> budgets, CancellationToken cancellationToken = default);

        /// <summary>
        /// Query expense lines for a department between the given dates.
        /// Implementations should return a collection of expense lines containing at least an Amount property.
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<ExpenseLine>> QueryExpensesByDepartmentAsync(
            string departmentName,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Connects to the QuickBooks API
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>True if the connection succeeds, false otherwise</returns>
        System.Threading.Tasks.Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default);

    }

    /// <summary>
    /// Simple expense line returned from QuickBooks (abstraction level).
    /// </summary>
    /// <param name="Amount">The monetary amount of the expense line.</param>
    /// <param name="Date">The date the expense occurred.</param>
    /// <param name="Description">Optional description or memo for the expense.</param>
    /// <param name="Department">The department or class associated with the expense.</param>
    public sealed record ExpenseLine(
        decimal Amount,
        DateTime? Date = null,
        string? Description = null,
        string? Department = null);

    public sealed class UrlAclCheckResult
    {
        public bool IsReady { get; set; }
        public string ListenerPrefix { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string Guidance { get; set; } = string.Empty;
        public string? RawNetshOutput { get; set; }
    }

    public sealed class ConnectionStatus
    {
        public bool IsConnected { get; set; }
        public string? CompanyName { get; set; }
        public string? LastSyncTime { get; set; }
        public string? StatusMessage { get; set; }
    }

    public sealed class SyncResult
    {
        public bool Success { get; set; }
        public int RecordsSynced { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public sealed class ImportResult
    {
        public bool Success { get; set; }
        public int AccountsImported { get; set; }
        public int AccountsUpdated { get; set; }
        public int AccountsSkipped { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string>? ValidationErrors { get; set; }
    }
}
