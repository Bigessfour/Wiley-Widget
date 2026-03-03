using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Plugins.Finance
{
    public class QuickBooksPlugin
    {
        private readonly IQuickBooksService _quickBooksService;
        private readonly ILogger<QuickBooksPlugin> _logger;

        public QuickBooksPlugin(IQuickBooksService quickBooksService, ILogger<QuickBooksPlugin> logger)
        {
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [KernelFunction]
        [Description("Checks if the application is currently connected to QuickBooks Online.")]
        public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
        {
            var connected = await _quickBooksService.IsConnectedAsync();
            _logger.LogDebug("QuickBooks connection status: {Connected}", connected);
            return connected;
        }

        [KernelFunction]
        [Description("Retrieves the list of budgets from QuickBooks.")]
        public async Task<string> GetBudgetsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("QuickBooksPlugin: fetching budgets");

            if (!await _quickBooksService.IsConnectedAsync())
            {
                _logger.LogWarning("QuickBooksPlugin: GetBudgets called but not connected to QuickBooks");
                return "Not connected to QuickBooks.";
            }

            try
            {
                var budgets = await _quickBooksService.GetBudgetsAsync();
                if (!budgets.Any())
                {
                    _logger.LogInformation("QuickBooksPlugin: no budgets found");
                    return "No budgets found.";
                }

                // Return a simplified summary to save tokens
                var summary = budgets.Select(b => new
                {
                    b.Name,
                    b.FiscalYear,
                    b.TotalAmount,
                    b.BudgetType
                });

                _logger.LogInformation("QuickBooksPlugin: returning {Count} budgets", budgets.Count);
                return JsonSerializer.Serialize(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickBooksPlugin: error retrieving budgets");
                return $"Error retrieving budgets: {ex.Message}";
            }
        }

        [KernelFunction]
        [Description("Retrieves the Chart of Accounts from QuickBooks.")]
        public async Task<string> GetChartOfAccountsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("QuickBooksPlugin: fetching chart of accounts");

            if (!await _quickBooksService.IsConnectedAsync())
            {
                _logger.LogWarning("QuickBooksPlugin: GetChartOfAccounts called but not connected to QuickBooks");
                return "Not connected to QuickBooks.";
            }

            try
            {
                var accounts = await _quickBooksService.GetChartOfAccountsAsync(CancellationToken.None);
                // Simplify for LLM
                var summary = accounts.Select(a => $"{a.AcctNum} - {a.Name} ({a.AccountType})").Take(50).ToList(); // Limit 50

                _logger.LogInformation("QuickBooksPlugin: returning {Total} total accounts (capped at 50 for LLM)", accounts.Count);
                return $"Found {accounts.Count} accounts. First 50: \n" + string.Join("\n", summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickBooksPlugin: error retrieving chart of accounts");
                return $"Error retrieving chart of accounts: {ex.Message}";
            }
        }
    }
}
