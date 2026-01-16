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

            var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var fiscalYearEnd = DateTime.UtcNow; // YTD up to now

            _logger.LogInformation("QuickBooksBudgetSync: Syncing FY {FiscalYear} actuals from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}", fiscalYear, fiscalYearStart, fiscalYearEnd);

            try
            {
                // Fetch chart of accounts once for mapping account id -> account number
                var chart = await _quickBooksService.GetChartOfAccountsAsync();
                var acctIdToNum = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var acctNameToNum = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var account in chart)
                {
                    var canonical = account.AcctNum ?? account.Name;
                    if (string.IsNullOrWhiteSpace(canonical))
                    {
                        _logger.LogWarning("QuickBooksBudgetSync: Skipping account with missing number/name (Id: {Id})", account.Id);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(account.Id))
                    {
                        acctIdToNum[account.Id] = canonical;
                    }

                    if (!string.IsNullOrWhiteSpace(account.Name) && !acctNameToNum.ContainsKey(account.Name))
                    {
                        acctNameToNum[account.Name] = canonical;
                    }
                }

                // Fetch journal entries for fiscal year-to-date
                var journals = await _quickBooksService.GetJournalEntriesAsync(fiscalYearStart, fiscalYearEnd);

                var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                foreach (var je in journals)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (je?.Line == null) continue;

                    foreach (var line in je.Line)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (line == null) continue;

                        var amount = line.Amount;
                        if (amount == 0m) continue;

                        // Try to resolve account number from journal line detail using strongly typed accessors
                        if (line.AnyIntuitObject is not JournalEntryLineDetail detail || detail.AccountRef is null)
                        {
                            continue;
                        }

                        var qbAcctId = detail.AccountRef.Value;
                        var qbAcctName = detail.AccountRef.name;

                        string? acctNum = null;
                        if (!string.IsNullOrEmpty(qbAcctId) && acctIdToNum.TryGetValue(qbAcctId, out var num1))
                        {
                            acctNum = num1;
                        }
                        else if (!string.IsNullOrEmpty(qbAcctName) && acctNameToNum.TryGetValue(qbAcctName, out var num2))
                        {
                            acctNum = num2;
                        }

                        if (string.IsNullOrWhiteSpace(acctNum))
                        {
                            continue;
                        }

                        // Only expense accounts (5xxx/6xxx) contribute to budget actuals
                        if (!acctNum.StartsWith("5", StringComparison.OrdinalIgnoreCase) && !acctNum.StartsWith("6", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var postingType = detail.PostingType;
                        var delta = postingType == Intuit.Ipp.Data.PostingTypeEnum.Credit ? -amount : amount;
                        totals[acctNum] = totals.TryGetValue(acctNum, out var existing) ? existing + delta : delta;
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
