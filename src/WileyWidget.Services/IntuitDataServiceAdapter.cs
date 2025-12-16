using System;
using System.Collections.Generic;
using System.Linq;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.QueryFilter;

namespace WileyWidget.Services
{
    internal sealed class IntuitDataServiceAdapter : IQuickBooksDataService
    {
        private readonly DataService _ds;
        private readonly ServiceContext? _ctx;

        public IntuitDataServiceAdapter(DataService ds)
        {
            _ds = ds ?? throw new ArgumentNullException(nameof(ds));
        }

        public IntuitDataServiceAdapter(ServiceContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _ds = new DataService(ctx);
        }

        public List<Customer> FindCustomers(int startPosition = 1, int pageSize = 100)
        {
            return _ds.FindAll(new Customer(), startPosition, pageSize).ToList();
        }

        public List<Invoice> FindInvoices(int startPosition = 1, int pageSize = 100)
        {
            return _ds.FindAll(new Invoice(), startPosition, pageSize).ToList();
        }

        public List<Account> FindAccounts(int startPosition = 1, int pageSize = 100)
        {
            return _ds.FindAll(new Account(), startPosition, pageSize).ToList();
        }

        public List<JournalEntry> FindJournalEntries(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Try a date-based query using QueryService when we have a ServiceContext;
                // fall back to FindAll if not supported.
                var q = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
                if (_ctx != null)
                {
                    var qs = new QueryService<JournalEntry>(_ctx);
                    return qs.ExecuteIdsQuery(q).Cast<JournalEntry>().ToList();
                }

                // As a last resort, try DataService-based query patterns (if supported by SDK)
                // Some SDK versions provide ExecuteQuery on DataService; call via reflection to remain flexible.
                var executeQuery = _ds.GetType().GetMethod("ExecuteQuery", new[] { typeof(string) });
                if (executeQuery != null)
                {
                    var results = executeQuery.Invoke(_ds, new object[] { q }) as System.Collections.IEnumerable;
                    return results?.Cast<JournalEntry>().ToList() ?? new List<JournalEntry>();
                }
            }
            catch
            {
                // fall through to FindAll fallback below
            }

            return _ds.FindAll(new JournalEntry(), 1, 500).ToList();
        }

        public List<Budget> FindBudgets(int startPosition = 1, int pageSize = 100)
        {
            // Some SDK versions expose Budget; this implements a simple FindAll-based adapter.
            return _ds.FindAll(new Budget(), startPosition, pageSize).ToList();
        }

        public List<Vendor> FindVendors(int startPosition = 1, int pageSize = 100)
        {
            return _ds.FindAll(new Vendor(), startPosition, pageSize).ToList();
        }
    }
}
