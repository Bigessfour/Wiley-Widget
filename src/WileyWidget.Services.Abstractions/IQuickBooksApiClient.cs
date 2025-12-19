using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Intuit.Ipp.Data;

namespace WileyWidget.Services.Abstractions
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
        Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync();
        Task<bool> TestConnectionAsync();
    }
}
