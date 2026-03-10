#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Payments panel (check register)
/// Provides CRUD operations, filtering, and payment management
/// </summary>
public sealed partial class PaymentsViewModel : ObservableObject, IDisposable
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMunicipalAccountRepository _municipalAccountRepository;
    private readonly ILogger<PaymentsViewModel> _logger;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    [ObservableProperty]
    private ObservableCollection<Payment> _payments = new();

    [ObservableProperty]
    private Payment? _selectedPayment;

    [ObservableProperty]
    private ObservableCollection<PaymentBudgetAccountOption> _budgetAccountOptions = new();

    [Obsolete("Search functionality is now handled by SfDataGrid View.Filter in PaymentsPanel.ApplySearchFilter()")]
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private string _selectedStatus = "All";

    public PaymentsViewModel(
        IPaymentRepository paymentRepository,
        IMunicipalAccountRepository municipalAccountRepository,
        ILogger<PaymentsViewModel> logger)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads all payments from the repository for full history visibility.
    /// </summary>
    [RelayCommand]
    private async Task LoadPaymentsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading payment history...";

            var cancellationToken = ReplaceCancellationTokenSource();

            var payments = await _paymentRepository.GetAllAsync(cancellationToken);
            await RefreshBudgetAccountOptionsAsync(payments, cancellationToken);

            Payments.Clear();
            foreach (var payment in payments)
            {
                Payments.Add(payment);
            }

            StatusMessage = Payments.Count == 1
                ? "Loaded 1 payment from history"
                : $"Loaded {Payments.Count} payments from history";
            _logger.LogInformation("PaymentsViewModel: Loaded {Count} payments from history", Payments.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Load cancelled";
            _logger.LogDebug("PaymentsViewModel: Load cancelled");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading payments: {ex.Message}";
            _logger.LogError(ex, "PaymentsViewModel: Error loading payments");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Searches payments by payee, check number, or description
    /// </summary>
    [Obsolete("Search functionality is now handled by SfDataGrid View.Filter in PaymentsPanel. Use LoadPaymentsCommand to reload full dataset.")]
    [RelayCommand]
    private async Task SearchPaymentsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadPaymentsAsync();
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Searching for '{SearchText}'...";

            var cancellationToken = ReplaceCancellationTokenSource();

            var payments = await _paymentRepository.GetByPayeeAsync(SearchText, cancellationToken);

            // Also search by check number
            var byCheckNumber = await _paymentRepository.GetByCheckNumberAsync(SearchText, cancellationToken);
            var allResults = payments.Union(byCheckNumber).Distinct().OrderByDescending(p => p.PaymentDate);

            Payments.Clear();
            foreach (var payment in allResults)
            {
                Payments.Add(payment);
            }

            StatusMessage = $"Found {Payments.Count} matching payments";
            _logger.LogInformation("PaymentsViewModel: Search found {Count} payments", Payments.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error searching payments: {ex.Message}";
            _logger.LogError(ex, "PaymentsViewModel: Error searching payments");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Filters payments by date range
    /// </summary>
    [RelayCommand]
    private async Task FilterByDateRangeAsync()
    {
        if (!StartDate.HasValue || !EndDate.HasValue)
        {
            await LoadPaymentsAsync();
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Filtering by date range...";

            var cancellationToken = ReplaceCancellationTokenSource();

            var payments = await _paymentRepository.GetByDateRangeAsync(StartDate.Value, EndDate.Value, cancellationToken);

            Payments.Clear();
            foreach (var payment in payments.OrderByDescending(p => p.PaymentDate))
            {
                Payments.Add(payment);
            }

            StatusMessage = $"Found {Payments.Count} payments in date range";
            _logger.LogInformation("PaymentsViewModel: Filtered by date range, found {Count} payments", Payments.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Filter cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error filtering payments: {ex.Message}";
            _logger.LogError(ex, "PaymentsViewModel: Error filtering payments");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deletes the selected payment
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeletePayment))]
    private async Task DeletePaymentAsync()
    {
        if (SelectedPayment == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting payment {SelectedPayment.CheckNumber}...";

            var cancellationToken = ReplaceCancellationTokenSource();

            await _paymentRepository.DeleteAsync(SelectedPayment.Id, cancellationToken);

            Payments.Remove(SelectedPayment);
            SelectedPayment = null;

            StatusMessage = "Payment deleted successfully";
            _logger.LogInformation("PaymentsViewModel: Payment deleted successfully");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting payment: {ex.Message}";
            _logger.LogError(ex, "PaymentsViewModel: Error deleting payment");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDeletePayment() => SelectedPayment != null && !IsLoading;

    /// <summary>
    /// Refreshes the current view
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPaymentsAsync();
    }

    public bool TryResolveBudgetAccountOption(int? accountId, out PaymentBudgetAccountOption? option)
    {
        option = BudgetAccountOptions.FirstOrDefault(candidate => candidate.AccountId == accountId);
        return option != null || accountId is null;
    }

    public async Task UpdatePaymentBudgetAccountAsync(Payment payment, int? municipalAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!TryResolveBudgetAccountOption(municipalAccountId, out var selectedOption) && municipalAccountId.HasValue)
        {
            EnsureHistoricalBudgetAccountOption(municipalAccountId.Value, payment.MunicipalAccount);
            selectedOption = BudgetAccountOptions.FirstOrDefault(candidate => candidate.AccountId == municipalAccountId);
        }

        payment.MunicipalAccountId = municipalAccountId;
        payment.MunicipalAccount = selectedOption?.Account;

        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        StatusMessage = municipalAccountId.HasValue
            ? $"Mapped check {payment.CheckNumber} to {selectedOption?.Display ?? payment.BudgetAccountDisplay}."
            : $"Cleared budget mapping for check {payment.CheckNumber}.";
        _logger.LogInformation(
            "PaymentsViewModel: Updated budget account mapping for payment {PaymentId} to {MunicipalAccountId}",
            payment.Id,
            municipalAccountId);
    }

    /// <summary>
    /// Gets the total amount of displayed payments
    /// </summary>
    [Obsolete("Total calculation is now handled by SfDataGrid GridTableSummaryRow with ClearedPaymentSummary custom aggregate in PaymentsPanel")]
    public decimal GetTotalAmount()
    {
        return Payments.Where(p => p.IsCleared).Sum(p => p.Amount);
    }

    private CancellationToken ReplaceCancellationTokenSource()
    {
        var newSource = new CancellationTokenSource();
        var previousSource = Interlocked.Exchange(ref _cts, newSource);

        if (previousSource != null)
        {
            try
            {
                previousSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            previousSource.Dispose();
        }

        return newSource.Token;
    }

    private async Task RefreshBudgetAccountOptionsAsync(IReadOnlyList<Payment> payments, CancellationToken cancellationToken)
    {
        var budgetAccounts = (await _municipalAccountRepository.GetBudgetAccountsAsync(cancellationToken)).ToList();
        if (budgetAccounts.Count == 0)
        {
            _logger.LogWarning("PaymentsViewModel: No budget accounts found; falling back to active municipal accounts for inline mapping");
            budgetAccounts = (await _municipalAccountRepository.GetAllAsync(cancellationToken))
                .Where(account => account.IsActive)
                .ToList();
        }

        var options = budgetAccounts
            .OrderBy(account => account.AccountNumber?.Value)
            .ThenBy(account => account.Name)
            .Select(account => new PaymentBudgetAccountOption(account.Id, BuildBudgetAccountDisplay(account), account))
            .ToList();

        foreach (var payment in payments)
        {
            if (payment.MunicipalAccountId is int accountId && options.All(option => option.AccountId != accountId))
            {
                options.Insert(0, new PaymentBudgetAccountOption(
                    accountId,
                    BuildBudgetAccountDisplay(payment.MunicipalAccount, accountId, isInactiveFallback: true),
                    payment.MunicipalAccount));
            }
        }

        BudgetAccountOptions.Clear();
        BudgetAccountOptions.Add(new PaymentBudgetAccountOption(null, "(Unassigned)", null));

        foreach (var option in options
            .GroupBy(candidate => candidate.AccountId)
            .Select(group => group.First()))
        {
            BudgetAccountOptions.Add(option);
        }
    }

    private void EnsureHistoricalBudgetAccountOption(int accountId, MunicipalAccount? account)
    {
        if (BudgetAccountOptions.Any(option => option.AccountId == accountId))
        {
            return;
        }

        BudgetAccountOptions.Add(new PaymentBudgetAccountOption(
            accountId,
            BuildBudgetAccountDisplay(account, accountId, isInactiveFallback: true),
            account));
    }

    private static string BuildBudgetAccountDisplay(MunicipalAccount account)
    {
        return BuildBudgetAccountDisplay(account, account.Id, isInactiveFallback: false);
    }

    private static string BuildBudgetAccountDisplay(MunicipalAccount? account, int accountId, bool isInactiveFallback)
    {
        var accountNumber = account?.AccountNumber?.DisplayValue;
        var accountName = account?.Name;

        string baseText;
        if (!string.IsNullOrWhiteSpace(accountNumber) && !string.IsNullOrWhiteSpace(accountName))
        {
            baseText = $"{accountNumber} - {accountName}";
        }
        else if (!string.IsNullOrWhiteSpace(accountName))
        {
            baseText = accountName;
        }
        else
        {
            baseText = $"Historical Account #{accountId}";
        }

        return isInactiveFallback ? $"[Inactive] {baseText}" : baseText;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var source = Interlocked.Exchange(ref _cts, null);

        if (source != null)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            source.Dispose();
        }

        _disposed = true;
    }
}

public sealed record PaymentBudgetAccountOption(int? AccountId, string Display, MunicipalAccount? Account);
