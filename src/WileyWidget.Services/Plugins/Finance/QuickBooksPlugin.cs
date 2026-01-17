using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Plugins.Finance
{
    public class QuickBooksPlugin
    {
        private readonly IQuickBooksService _quickBooksService;

        public QuickBooksPlugin(IQuickBooksService quickBooksService)
        {
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
        }

        [KernelFunction]
        [Description("Checks if the application is currently connected to QuickBooks Online.")]
        public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
        {
            return await _quickBooksService.IsConnectedAsync();
        }

        [KernelFunction]
        [Description("Retrieves the list of budgets from QuickBooks.")]
        public async Task<string> GetBudgetsAsync(CancellationToken cancellationToken = default)
        {
            if (!await _quickBooksService.IsConnectedAsync())
                return "Not connected to QuickBooks.";

            var budgets = await _quickBooksService.GetBudgetsAsync();
            if (!budgets.Any()) return "No budgets found.";

            // Return a simplified summary to save tokens
            var summary = budgets.Select(b => new
            {
                b.Name,
                b.FiscalYear,
                b.TotalAmount,
                b.BudgetType
            });

            return JsonSerializer.Serialize(summary);
        }

        [KernelFunction]
        [Description("Retrieves the Chart of Accounts from QuickBooks.")]
        public async Task<string> GetChartOfAccountsAsync(CancellationToken cancellationToken = default)
        {
            if (!await _quickBooksService.IsConnectedAsync())
                return "Not connected to QuickBooks.";

            var accounts = await _quickBooksService.GetChartOfAccountsAsync(CancellationToken.None);
            // Simplify for LLM
            var summary = accounts.Select(a => $"{a.AcctNum} - {a.Name} ({a.AccountType})").Take(50).ToList(); // Limit 50

            return $"Found {accounts.Count} accounts. First 50: \n" + string.Join("\n", summary);
        }
    }
}
