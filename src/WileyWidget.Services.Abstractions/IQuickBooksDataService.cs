using System;
using System.Collections.Generic;
using Intuit.Ipp.Data;

namespace WileyWidget.Services
{
    /// <summary>
    /// Abstraction over Intuit DataService used by QuickBooksService to enable test doubles.
    /// </summary>
    public interface IQuickBooksDataService
    {
        List<Customer> FindCustomers(int startPosition = 1, int pageSize = 100);
        List<Invoice> FindInvoices(int startPosition = 1, int pageSize = 100);
        List<Account> FindAccounts(int startPosition = 1, int pageSize = 100);
        List<JournalEntry> FindJournalEntries(DateTime startDate, DateTime endDate);
        // Support for QuickBooks Budget entities (some SDKs expose Budget; adapt as needed)
        List<Budget> FindBudgets(int startPosition = 1, int pageSize = 100);
        List<Vendor> FindVendors(int startPosition = 1, int pageSize = 100);
    }
}
