#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Models;

/// <summary>
/// Resolution state for matching a payment to a budget line.
/// </summary>
public enum PaymentBudgetPostingState
{
    Posted,
    NeedsAccount,
    NeedsReconciliation,
    NoBudgetLine,
    MultipleBudgetLines,
    ConflictingBudgetAccount,
}

/// <summary>
/// Result of resolving a payment against budget entries for a fiscal year.
/// </summary>
public sealed class PaymentBudgetPostingResolution
{
    public PaymentBudgetPostingState State { get; init; }

    public BudgetEntry? MatchedBudgetEntry { get; init; }

    public bool CanAutoLinkByAccountNumber { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;
}

/// <summary>
/// Summary from a payment-to-budget reconciliation run.
/// </summary>
public sealed class PaymentBudgetReconciliationResult
{
    public int PaymentsReviewed { get; init; }

    public int BudgetLinksAdded { get; init; }

    public int PostedPayments { get; init; }

    public int NeedsAccountCount { get; init; }

    public int NeedsReconciliationCount { get; init; }

    public int NoBudgetLineCount { get; init; }

    public int MultipleBudgetLinesCount { get; init; }

    public int ConflictingBudgetAccountCount { get; init; }

    public int BudgetRowsUpdated { get; init; }

    public IReadOnlyList<int> FiscalYearsAffected { get; init; } = Array.Empty<int>();

    public int NeedsAttentionCount =>
        NeedsAccountCount + NeedsReconciliationCount + NoBudgetLineCount + MultipleBudgetLinesCount + ConflictingBudgetAccountCount;

    public string Summary =>
        $"Reviewed {PaymentsReviewed} payments. Added {BudgetLinksAdded} budget links, refreshed {BudgetRowsUpdated} budget rows, and left {NeedsAttentionCount} payments needing review.";
}

/// <summary>
/// Helper methods for resolving payments against budget lines.
/// </summary>
public static class PaymentBudgetPostingResolver
{
    public static int GetFiscalYear(DateTime paymentDate)
    {
        return paymentDate.Month >= 7 ? paymentDate.Year + 1 : paymentDate.Year;
    }

    public static PaymentBudgetPostingResolution Resolve(
        Payment payment,
        MunicipalAccount? account,
        IEnumerable<BudgetEntry> fiscalYearBudgetEntries)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(fiscalYearBudgetEntries);

        int fiscalYear = GetFiscalYear(payment.PaymentDate);
        var entries = fiscalYearBudgetEntries.ToList();

        if (!payment.MunicipalAccountId.HasValue)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.NeedsAccount,
                StatusText = "Needs account",
                DisplayText = $"Review: select a budget account for FY {fiscalYear}."
            };
        }

        var linkedEntries = entries
            .Where(entry => entry.MunicipalAccountId == payment.MunicipalAccountId.Value)
            .ToList();

        if (linkedEntries.Count == 1)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.Posted,
                MatchedBudgetEntry = linkedEntries[0],
                StatusText = "Posted",
                DisplayText = $"Posted to {FormatBudgetLine(linkedEntries[0])}."
            };
        }

        if (linkedEntries.Count > 1)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.MultipleBudgetLines,
                StatusText = "Multiple budget lines",
                DisplayText = $"Review: multiple FY {fiscalYear} budget lines are linked to this account."
            };
        }

        string? accountNumber = account?.AccountNumber?.Value;
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.NoBudgetLine,
                StatusText = "No budget line",
                DisplayText = $"Review: no FY {fiscalYear} budget line is linked to the selected account."
            };
        }

        var equivalentAccountNumbers = new HashSet<string>(AccountNumber.GetEquivalentValues(accountNumber), StringComparer.OrdinalIgnoreCase);
        var accountNumberMatches = entries
            .Where(entry => equivalentAccountNumbers.Contains(AccountNumber.FormatDisplay(entry.AccountNumber)))
            .ToList();

        if (accountNumberMatches.Count == 0)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.NoBudgetLine,
                StatusText = "No budget line",
                DisplayText = $"Review: no FY {fiscalYear} budget line matches account {AccountNumber.FormatDisplay(accountNumber)}."
            };
        }

        if (accountNumberMatches.Count > 1)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.MultipleBudgetLines,
                StatusText = "Multiple budget lines",
                DisplayText = $"Review: account {AccountNumber.FormatDisplay(accountNumber)} matches multiple FY {fiscalYear} budget lines."
            };
        }

        var matchedEntry = accountNumberMatches[0];
        if (matchedEntry.MunicipalAccountId.HasValue && matchedEntry.MunicipalAccountId.Value != payment.MunicipalAccountId.Value)
        {
            return new PaymentBudgetPostingResolution
            {
                State = PaymentBudgetPostingState.ConflictingBudgetAccount,
                MatchedBudgetEntry = matchedEntry,
                StatusText = "Conflicting budget account",
                DisplayText = $"Review: {FormatBudgetLine(matchedEntry)} is already linked to another account."
            };
        }

        return new PaymentBudgetPostingResolution
        {
            State = PaymentBudgetPostingState.NeedsReconciliation,
            MatchedBudgetEntry = matchedEntry,
            CanAutoLinkByAccountNumber = true,
            StatusText = "Ready to link",
            DisplayText = $"Ready to link {FormatBudgetLine(matchedEntry)} by account number."
        };
    }

    public static string FormatBudgetLine(BudgetEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.IsNullOrWhiteSpace(entry.AccountNumber)
            ? entry.Description
            : $"{AccountNumber.FormatDisplay(entry.AccountNumber)} - {entry.Description}";
    }
}
