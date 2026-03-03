using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.QueryFilter;
using Intuit.Ipp.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IQuickBooksApiClient using Intuit SDK
    /// </summary>
    public class QuickBooksApiClient : IQuickBooksApiClient
    {
        private readonly SettingsService _settings;
        private readonly ILogger<QuickBooksApiClient> _logger;
        private readonly IQuickBooksAuthService _authService;
        private readonly int _tokenExpiryBufferSeconds;
        private static readonly Serilog.ILogger IntuitAdvancedRequestLogger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Fatal()
            .CreateLogger();

        public QuickBooksApiClient(
            SettingsService settings,
            ILogger<QuickBooksApiClient> logger,
            IQuickBooksAuthService authService,
            IOptions<QuickBooksOAuthOptions>? oauthOptions = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tokenExpiryBufferSeconds = oauthOptions?.Value.TokenExpiryBufferSeconds ?? 300;
        }

        private (ServiceContext Ctx, DataService Ds) GetDataService()
        {
            var s = _settings.Current;
            if (!HasValidAccessToken(s)) throw new InvalidOperationException("Access token invalid – refresh required.");
            if (string.IsNullOrWhiteSpace(s.QuickBooksRealmId))
                throw new InvalidOperationException("QuickBooks company (realmId) is not set. Connect to QuickBooks first.");

            // Use IQuickBooksAuthService as the single source of truth for environment
            // so SDK context and REST calls always target the same host.
            var environment = _authService.GetEnvironment();
            var validator = new OAuth2RequestValidator(s.QboAccessToken);
            var ctx = new ServiceContext(s.QuickBooksRealmId, IntuitServicesType.QBO, validator);
            ctx.IppConfiguration.BaseUrl.Qbo = environment == "sandbox"
                ? "https://sandbox-quickbooks.api.intuit.com/"
                : "https://quickbooks.api.intuit.com/";
            ConfigureIntuitLogging(ctx);
            return (ctx, new DataService(ctx));
        }

        private static void ConfigureIntuitLogging(ServiceContext context)
        {
            var requestLog = context.IppConfiguration.Logger.RequestLog;
            requestLog.EnableRequestResponseLogging = false;

            var requestAdvancedLog = context.IppConfiguration.AdvancedLogger?.RequestAdvancedLog;
            if (requestAdvancedLog == null)
            {
                return;
            }

            requestAdvancedLog.EnableSerilogRequestResponseLoggingForDebug = false;
            requestAdvancedLog.EnableSerilogRequestResponseLoggingForTrace = false;
            requestAdvancedLog.EnableSerilogRequestResponseLoggingForConsole = false;
            requestAdvancedLog.EnableSerilogRequestResponseLoggingForFile = false;
            requestAdvancedLog.CustomLogger = IntuitAdvancedRequestLogger;
        }

        private bool HasValidAccessToken(WileyWidget.Models.AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
            if (s.QboTokenExpiry == default) return false;
            // Use configured expiry buffer (default 300s = 5 minutes) to prevent mid-flight expiry
            return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(_tokenExpiryBufferSeconds);
        }

        public async Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                var customers = p.Ds.FindAll(new Customer(), 1, 100).ToList();
                _logger.LogInformation("Fetched {Count} customers from QuickBooks", customers.Count);
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO customers fetch failed");
                throw;
            }
        }

        public async Task<List<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                var invoices = p.Ds.FindAll(new Invoice(), 1, 100).ToList();
                _logger.LogInformation("Fetched {Count} invoices from QuickBooks", invoices.Count);
                return invoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO invoices fetch failed");
                throw;
            }
        }

        public async Task<List<Account>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allAccounts = new List<Account>();
                const int pageSize = 500;
                int startPosition = 1;
                int maxPages = 10;
                int pageCount = 0;

                _logger.LogInformation("Starting batch fetch of chart of accounts");

                while (pageCount < maxPages)
                {
                    var p = GetDataService();
                    var pageAccounts = p.Ds.FindAll(new Account(), startPosition, pageSize).ToList();

                    if (pageAccounts == null || pageAccounts.Count == 0)
                    {
                        _logger.LogInformation("No more accounts found at position {Position}, ending fetch", startPosition);
                        break;
                    }

                    allAccounts.AddRange(pageAccounts);
                    _logger.LogInformation("Fetched page {Page} with {Count} accounts (total: {Total})",
                        pageCount + 1, pageAccounts.Count, allAccounts.Count);

                    if (pageAccounts.Count < pageSize)
                    {
                        break;
                    }

                    startPosition += pageSize;
                    pageCount++;

                    if (pageCount < maxPages)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                }

                if (pageCount >= maxPages)
                {
                    _logger.LogWarning("Reached maximum page limit ({MaxPages}) for chart of accounts fetch. Total accounts: {Total}",
                        maxPages, allAccounts.Count);
                }

                _logger.LogInformation("Chart of accounts fetch completed. Total accounts: {Total}", allAccounts.Count);
                return allAccounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO chart of accounts batch fetch failed");
                throw;
            }
        }

        public async Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();

                var query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
                var qs = new QueryService<JournalEntry>(p.Ctx);
                var entries = qs.ExecuteIdsQuery(query).ToList();
                _logger.LogInformation("Fetched {Count} journal entries from QuickBooks ({Start:yyyy-MM-dd} to {End:yyyy-MM-dd})", entries.Count, startDate, endDate);
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO journal entries fetch failed");
                throw;
            }
        }

        public async Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();

                // Note: QuickBooks Online API doesn't have a native Budget entity
                // This implementation creates budgets from budget vs actual reports
                // For a full implementation, use QBO Reports API to fetch Budget vs Actuals

                var budgets = new List<WileyWidget.Models.QuickBooksBudget>();

                // Placeholder: Query budget data from QuickBooks Reports API
                // In production, you would call:
                // - Budget vs. Actuals report
                // - Parse report data into QuickBooksBudget objects

                _logger.LogInformation("QuickBooks Online doesn't support direct Budget entity queries. Use Reports API to fetch budget data.");

                return budgets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO budgets fetch failed");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                var customers = p.Ds.FindAll(new Customer(), 1, 1).ToList();
                _logger.LogInformation("QuickBooks connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO connection test failed");
                return false;
            }
        }
    }
}
