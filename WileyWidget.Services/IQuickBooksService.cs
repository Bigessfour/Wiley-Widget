using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for QuickBooks integration operations
    /// </summary>
    public interface IQuickBooksService
    {
        // Initiates the interactive OAuth flow to authorize the application with QuickBooks
        Task<bool> AuthorizeAsync();
        Task<bool> TestConnectionAsync();
        // Check whether the local HTTP listener URL ACL is configured for the redirect URI
        Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null);
        Task<List<Customer>> GetCustomersAsync();
        Task<List<Invoice>> GetInvoicesAsync(string enterprise = null);
        Task<List<Account>> GetChartOfAccountsAsync();
        Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        Task<List<Budget>> GetBudgetsAsync();

        // Data synchronization with cancellation support
        System.Threading.Tasks.Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

        // Chart of accounts import with cancellation support
        System.Threading.Tasks.Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default);

        // Data synchronization with cancellation support
        System.Threading.Tasks.Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of URL ACL readiness check
    /// </summary>
    public sealed class UrlAclCheckResult
    {
        public bool IsReady { get; set; }
        public string ListenerPrefix { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string Guidance { get; set; } = string.Empty;
        public string? RawNetshOutput { get; set; }
    }

    /// <summary>
    /// Connection status for QuickBooks service
    /// </summary>
    public sealed class ConnectionStatus
    {
        public bool IsConnected { get; set; }
        public string? CompanyName { get; set; }
        public string? LastSyncTime { get; set; }
        public string? StatusMessage { get; set; }
    }

    /// <summary>
    /// Result of data synchronization operation
    /// </summary>
    public sealed class SyncResult
    {
        public bool Success { get; set; }
        public int RecordsSynced { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Result of chart of accounts import operation
    /// </summary>
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
