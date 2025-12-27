using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using Intuit.Ipp.Data;

namespace WileyWidget.TestUtilities
{
    /// <summary>
    /// Helper utilities and simple test doubles used by integration tests.
    /// </summary>
    public static class TestHelpers
    {
        public static DbContextOptions<AppDbContext> CreateInMemoryOptions(string? dbName = null)
        {
            var name = dbName ?? Guid.NewGuid().ToString();
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(name)
                .Options;
        }

        public static DbContextOptions<AppDbContext> CreateSqliteInMemoryOptions(out SqliteConnection connection)
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            return options;
        }

        public static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, System.Threading.Tasks.Task<HttpResponseMessage>> handler)
        {
            return new HttpClient(new DelegatingHandlerImpl(handler)) { Timeout = TimeSpan.FromSeconds(30) };
        }

        private sealed class DelegatingHandlerImpl : DelegatingHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, System.Threading.Tasks.Task<HttpResponseMessage>> _func;

            public DelegatingHandlerImpl(Func<HttpRequestMessage, CancellationToken, System.Threading.Tasks.Task<HttpResponseMessage>> func)
            {
                _func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _func(request, cancellationToken);
        }

        /// <summary>
        /// Minimal in-memory settings service for tests.
        /// </summary>
        public class FakeSettingsService : ISettingsService
        {
            public AppSettings Current { get; private set; } = new AppSettings();

            public string Get(string key) => string.Empty;
            public void Set(string key, string value) { }
            public string GetEnvironmentName() => "Test";
            public string GetValue(string key) => string.Empty;
            public void SetValue(string key, string value) { }
            public void Save() { }
            public void SaveFiscalYearSettings(int month, int day) { }
            public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Minimal secret vault stub used by tests. Returns configured dictionary values.
        /// </summary>
        public class FakeSecretVaultService : ISecretVaultService
        {
            private readonly Dictionary<string, string> _storage = new(StringComparer.OrdinalIgnoreCase);

            public void SetSecret(string key, string value) => _storage[key] = value;
            public string? GetSecret(string key) => _storage.TryGetValue(key, out var v) ? v : null;
            public void StoreSecret(string key, string value) => _storage[key] = value;
        public System.Threading.Tasks.Task<string?> GetSecretAsync(string key) => System.Threading.Tasks.Task.FromResult(GetSecret(key));
        public System.Threading.Tasks.Task SetSecretAsync(string key, string value) { StoreSecret(key, value); return System.Threading.Tasks.Task.CompletedTask; }
        public System.Threading.Tasks.Task RotateSecretAsync(string secretName, string newValue) { _storage[secretName] = newValue; return System.Threading.Tasks.Task.CompletedTask; }
        public System.Threading.Tasks.Task MigrateSecretsFromEnvironmentAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task PopulateProductionSecretsAsync() => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task<bool> TestConnectionAsync() => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<string> ExportSecretsAsync() => System.Threading.Tasks.Task.FromResult("{}");
        public System.Threading.Tasks.Task ImportSecretsAsync(string jsonSecrets) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<IEnumerable<string>> ListSecretKeysAsync() => System.Threading.Tasks.Task.FromResult((IEnumerable<string>)_storage.Keys);
        public System.Threading.Tasks.Task DeleteSecretAsync(string secretName) { _storage.Remove(secretName); return System.Threading.Tasks.Task.CompletedTask; }
        public System.Threading.Tasks.Task<string> GetDiagnosticsAsync() => System.Threading.Tasks.Task.FromResult("OK");
        }

        /// <summary>
        /// Simple fake QuickBooks data service for tests.
        /// </summary>
        public class FakeQuickBooksDataService : IQuickBooksDataService
        {
            public List<Intuit.Ipp.Data.Account> FindAccounts(int startPosition = 1, int pageSize = 100) => new List<Intuit.Ipp.Data.Account>();
            public List<Intuit.Ipp.Data.Bill> FindBills(int startPosition = 1, int pageSize = 100) => new List<Intuit.Ipp.Data.Bill>();
            public List<Intuit.Ipp.Data.Budget> FindBudgets(int startPosition = 1, int pageSize = 100) => new List<Intuit.Ipp.Data.Budget>();
            public List<Intuit.Ipp.Data.Customer> FindCustomers(int startPosition = 1, int pageSize = 100)
                => new List<Intuit.Ipp.Data.Customer> { new Intuit.Ipp.Data.Customer { DisplayName = "Test Customer" } };
            public List<Intuit.Ipp.Data.Invoice> FindInvoices(int startPosition = 1, int pageSize = 100) => new List<Intuit.Ipp.Data.Invoice> { new Intuit.Ipp.Data.Invoice { DocNumber = "INV-1", TotalAmt = 123.45M } };
            public List<Intuit.Ipp.Data.JournalEntry> FindJournalEntries(DateTime startDate, DateTime endDate) => new List<Intuit.Ipp.Data.JournalEntry>();
            public List<Intuit.Ipp.Data.Vendor> FindVendors(int startPosition = 1, int pageSize = 100) => new List<Intuit.Ipp.Data.Vendor>();
        }
    }
}
