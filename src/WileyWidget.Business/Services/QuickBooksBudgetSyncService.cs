using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Business.Services
{
    public class QuickBooksBudgetSyncService : IQuickBooksBudgetSyncService
    {
        private readonly IQuickBooksService _quickBooksService;
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<QuickBooksBudgetSyncService> _logger;
        private readonly IConfiguration? _configuration;
        private readonly WileyWidget.Services.Abstractions.IAppEventBus? _eventBus;

        public QuickBooksBudgetSyncService(
            IQuickBooksService quickBooksService,
            IBudgetRepository budgetRepository,
            ILogger<QuickBooksBudgetSyncService> logger,
            IConfiguration? configuration = null,
            WileyWidget.Services.Abstractions.IAppEventBus? eventBus = null)
        {
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _eventBus = eventBus;
        }

        public async Task<int> SyncFiscalYearActualsAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1);
            var fiscalYearEnd = DateTime.UtcNow; // YTD up to now

            _logger.LogInformation("QuickBooksBudgetSync: Syncing FY {FiscalYear} actuals from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}", fiscalYear, fiscalYearStart, fiscalYearEnd);

            try
            {
                // Fetch chart of accounts once for mapping account id -> account number
                var chart = await _quickBooksService.GetChartOfAccountsAsync();
                var acctIdToNum = chart
                    .Where(a => !string.IsNullOrEmpty(a.Id))
                    .ToDictionary(a => a.Id, a => (a.AcctNum ?? a.Name ?? a.Id), StringComparer.OrdinalIgnoreCase);

                var acctNameToNum = chart
                    .Where(a => !string.IsNullOrEmpty(a.Name))
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().AcctNum ?? g.First().Name, StringComparer.OrdinalIgnoreCase);

                // Fetch journal entries for fiscal year-to-date
                var journals = await _quickBooksService.GetJournalEntriesAsync(fiscalYearStart, fiscalYearEnd);

                var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                foreach (var je in journals)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (je?.Line == null) continue;

                    foreach (var line in je.Line)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        decimal amount = 0m;
                        try { amount = Convert.ToDecimal(line.Amount); } catch { continue; }

                        // Try to resolve account number from journal line detail
                        string? acctNum = null;

                        try
                        {
                            var detail = line.GetType().GetProperty("JournalEntryLineDetail")?.GetValue(line);
                            var accountRef = detail?.GetType().GetProperty("AccountRef")?.GetValue(detail);
                            var qbAcctId = accountRef?.GetType().GetProperty("Value")?.GetValue(accountRef) as string;
                            var qbAcctName = accountRef?.GetType().GetProperty("name")?.GetValue(accountRef) as string;

                            if (!string.IsNullOrEmpty(qbAcctId) && acctIdToNum.TryGetValue(qbAcctId, out var num1))
                            {
                                acctNum = num1;
                            }
                            else if (!string.IsNullOrEmpty(qbAcctName) && acctNameToNum.TryGetValue(qbAcctName, out var num2))
                            {
                                acctNum = num2;
                            }
                        }
                        catch
                        {
                            // best-effort mapping; skip if we can't resolve
                        }

                        if (string.IsNullOrWhiteSpace(acctNum)) continue;

                        if (!totals.TryGetValue(acctNum, out var existing)) totals[acctNum] = amount;
                        else totals[acctNum] = existing + amount;
                    }
                }

                if (!totals.Any())
                {
                    _logger.LogInformation("QuickBooksBudgetSync: No journal lines found for FY {FiscalYear}", fiscalYear);
                    return 0;
                }

                // Persist aggregated actuals to budget repository
                int updatedCount = await _budgetRepository.BulkUpdateActualsAsync(totals, fiscalYear, cancellationToken);

                _logger.LogInformation("QuickBooksBudgetSync: Updated {UpdatedCount} budget rows for FY {FiscalYear}", updatedCount, fiscalYear);

                // Publish application-level event so UI components can react (e.g., reload budgets)
                try
                {
                    _eventBus?.Publish(new WileyWidget.Services.Abstractions.BudgetActualsUpdatedEvent(fiscalYear, updatedCount));
                }
                catch (Exception exPub)
                {
                    _logger.LogWarning(exPub, "Failed to publish BudgetActualsUpdatedEvent");
                }

                return updatedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("QuickBooksBudgetSync: Operation cancelled for FY {FiscalYear}", fiscalYear);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickBooksBudgetSync: Failed to sync fiscal year actuals for FY {FiscalYear}", fiscalYear);
                throw;
            }
        }
    }
}
