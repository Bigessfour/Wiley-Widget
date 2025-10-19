using System.Collections.Generic;
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
}