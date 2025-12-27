using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using QBTask = Intuit.Ipp.Data.Task;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Represents a interface for iquickbooksservice.
    /// </summary>
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
        System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<List<Bill>> GetBillsAsync();
        System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null);
        System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync();
        System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        System.Threading.Tasks.Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync();
        System.Threading.Tasks.Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<WileyWidget.Models.QuickBooksBudget> budgets, CancellationToken cancellationToken = default);

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

    public sealed class UrlAclCheckResult
    {
        /// <summary>
        /// Gets or sets the isready.
        /// </summary>
        public bool IsReady { get; set; }
        /// <summary>
        /// Gets or sets the listenerprefix.
        /// </summary>
        public string ListenerPrefix { get; set; } = string.Empty;
        public string? Owner { get; set; }
        /// <summary>
        /// Gets or sets the guidance.
        /// </summary>
        public string Guidance { get; set; } = string.Empty;
        public string? RawNetshOutput { get; set; }
    }

    public sealed class ConnectionStatus
    {
        /// <summary>
        /// Gets or sets the isconnected.
        /// </summary>
        public bool IsConnected { get; set; }
        public string? CompanyName { get; set; }
        public string? LastSyncTime { get; set; }
        public string? StatusMessage { get; set; }
    }

    public sealed class SyncResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the recordssynced.
        /// </summary>
        public int RecordsSynced { get; set; }
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the duration.
        /// </summary>
        /// <summary>
        /// Gets or sets the duration.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    public sealed class ImportResult
    {
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the accountsimported.
        /// </summary>
        public int AccountsImported { get; set; }
        /// <summary>
        /// Gets or sets the accountsupdated.
        /// </summary>
        public int AccountsUpdated { get; set; }
        /// <summary>
        /// Gets or sets the accountsskipped.
        /// </summary>
        public int AccountsSkipped { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string>? ValidationErrors { get; set; }
    }
}
