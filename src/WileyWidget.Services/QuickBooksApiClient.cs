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

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IQuickBooksApiClient using Intuit SDK
    /// </summary>
    public class QuickBooksApiClient : IQuickBooksApiClient
    {
        private readonly SettingsService _settings;
        private readonly ILogger<QuickBooksApiClient> _logger;

        public QuickBooksApiClient(SettingsService settings, ILogger<QuickBooksApiClient> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private (ServiceContext Ctx, DataService Ds) GetDataService()
        {
            var s = _settings.Current;
            if (!HasValidAccessToken(s)) throw new InvalidOperationException("Access token invalid – refresh required.");
            if (string.IsNullOrWhiteSpace(s.QuickBooksRealmId))
                throw new InvalidOperationException("QuickBooks company (realmId) is not set. Connect to QuickBooks first.");
            var validator = new OAuth2RequestValidator(s.QboAccessToken);
            var ctx = new ServiceContext(s.QuickBooksRealmId, IntuitServicesType.QBO, validator);
            ctx.IppConfiguration.BaseUrl.Qbo = s.QuickBooksEnvironment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
            return (ctx, new DataService(ctx));
        }

        private static bool HasValidAccessToken(WileyWidget.Models.AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
            if (s.QboTokenExpiry == default) return false;
            return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(60);
        }

        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                return p.Ds.FindAll(new Customer(), 1, 100).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO customers fetch failed");
                throw;
            }
        }

        public async Task<List<Invoice>> GetInvoicesAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                return p.Ds.FindAll(new Invoice(), 1, 100).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO invoices fetch failed");
                throw;
            }
        }

        public async Task<List<Account>> GetChartOfAccountsAsync()
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

        public async Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();

                var query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
                var qs = new QueryService<JournalEntry>(p.Ctx);
                return qs.ExecuteIdsQuery(query).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO journal entries fetch failed");
                throw;
            }
        }

        public async Task<List<Budget>> GetBudgetsAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                return p.Ds.FindAll(new Budget(), 1, 100).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QBO budgets fetch failed");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.CompletedTask;
                var p = GetDataService();
                var customers = p.Ds.FindAll(new Customer(), 1, 1).ToList();
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