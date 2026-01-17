using System.Threading;
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
        Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
        Task<List<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default);
        Task<List<Account>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default);
        Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync(CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}
