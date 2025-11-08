using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using WileyWidget.Services;
using Intuit.Ipp.Data;

namespace WileyWidget.Fakes;

public class FakeQuickBooksService : IQuickBooksService
{
    public System.Threading.Tasks.Task<bool> AuthorizeAsync()
    {
        return System.Threading.Tasks.Task.FromResult(true);
    }

    public System.Threading.Tasks.Task<bool> TestConnectionAsync()
    {
        return System.Threading.Tasks.Task.FromResult(true);
    }

    public System.Threading.Tasks.Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null)
    {
        return System.Threading.Tasks.Task.FromResult(new UrlAclCheckResult { IsReady = true });
    }

    public System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync()
    {
        return System.Threading.Tasks.Task.FromResult(new List<Customer>());
    }

    public System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null)
    {
        return System.Threading.Tasks.Task.FromResult(new List<Invoice>());
    }

    public Task<List<Account>> GetChartOfAccountsAsync()
    {
        return Task.FromResult(new List<Account>());
    }

    public Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
    {
        return Task.FromResult(new List<JournalEntry>());
    }

    public Task<List<Budget>> GetBudgetsAsync()
    {
        return Task.FromResult(new List<Budget>());
    }

    public Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<Budget> budgets, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SyncResult { Success = true });
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConnectionStatus { IsConnected = true });
    }

    public Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ImportResult { Success = true });
    }

    public Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SyncResult { Success = true });
    }
}
