using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Intuit.Ipp.Data;

namespace WileyWidget.Services
{
    /// <summary>
    /// Abstraction for QuickBooks API client operations to enable testing
    /// </summary>
    public interface IQuickBooksApiClient
    {
        Task<List<Customer>> GetCustomersAsync();
        Task<List<Invoice>> GetInvoicesAsync();
        Task<List<Account>> GetChartOfAccountsAsync();
        Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate);
        // TODO: Re-enable when Budget type is available or custom implementation is created
        // Task<List<Budget>> GetBudgetsAsync();
        Task<bool> TestConnectionAsync();
    }
}
