#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for payment/check management.
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaymentRepository> _logger;
    private readonly IAppEventBus? _eventBus;

    public PaymentRepository(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMemoryCache cache,
        ILogger<PaymentRepository> logger,
        IAppEventBus? eventBus = null)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventBus = eventBus;
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting all payments");
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting {Count} most recent payments", count);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payment by ID {Id}", id);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payment = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (payment != null)
        {
            await PopulateBudgetPostingDetailsAsync(context, new[] { payment }, cancellationToken);
        }

        return payment;
    }

    public async Task<IReadOnlyList<Payment>> GetByCheckNumberAsync(string checkNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments by check number {CheckNumber}", checkNumber);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.CheckNumber == checkNumber)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetByPayeeAsync(string payee, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments by payee {Payee}", payee);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.Payee.Contains(payee))
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments between {StartDate} and {EndDate}", startDate, endDate);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetByAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments for account {AccountId}", accountId);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.MunicipalAccountId == accountId)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetByVendorAsync(int vendorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments for vendor {VendorId}", vendorId);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.VendorId == vendorId)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<IReadOnlyList<Payment>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments with status {Status}", status);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await PopulateBudgetPostingDetailsAsync(context, payments, cancellationToken);
        return payments;
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));

        NormalizePaymentState(payment);

        _logger.LogInformation("PaymentRepository: Adding payment {CheckNumber} to {Payee}", payment.CheckNumber, payment.Payee);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await CheckNumberExistsAsync(context, payment.CheckNumber, null, cancellationToken))
        {
            throw new InvalidOperationException($"Check number {payment.CheckNumber} already exists");
        }

        payment.CreatedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
        await RefreshBudgetActualsAsync(context, new[] { PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate) }, cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} added successfully", payment.Id);
        return payment;
    }

    public async Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));

        NormalizePaymentState(payment);

        _logger.LogInformation("PaymentRepository: Updating payment {Id}", payment.Id);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await CheckNumberExistsAsync(context, payment.CheckNumber, payment.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Check number {payment.CheckNumber} already exists for another payment");
        }

        var existing = await context.Payments.FindAsync(new object[] { payment.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Payment {payment.Id} not found");
        }

        int previousFiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(existing.PaymentDate);

        existing.CheckNumber = payment.CheckNumber;
        existing.PaymentDate = payment.PaymentDate;
        existing.Payee = payment.Payee;
        existing.Amount = payment.Amount;
        existing.Description = payment.Description;
        existing.MunicipalAccountId = payment.MunicipalAccountId;
        existing.VendorId = payment.VendorId;
        existing.InvoiceId = payment.InvoiceId;
        existing.Status = payment.Status;
        existing.IsCleared = payment.IsCleared;
        existing.Memo = payment.Memo;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var affectedFiscalYears = new HashSet<int>
        {
            previousFiscalYear,
            PaymentBudgetPostingResolver.GetFiscalYear(existing.PaymentDate)
        };

        await RefreshBudgetActualsAsync(context, affectedFiscalYears, cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} updated successfully", payment.Id);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PaymentRepository: Deleting payment {Id}", id);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var payment = await context.Payments.FindAsync(new object[] { id }, cancellationToken);
        if (payment == null)
        {
            throw new InvalidOperationException($"Payment {id} not found");
        }

        int fiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate);

        context.Payments.Remove(payment);
        await context.SaveChangesAsync(cancellationToken);
        await RefreshBudgetActualsAsync(context, new[] { fiscalYear }, cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} deleted successfully", id);
    }

    public async Task<bool> CheckNumberExistsAsync(string checkNumber, int? excludePaymentId = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await CheckNumberExistsAsync(context, checkNumber, excludePaymentId, cancellationToken);
    }

    public async Task<decimal> GetTotalAmountAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Payments.Where(p => p.Status == "Cleared");

        if (startDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate <= endDate.Value);
        }

        return await query.SumAsync(p => p.Amount, cancellationToken);
    }

    public async Task<PaymentBudgetReconciliationResult> ReconcileBudgetMappingsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var payments = await context.Payments
            .Include(payment => payment.MunicipalAccount)
            .OrderBy(payment => payment.PaymentDate)
            .ThenBy(payment => payment.Id)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return new PaymentBudgetReconciliationResult();
        }

        var fiscalYears = payments
            .Select(payment => PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate))
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        var budgetEntries = await context.BudgetEntries
            .Where(entry => fiscalYears.Contains(entry.FiscalYear))
            .ToListAsync(cancellationToken);

        int budgetLinksAdded = 0;
        int postedCount = 0;
        int needsAccountCount = 0;
        int needsReconciliationCount = 0;
        int noBudgetLineCount = 0;
        int multipleBudgetLinesCount = 0;
        int conflictingBudgetAccountCount = 0;

        foreach (var payment in payments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int fiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate);
            var resolution = PaymentBudgetPostingResolver.Resolve(
                payment,
                payment.MunicipalAccount,
                budgetEntries.Where(entry => entry.FiscalYear == fiscalYear));

            switch (resolution.State)
            {
                case PaymentBudgetPostingState.Posted:
                    postedCount++;
                    break;
                case PaymentBudgetPostingState.NeedsAccount:
                    needsAccountCount++;
                    break;
                case PaymentBudgetPostingState.NeedsReconciliation:
                    needsReconciliationCount++;
                    if (resolution.CanAutoLinkByAccountNumber && resolution.MatchedBudgetEntry != null && payment.MunicipalAccountId.HasValue)
                    {
                        resolution.MatchedBudgetEntry.MunicipalAccountId = payment.MunicipalAccountId.Value;
                        resolution.MatchedBudgetEntry.UpdatedAt = DateTime.UtcNow;
                        budgetLinksAdded++;
                        postedCount++;
                        needsReconciliationCount--;
                    }
                    break;
                case PaymentBudgetPostingState.NoBudgetLine:
                    noBudgetLineCount++;
                    break;
                case PaymentBudgetPostingState.MultipleBudgetLines:
                    multipleBudgetLinesCount++;
                    break;
                case PaymentBudgetPostingState.ConflictingBudgetAccount:
                    conflictingBudgetAccountCount++;
                    break;
            }
        }

        if (budgetLinksAdded > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        int budgetRowsUpdated = await RefreshBudgetActualsAsync(context, fiscalYears, cancellationToken);

        return new PaymentBudgetReconciliationResult
        {
            PaymentsReviewed = payments.Count,
            BudgetLinksAdded = budgetLinksAdded,
            PostedPayments = postedCount,
            NeedsAccountCount = needsAccountCount,
            NeedsReconciliationCount = needsReconciliationCount,
            NoBudgetLineCount = noBudgetLineCount,
            MultipleBudgetLinesCount = multipleBudgetLinesCount,
            ConflictingBudgetAccountCount = conflictingBudgetAccountCount,
            BudgetRowsUpdated = budgetRowsUpdated,
            FiscalYearsAffected = fiscalYears
        };
    }

    private static Task<bool> CheckNumberExistsAsync(
        AppDbContext context,
        string checkNumber,
        int? excludePaymentId,
        CancellationToken cancellationToken)
    {
        var query = context.Payments.Where(p => p.CheckNumber == checkNumber);

        if (excludePaymentId.HasValue)
        {
            query = query.Where(p => p.Id != excludePaymentId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    private static void NormalizePaymentState(Payment payment)
    {
        var status = payment.Status?.Trim();
        payment.Status = string.IsNullOrWhiteSpace(status) ? "Pending" : status;

        if (string.Equals(payment.Status, "Void", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(payment.Status, "Voided", StringComparison.OrdinalIgnoreCase))
        {
            payment.Amount = 0m;
            payment.IsCleared = false;
            return;
        }

        if (string.Equals(payment.Status, "Cleared", StringComparison.OrdinalIgnoreCase))
        {
            payment.IsCleared = true;
        }
    }

    private async Task PopulateBudgetPostingDetailsAsync(AppDbContext context, IEnumerable<Payment> payments, CancellationToken cancellationToken)
    {
        var paymentList = payments.ToList();
        if (paymentList.Count == 0)
        {
            return;
        }

        var fiscalYears = paymentList
            .Select(payment => PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate))
            .Distinct()
            .ToList();

        var budgetEntries = await context.BudgetEntries
            .Where(entry => fiscalYears.Contains(entry.FiscalYear))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var payment in paymentList)
        {
            int fiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate);
            var resolution = PaymentBudgetPostingResolver.Resolve(
                payment,
                payment.MunicipalAccount,
                budgetEntries.Where(entry => entry.FiscalYear == fiscalYear));

            payment.BudgetPostingStatus = resolution.StatusText;
            payment.BudgetPostingDisplay = resolution.DisplayText;
            payment.MatchedBudgetEntryId = resolution.MatchedBudgetEntry?.Id;
            payment.BudgetLineDisplay = resolution.MatchedBudgetEntry != null
                ? PaymentBudgetPostingResolver.FormatBudgetLine(resolution.MatchedBudgetEntry)
                : string.Empty;
        }
    }

    private async Task<int> RefreshBudgetActualsAsync(AppDbContext context, IEnumerable<int> fiscalYears, CancellationToken cancellationToken)
    {
        var affectedFiscalYears = fiscalYears
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        if (affectedFiscalYears.Count == 0)
        {
            return 0;
        }

        await AutoLinkBudgetEntriesFromPaymentsAsync(context, affectedFiscalYears, cancellationToken);

        var budgetEntries = await context.BudgetEntries
            .Where(entry => affectedFiscalYears.Contains(entry.FiscalYear) && entry.MunicipalAccountId.HasValue)
            .ToListAsync(cancellationToken);

        if (budgetEntries.Count == 0)
        {
            InvalidateBudgetCaches(affectedFiscalYears);
            PublishBudgetActualsUpdatedEvents(affectedFiscalYears, 0);
            return 0;
        }

        DateTime minDate = affectedFiscalYears.Min(GetFiscalYearStartDate);
        DateTime maxDate = affectedFiscalYears.Max(GetFiscalYearEndDate);

        var payments = await context.Payments
            .Where(payment => payment.MunicipalAccountId.HasValue && payment.PaymentDate >= minDate && payment.PaymentDate <= maxDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalsByScope = payments
            .Where(CountsTowardBudgetActuals)
            .GroupBy(payment => new
            {
                FiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate),
                AccountId = payment.MunicipalAccountId!.Value
            })
            .ToDictionary(group => (group.Key.FiscalYear, group.Key.AccountId), group => group.Sum(payment => payment.Amount));

        int updatedRows = 0;

        foreach (var group in budgetEntries.GroupBy(entry => new { entry.FiscalYear, AccountId = entry.MunicipalAccountId!.Value }))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entriesForScope = group.ToList();
            BudgetEntry? entryToPost = entriesForScope.Count == 1
                ? entriesForScope[0]
                : TryResolveUniqueEntryByAccountNumber(entriesForScope);

            if (entryToPost == null)
            {
                continue;
            }

            decimal total = totalsByScope.TryGetValue((group.Key.FiscalYear, group.Key.AccountId), out var amount)
                ? amount
                : 0m;

            decimal variance = entryToPost.BudgetedAmount - total;
            if (entryToPost.ActualAmount == total && entryToPost.Variance == variance)
            {
                continue;
            }

            entryToPost.ActualAmount = total;
            entryToPost.Variance = variance;
            entryToPost.UpdatedAt = DateTime.UtcNow;
            updatedRows++;
        }

        if (updatedRows > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        InvalidateBudgetCaches(affectedFiscalYears);
        PublishBudgetActualsUpdatedEvents(affectedFiscalYears, updatedRows);
        return updatedRows;
    }

    private async Task AutoLinkBudgetEntriesFromPaymentsAsync(AppDbContext context, IReadOnlyCollection<int> fiscalYears, CancellationToken cancellationToken)
    {
        if (fiscalYears.Count == 0)
        {
            return;
        }

        DateTime minDate = fiscalYears.Min(GetFiscalYearStartDate);
        DateTime maxDate = fiscalYears.Max(GetFiscalYearEndDate);

        var payments = await context.Payments
            .Include(payment => payment.MunicipalAccount)
            .Where(payment => payment.MunicipalAccountId.HasValue && payment.PaymentDate >= minDate && payment.PaymentDate <= maxDate)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return;
        }

        var budgetEntries = await context.BudgetEntries
            .Where(entry => fiscalYears.Contains(entry.FiscalYear))
            .ToListAsync(cancellationToken);

        bool changed = false;

        foreach (var payment in payments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int fiscalYear = PaymentBudgetPostingResolver.GetFiscalYear(payment.PaymentDate);
            var resolution = PaymentBudgetPostingResolver.Resolve(
                payment,
                payment.MunicipalAccount,
                budgetEntries.Where(entry => entry.FiscalYear == fiscalYear));

            if (!resolution.CanAutoLinkByAccountNumber || resolution.MatchedBudgetEntry == null || !payment.MunicipalAccountId.HasValue)
            {
                continue;
            }

            if (resolution.MatchedBudgetEntry.MunicipalAccountId == payment.MunicipalAccountId.Value)
            {
                continue;
            }

            resolution.MatchedBudgetEntry.MunicipalAccountId = payment.MunicipalAccountId.Value;
            resolution.MatchedBudgetEntry.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static BudgetEntry? TryResolveUniqueEntryByAccountNumber(IReadOnlyList<BudgetEntry> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var distinctAccountNumbers = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.AccountNumber))
            .GroupBy(entry => AccountNumber.FormatDisplay(entry.AccountNumber), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinctAccountNumbers.Count == 1 && distinctAccountNumbers[0].Count() == 1
            ? distinctAccountNumbers[0].Single()
            : null;
    }

    private static bool CountsTowardBudgetActuals(Payment payment)
    {
        if (!payment.MunicipalAccountId.HasValue || payment.Amount == 0m)
        {
            return false;
        }

        return !string.Equals(payment.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(payment.Status?.Trim(), "Canceled", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(payment.Status?.Trim(), "Void", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(payment.Status?.Trim(), "Voided", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetFiscalYearStartDate(int fiscalYear)
    {
        return new DateTime(fiscalYear - 1, 7, 1);
    }

    private static DateTime GetFiscalYearEndDate(int fiscalYear)
    {
        return new DateTime(fiscalYear, 6, 30, 23, 59, 59, 999);
    }

    private void InvalidateBudgetCaches(IEnumerable<int> fiscalYears)
    {
        foreach (var fiscalYear in fiscalYears.Distinct())
        {
            try { _cache.Remove($"BudgetEntries_FiscalYear_{fiscalYear}"); } catch { }
            try { _cache.Remove($"BudgetEntries_Sewer_Year_{fiscalYear}"); } catch { }
        }
    }

    private void PublishBudgetActualsUpdatedEvents(IEnumerable<int> fiscalYears, int updatedRows)
    {
        foreach (var fiscalYear in fiscalYears.Distinct())
        {
            try
            {
                _eventBus?.Publish(new BudgetActualsUpdatedEvent(fiscalYear, updatedRows));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PaymentRepository: Failed to publish budget actuals update for FY {FiscalYear}", fiscalYear);
            }
        }
    }
}
