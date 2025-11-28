using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using WileyWidget.Services.Abstractions.Models;

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
        Task<List<QuickBooksBudget>> GetBudgetsAsync(); // Stable app DTO for budgets - avoids direct Intuit SDK type dependency
        Task<bool> TestConnectionAsync();
    }
}
