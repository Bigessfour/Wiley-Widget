using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using QBTask = Intuit.Ipp.Data.Task;

namespace WileyWidget.Services
{
    public interface IQuickBooksService
    {
        System.Threading.Tasks.Task<bool> AuthorizeAsync();
        System.Threading.Tasks.Task<bool> TestConnectionAsync();
        System.Threading.Tasks.Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null);
        System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync();
        System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null);
        System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync();
        System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        System.Threading.Tasks.Task<List<Budget>> GetBudgetsAsync();
        System.Threading.Tasks.Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<Budget> budgets, CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default);
        System.Threading.Tasks.Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default);
    }

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
