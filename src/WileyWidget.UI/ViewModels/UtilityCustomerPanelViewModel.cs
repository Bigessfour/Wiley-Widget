using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.UI.ViewModels;

/// <summary>
/// ViewModel for the Utility Customer Panel providing comprehensive customer management functionality
/// </summary>
public class UtilityCustomerPanelViewModel : BindableBase, INavigationAware, IDisposable
{
    private readonly IUtilityCustomerRepository _customerRepository;
    private readonly IUtilityBillRepository _billRepository;
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IRegionManager _regionManager;

    private ObservableCollection<UtilityCustomer> _customers = new();
    private UtilityCustomer? _selectedCustomer;
    private ObservableCollection<UtilityBill> _selectedCustomerBills = new();
    private string _searchTerm = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _isDirty;
    private decimal _totalOutstandingBalance;
    private System.Timers.Timer? _autoSaveTimer;

    public UtilityCustomerPanelViewModel(
        IUtilityCustomerRepository customerRepository,
        IUtilityBillRepository billRepository,
        IGrokSupercomputer grokSupercomputer,
        IRegionManager regionManager)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));

        LoadCustomersAsyncCommand = new DelegateCommand(async () => await LoadCustomersAsync());
        SearchCustomersCommand = new DelegateCommand(async () => await SearchCustomersAsync());
        AddCustomerCommand = new DelegateCommand(async () => await AddCustomerAsync());
        UpdateCustomerCommand = new DelegateCommand(async () => await UpdateCustomerAsync(), CanUpdateCustomer);
        DeleteCustomerCommand = new DelegateCommand(async () => await DeleteCustomerAsync(), CanDeleteCustomer);
        ClearSearchCommand = new DelegateCommand(ClearSearch);
        RefreshCommand = new DelegateCommand(async () => await LoadCustomersAsync());
        GenerateReportCommand = new DelegateCommand(async () => await GenerateReportAsync());
        AnalyzeWithAICommand = new DelegateCommand(async () => await AnalyzeWithAIAsync(), CanAnalyzeWithAI);

        // Set up auto-save timer
        StartAutoSaveTimer();

        // Subscribe to property changes for validation
        PropertyChanged += OnPropertyChanged;
    }

    public ObservableCollection<UtilityCustomer> Customers
    {
        get => _customers;
        set => SetProperty(ref _customers, value);
    }

    public UtilityCustomer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                UpdateCustomerCommand.RaiseCanExecuteChanged();
                DeleteCustomerCommand.RaiseCanExecuteChanged();
                AnalyzeWithAICommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(HasSelectedCustomer));
                RaisePropertyChanged(nameof(SelectedCustomerDetails));
                // Load bills for the selected customer asynchronously
                _ = LoadSelectedCustomerBillsAsync();
            }
        }
    }

    public ObservableCollection<UtilityBill> SelectedCustomerBills
    {
        get => _selectedCustomerBills;
        set => SetProperty(ref _selectedCustomerBills, value);
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                SearchCustomersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    // Commands
    public DelegateCommand LoadCustomersAsyncCommand { get; }
    public DelegateCommand SearchCustomersCommand { get; }
    public DelegateCommand AddCustomerCommand { get; }
    public DelegateCommand UpdateCustomerCommand { get; }
    public DelegateCommand DeleteCustomerCommand { get; }
    public DelegateCommand ClearSearchCommand { get; }
    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand GenerateReportCommand { get; }
    public DelegateCommand AnalyzeWithAICommand { get; }

    // Computed properties
    public bool HasSelectedCustomer => SelectedCustomer != null;

    public string SelectedCustomerDetails => SelectedCustomer != null
        ? $"{SelectedCustomer.FirstName} {SelectedCustomer.LastName} - Account: {SelectedCustomer.AccountNumber}"
        : "No customer selected";

    public int TotalCustomers => Customers.Count;

    public int ActiveCustomers => Customers.Count(c => c.IsActive);

    public decimal TotalOutstandingBalance
    {
        get => _totalOutstandingBalance;
        set => SetProperty(ref _totalOutstandingBalance, value);
    }

    public string FormattedOutstandingBalance => TotalOutstandingBalance.ToString("C2", CultureInfo.CurrentCulture);

    private async Task LoadCustomersAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading customers...";

            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<UtilityCustomer>(customers.OrderBy(c => c.LastName));

            // Calculate total outstanding balance
            TotalOutstandingBalance = await CalculateTotalOutstandingBalanceAsync();

            StatusMessage = $"Loaded {Customers.Count} customers successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading customers: {ex.Message}";
            MessageBox.Show($"Failed to load customers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchCustomersAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            await LoadCustomersAsync();
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Searching for '{SearchTerm}'...";

            var results = await _customerRepository.SearchAsync(SearchTerm);
            Customers = new ObservableCollection<UtilityCustomer>(results.OrderBy(c => c.LastName));

            StatusMessage = $"Found {Customers.Count} customers matching '{SearchTerm}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error searching customers: {ex.Message}";
            MessageBox.Show($"Failed to search customers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddCustomerAsync()
    {
        try
        {
            var newCustomer = new UtilityCustomer
            {
                AccountNumber = await GenerateNextAccountNumberAsync(),
                FirstName = "New",
                LastName = "Customer",
                Status = CustomerStatus.Active,
                CreatedDate = DateTime.Now
            };

            var addedCustomer = await _customerRepository.AddAsync(newCustomer);
            Customers.Add(addedCustomer);
            SelectedCustomer = addedCustomer;

            StatusMessage = $"Added new customer: {addedCustomer.AccountNumber}";
            IsDirty = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding customer: {ex.Message}";
            MessageBox.Show($"Failed to add customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdateCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Updating customer...";

            var updatedCustomer = await _customerRepository.UpdateAsync(SelectedCustomer);
            var index = Customers.IndexOf(SelectedCustomer);
            if (index >= 0)
            {
                Customers[index] = updatedCustomer;
            }
            SelectedCustomer = updatedCustomer;

            StatusMessage = "Customer updated successfully.";
            IsDirty = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating customer: {ex.Message}";
            MessageBox.Show($"Failed to update customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete customer {SelectedCustomer.FirstName} {SelectedCustomer.LastName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Deleting customer...";

            var success = await _customerRepository.DeleteAsync(SelectedCustomer.Id);
            if (success)
            {
                Customers.Remove(SelectedCustomer);
                SelectedCustomer = Customers.FirstOrDefault();
                StatusMessage = "Customer deleted successfully.";
            }
            else
            {
                StatusMessage = "Failed to delete customer.";
                MessageBox.Show("Failed to delete customer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting customer: {ex.Message}";
            MessageBox.Show($"Failed to delete customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearSearch()
    {
        SearchTerm = string.Empty;
        _ = LoadCustomersAsync();
    }

    private async Task GenerateReportAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Generating report...";

            var reportData = new
            {
                TotalCustomers = TotalCustomers,
                ActiveCustomers = ActiveCustomers,
                TotalOutstandingBalance = TotalOutstandingBalance,
                Customers = await Task.WhenAll(Customers.Select(async c => new
                {
                    c.AccountNumber,
                    c.FirstName,
                    c.LastName,
                    c.CustomerType,
                    OutstandingBalance = await _billRepository.GetCustomerBalanceAsync(c.Id)
                }))
            };

            var analysis = await _grokSupercomputer.AnalyzeMunicipalDataAsync(reportData, "Utility Customer Report");

            StatusMessage = "Report generated successfully.";
            MessageBox.Show($"Report Analysis:\n{analysis}", "Report Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating report: {ex.Message}";
            MessageBox.Show($"Failed to generate report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AnalyzeWithAIAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Analyzing customer data with AI...";

            var analysis = await _grokSupercomputer.AnalyzeMunicipalDataAsync(SelectedCustomer, "Customer Analysis");

            StatusMessage = "AI analysis completed.";
            MessageBox.Show($"Customer Analysis:\n{analysis}", "AI Analysis Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing with AI: {ex.Message}";
            MessageBox.Show($"Failed to analyze with AI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanUpdateCustomer() => SelectedCustomer != null && IsDirty;

    private bool CanDeleteCustomer() => SelectedCustomer != null;

    private bool CanAnalyzeWithAI() => SelectedCustomer != null;

    private async Task<string> GenerateNextAccountNumberAsync()
    {
        // Generate account number based on current date and sequence
        var datePart = DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture);
        var sequence = (await _customerRepository.GetCountAsync() + 1).ToString("D4", CultureInfo.InvariantCulture);
        return $"{datePart}{sequence}";
    }

    private void StartAutoSaveTimer()
    {
        _autoSaveTimer = new System.Timers.Timer(30000); // 30 seconds
        _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
        _autoSaveTimer.Start();
    }

    private async void OnAutoSaveTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (IsDirty && SelectedCustomer != null)
        {
            try
            {
                await _customerRepository.UpdateAsync(SelectedCustomer);
                IsDirty = false;
                StatusMessage = "Auto-saved customer changes.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Auto-save failed: {ex.Message}";
            }
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IsDirty) && e.PropertyName != nameof(IsLoading) && e.PropertyName != nameof(StatusMessage))
        {
            IsDirty = true;
        }
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = LoadCustomersAsync();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Auto-save on navigation
        if (IsDirty && SelectedCustomer != null)
        {
            _ = _customerRepository.UpdateAsync(SelectedCustomer);
        }
    }

    #region Bill Management Methods

    /// <summary>
    /// Loads bills for the currently selected customer asynchronously.
    /// </summary>
    private async Task LoadSelectedCustomerBillsAsync()
    {
        if (SelectedCustomer == null)
        {
            SelectedCustomerBills.Clear();
            return;
        }

        try
        {
            IsLoading = true;
            var bills = await LoadCustomerBillsAsync(SelectedCustomer.Id);
            SelectedCustomerBills.Clear();
            foreach (var bill in bills)
            {
                SelectedCustomerBills.Add(bill);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading customer bills: {ex.Message}";
            SelectedCustomerBills.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads bills for a specific customer.
    /// </summary>
    private async Task<IEnumerable<UtilityBill>> LoadCustomerBillsAsync(int customerId)
    {
        try
        {
            return await _billRepository.GetByCustomerIdAsync(customerId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading bills: {ex.Message}";
            return Enumerable.Empty<UtilityBill>();
        }
    }

    /// <summary>
    /// Calculates the total outstanding balance across all customers.
    /// </summary>
    private async Task<decimal> CalculateTotalOutstandingBalanceAsync()
    {
        try
        {
            decimal total = 0;
            foreach (var customer in Customers)
            {
                total += await CalculateCustomerBalanceAsync(customer.Id);
            }
            return total;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error calculating balance: {ex.Message}";
            return 0;
        }
    }

    /// <summary>
    /// Calculates the outstanding balance for a specific customer.
    /// </summary>
    private async Task<decimal> CalculateCustomerBalanceAsync(int customerId)
    {
        try
        {
            return await _billRepository.GetCustomerBalanceAsync(customerId);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Generates sample bills for demonstration purposes.
    /// In production, this would be removed and bills loaded from database.
    /// </summary>
    private IEnumerable<UtilityBill> GenerateSampleBillsForCustomer(int customerId)
    {
        // Generate 1-3 sample bills per customer for demonstration
        var random = new Random(customerId); // Deterministic based on customer ID
        var billCount = random.Next(1, 4);

        for (int i = 0; i < billCount; i++)
        {
            var billDate = DateTime.Now.AddDays(-random.Next(1, 90));
            var totalAmount = (decimal)(random.Next(50, 500) + random.NextDouble() * 100);
            var amountPaid = random.NextDouble() < 0.3 ? totalAmount : 0; // 30% chance of being paid
            var status = amountPaid > 0 ? BillStatus.Paid : BillStatus.Pending;

            yield return new UtilityBill
            {
                Id = customerId * 100 + i,
                CustomerId = customerId,
                BillNumber = $"BILL-{customerId:D4}-{i + 1:D2}",
                BillDate = billDate,
                DueDate = billDate.AddDays(30),
                PeriodStartDate = billDate.AddMonths(-1),
                PeriodEndDate = billDate,
                WaterCharges = totalAmount * 0.6m,
                SewerCharges = totalAmount * 0.3m,
                GarbageCharges = totalAmount * 0.1m,
                AmountPaid = amountPaid,
                Status = status
            };
        }
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoSaveTimer?.Dispose();
            PropertyChanged -= OnPropertyChanged;
        }
    }
}
