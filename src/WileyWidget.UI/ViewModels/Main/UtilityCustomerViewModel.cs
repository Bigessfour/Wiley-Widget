using System;
using System.Windows;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// View model for managing utility customers
/// Provides data binding for customer CRUD operations and search functionality
/// </summary>
public class UtilityCustomerViewModel : BindableBase, INotifyDataErrorInfo, IDisposable, INavigationAware
{
    private readonly IUtilityCustomerRepository _customerRepository;
    private readonly IUtilityBillRepository _billRepository;
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IDialogService? _dialogService;
    private readonly ICacheService? _cacheService;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Collection of all customers for data binding
    /// </summary>
    public ObservableCollection<UtilityCustomer> Customers { get; } = new();

    /// <summary>
    /// Collection of bills for the selected customer
    /// </summary>
    public ObservableCollection<UtilityBill> CustomerBills { get; } = new();

    /// <summary>
    /// Collection of customer types for UI binding
    /// </summary>
    public IEnumerable<CustomerType> CustomerTypes { get; } = Enum.GetValues(typeof(CustomerType)).Cast<CustomerType>();

    /// <summary>
    /// Collection of service locations for UI binding
    /// </summary>
    public IEnumerable<ServiceLocation> ServiceLocations { get; } = Enum.GetValues(typeof(ServiceLocation)).Cast<ServiceLocation>();

    /// <summary>
    /// Collection of customer statuses for UI binding
    /// </summary>
    public IEnumerable<CustomerStatus> CustomerStatuses { get; } = Enum.GetValues(typeof(CustomerStatus)).Cast<CustomerStatus>();

    /// <summary>
    /// Currently selected customer in the UI
    /// </summary>
    private UtilityCustomer _selectedCustomer;
    public UtilityCustomer SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            // Unsubscribe from previous customer
            if (_selectedCustomer != null)
            {
                _selectedCustomer.PropertyChanged -= OnSelectedCustomerPropertyChanged;
            }

            if (SetProperty(ref _selectedCustomer, value))
            {
                // Subscribe to new customer
                if (_selectedCustomer != null)
                {
                    _selectedCustomer.PropertyChanged += OnSelectedCustomerPropertyChanged;
                    // Validate all customer properties
                    ValidateAllCustomerProperties();

                    // Populate ViewModel properties from selected customer
                    AccountNumber = _selectedCustomer.AccountNumber;
                    ServiceAddress = _selectedCustomer.ServiceAddress;

                    // Load charges for the selected customer
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var charges = await _billRepository.GetChargesByCustomerIdAsync(_selectedCustomer.Id);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Charges.Clear();
                                foreach (var charge in charges)
                                {
                                    Charges.Add(charge);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to load charges for customer {CustomerId}", _selectedCustomer.Id);
                        }
                    });
                }
                else
                {
                    // Clear customer-related errors and properties
                    ClearCustomerErrors();
                    AccountNumber = string.Empty;
                    ServiceAddress = string.Empty;
                    Charges.Clear();
                }
                // Update commands that depend on the selected customer
                EditCustomerCommand?.RaiseCanExecuteChanged();
                SaveCustomerCommand?.RaiseCanExecuteChanged();
                DeleteCustomerCommand?.RaiseCanExecuteChanged();
                LoadCustomerBillsCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                // Update commands that consider loading state
                EditCustomerCommand?.RaiseCanExecuteChanged();
                LoadCustomersCommand?.RaiseCanExecuteChanged();
                LoadActiveCustomersCommand?.RaiseCanExecuteChanged();
                LoadCustomersOutsideCityLimitsCommand?.RaiseCanExecuteChanged();
                SearchCustomersCommand?.RaiseCanExecuteChanged();
                AddCustomerCommand?.RaiseCanExecuteChanged();
                SaveCustomerCommand?.RaiseCanExecuteChanged();
                DeleteCustomerCommand?.RaiseCanExecuteChanged();
                PayBillCommand?.RaiseCanExecuteChanged();
                AnalyzeSelectedCustomerCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Search term for filtering customers
    /// </summary>
    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                ValidateProperty(nameof(SearchTerm), value);
            }
        }
    }

    /// <summary>
    /// Summary text for display
    /// </summary>
    private string _summaryText = "No customer data available";
    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    /// <summary>
    /// Account number for the selected customer
    /// </summary>
    private string _accountNumber = string.Empty;
    public string AccountNumber
    {
        get => _accountNumber;
        set
        {
            if (SetProperty(ref _accountNumber, value))
            {
                ValidateProperty(nameof(AccountNumber), value);
                // Update selected customer if it exists
                if (SelectedCustomer != null)
                {
                    SelectedCustomer.AccountNumber = value;
                }
            }
        }
    }

    /// <summary>
    /// Service address for the selected customer
    /// </summary>
    private string _serviceAddress = string.Empty;
    public string ServiceAddress
    {
        get => _serviceAddress;
        set
        {
            if (SetProperty(ref _serviceAddress, value))
            {
                ValidateProperty(nameof(ServiceAddress), value);
                // Update selected customer if it exists
                if (SelectedCustomer != null)
                {
                    SelectedCustomer.ServiceAddress = value;
                }
            }
        }
    }

    /// <summary>
    /// Collection of charges for the selected customer
    /// </summary>
    public ObservableCollection<Charge> Charges { get; } = new();

    /// <summary>
    /// Whether there's an error
    /// </summary>
    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    /// <summary>
    /// Error message if any
    /// </summary>
    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Status message presented in the UI
    /// </summary>
    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Analysis result from Grok AI for the selected customer
    /// </summary>
    private string _customerAnalysisResult = string.Empty;
    public string CustomerAnalysisResult
    {
        get => _customerAnalysisResult;
        set => SetProperty(ref _customerAnalysisResult, value);
    }

    /// <summary>
    /// Whether customer analysis is currently running
    /// </summary>
    private bool _isAnalyzingCustomer;
    public bool IsAnalyzingCustomer
    {
        get => _isAnalyzingCustomer;
        set => SetProperty(ref _isAnalyzingCustomer, value);
    }

    // Commands
    public DelegateCommand LoadCustomersCommand { get; private set; }
    public DelegateCommand LoadActiveCustomersCommand { get; private set; }
    public DelegateCommand LoadCustomersOutsideCityLimitsCommand { get; private set; }

    // Align with standardized pattern: expose a common LoadDataCommand
    public DelegateCommand LoadDataCommand => LoadCustomersCommand;
    public DelegateCommand SearchCustomersCommand { get; private set; }
    public DelegateCommand AddCustomerCommand { get; private set; }
    public DelegateCommand SaveCustomerCommand { get; private set; }
    public DelegateCommand DeleteCustomerCommand { get; private set; }
    public DelegateCommand ClearSearchCommand { get; private set; }
    public DelegateCommand ClearErrorCommand { get; private set; }
    public DelegateCommand LoadCustomerBillsCommand { get; private set; }
    public DelegateCommand PayBillCommand { get; private set; }
    public DelegateCommand AnalyzeSelectedCustomerCommand { get; private set; }
    // Command to open the edit dialog for the selected customer
    public DelegateCommand EditCustomerCommand { get; private set; }

    // All commands now use consistent naming without AsyncCommand aliases
    // XAML bindings have been updated to use the proper command names
    // All commands now use consistent naming without AsyncCommand aliases
    // XAML bindings have been updated to use the proper command names
    public UtilityCustomerViewModel(IUnitOfWork unitOfWork, IGrokSupercomputer grokSupercomputer, Prism.Dialogs.IDialogService? dialogService = null, ICacheService? cacheService = null)
    {
        if (unitOfWork is null)
        {
            throw new ArgumentNullException(nameof(unitOfWork));
        }

        _customerRepository = unitOfWork.UtilityCustomers
            ?? throw new ArgumentNullException(nameof(unitOfWork));
        _billRepository = unitOfWork.UtilityBills
            ?? throw new ArgumentNullException(nameof(unitOfWork));
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
    _dialogService = dialogService;
    _cacheService = cacheService;

        InitializeCommands();

        // Auto-load customers for E2E (cache-first)
        _ = Task.Run(async () =>
        {
            try
            {
                if (_cacheService != null)
                {
                    var cached = await _cacheService.GetAsync<System.Collections.Generic.List<UtilityCustomer>>("customers");
                    if (cached != null && cached.Any())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var c in cached) Customers.Add(c);
                        });
                        return;
                    }
                }

                var all = await _customerRepository.GetAllAsync();
                var list = all?.ToList() ?? new System.Collections.Generic.List<UtilityCustomer>();
                if (_cacheService != null && list.Any())
                    await _cacheService.SetAsync("customers", list, TimeSpan.FromHours(6));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var c in list) Customers.Add(c);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to auto-load customers in UtilityCustomerViewModel");
            }
        });
    }

    private void InitializeCommands()
    {
        LoadCustomersCommand = new DelegateCommand(async () => await ExecuteLoadCustomersAsync(), () => !IsLoading);
        LoadActiveCustomersCommand = new DelegateCommand(async () => await ExecuteLoadActiveCustomersAsync(), () => !IsLoading);
        LoadCustomersOutsideCityLimitsCommand = new DelegateCommand(async () => await ExecuteLoadCustomersOutsideCityLimitsAsync(), () => !IsLoading);
        SearchCustomersCommand = new DelegateCommand(async () => await ExecuteSearchCustomersAsync(), () => !IsLoading);
        AddCustomerCommand = new DelegateCommand(async () => await ExecuteAddCustomerAsync(), () => !IsLoading);
        SaveCustomerCommand = new DelegateCommand(async () => await ExecuteSaveCustomerAsync(), () => !IsLoading && SelectedCustomer != null);
        DeleteCustomerCommand = new DelegateCommand(async () => await ExecuteDeleteCustomerAsync(), () => !IsLoading && SelectedCustomer != null);
        ClearSearchCommand = new DelegateCommand(async () => await ExecuteClearSearchAsync());
        ClearErrorCommand = new DelegateCommand(ExecuteClearError);
        LoadCustomerBillsCommand = new DelegateCommand(async () => await ExecuteLoadCustomerBillsAsync(), () => SelectedCustomer != null);
        PayBillCommand = new DelegateCommand(async () => await ExecutePayBillAsync(), () => !IsLoading && SelectedBill != null);
        AnalyzeSelectedCustomerCommand = new DelegateCommand(async () => await ExecuteAnalyzeSelectedCustomerAsync(), () => !IsLoading && SelectedCustomer != null && !IsAnalyzingCustomer);
        EditCustomerCommand = new DelegateCommand(() => ShowEditCustomerDialog(), () => !IsLoading && SelectedCustomer != null);
    }

    // Navigation lifecycle (production-ready)
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // Cancel any previous load and start fresh if requested
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        var ct = _cancellationTokenSource.Token;

        // Honor navigation params: refresh / loadCustomerId
        _ = Task.Run(async () =>
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Preparing customers...";

                if (navigationContext?.Parameters != null)
                {
                    if (navigationContext.Parameters.ContainsKey("refresh") && navigationContext.Parameters["refresh"] is bool refresh && refresh)
                    {
                        await ExecuteLoadCustomersAsync();
                    }

                    if (navigationContext.Parameters.ContainsKey("loadCustomerId") && navigationContext.Parameters["loadCustomerId"] is int id)
                    {
                        var c = Customers.FirstOrDefault(x => x.Id == id);
                        if (c == null)
                        {
                            await ExecuteLoadCustomersAsync();
                            SelectedCustomer = Customers.FirstOrDefault(x => x.Id == id);
                        }
                        else
                        {
                            SelectedCustomer = c;
                        }
                    }
                }
                else
                {
                    if (!Customers.Any())
                        await ExecuteLoadCustomersAsync();
                }

                StatusMessage = "Ready";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Load cancelled";
                Log.Information("UtilityCustomer navigation load cancelled");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                StatusMessage = "Load failed";
                Log.Error(ex, "Error during UtilityCustomer OnNavigatedTo");
            }
            finally
            {
                IsLoading = false;
            }
        }, ct);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Cancel background operations to free resources
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error cancelling token in UtilityCustomerViewModel.OnNavigatedFrom");
        }
    }

    /// <summary>
    /// Loads all customers from the database
    /// </summary>
    private async Task ExecuteLoadCustomersAsync()
    {
        // Cancel any previous operation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading customers...";

            var customers = await _customerRepository.GetAllAsync();

            // Check if operation was cancelled
            token.ThrowIfCancellationRequested();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} customers.";
            Log.Information("Successfully loaded {Count} customers", customers.Count());
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Customer loading was cancelled.";
            Log.Information("Customer loading operation was cancelled");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load customers");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads active customers only
    /// </summary>
    private async Task ExecuteLoadActiveCustomersAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading active customers...";

            var customers = await _customerRepository.GetActiveCustomersAsync();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} active customers.";
            Log.Information("Successfully loaded {Count} active customers", customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load active customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load active customers");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads customers outside city limits
    /// </summary>
    private async Task ExecuteLoadCustomersOutsideCityLimitsAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading customers outside city limits...";

            var customers = await _customerRepository.GetCustomersOutsideCityLimitsAsync();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} customers outside city limits.";
            Log.Information("Successfully loaded {Count} customers outside city limits", customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customers outside city limits: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load customers outside city limits");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Searches customers based on the search term
    /// </summary>
    private async Task ExecuteSearchCustomersAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Searching customers...";

            var customers = await _customerRepository.SearchAsync(SearchTerm);

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Found {Customers.Count} customers matching '{SearchTerm}'.";
            Log.Information("Successfully searched customers with term '{SearchTerm}', found {Count} results", SearchTerm, customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to search customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to search customers with term '{SearchTerm}'", SearchTerm);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Adds a new customer
    /// </summary>

    private async Task ExecuteAddCustomerAsync()
    {
        // Use dialog service for creating a new customer if available
        if (_dialogService != null)
        {
            var temp = new UtilityCustomer { AccountNumber = await GenerateNextAccountNumberAsync() };
            var parameters = new DialogParameters { { "customer", temp } };
            // use a string key instead of nameof(...) to avoid compile-time dependency on a missing View type
            _dialogService.ShowDialog("CustomerEditDialogView", parameters, r =>
            {
                if (r.Parameters.ContainsKey("canceled") && r.Parameters.GetValue<bool>("canceled")) return;
                if (r.Parameters.ContainsKey("customer"))
                {
                    var returned = r.Parameters.GetValue<UtilityCustomer>("customer");
                    // Persist via repository
                    _ = PersistNewCustomerAsync(returned);
                }
            });
        }
        else
        {
            // Fallback: create and persist without dialog
            try
            {
                StatusMessage = "Adding new customer...";
                var newCustomer = new UtilityCustomer
                {
                    AccountNumber = await GenerateNextAccountNumberAsync(),
                    FirstName = "New",
                    LastName = "Customer",
                    ServiceAddress = "Enter service address",
                    ServiceCity = "City",
                    ServiceState = "ST",
                    ServiceZipCode = "12345",
                    CustomerType = CustomerType.Residential,
                    ServiceLocation = ServiceLocation.InsideCityLimits,
                    Status = CustomerStatus.Active,
                    AccountOpenDate = DateTime.Now,
                    Notes = "New customer - update details"
                };

                var addedCustomer = await _customerRepository.AddAsync(newCustomer);
                Customers.Add(addedCustomer);
                SelectedCustomer = addedCustomer;
                UpdateSummaryText();
                StatusMessage = $"Customer {addedCustomer.AccountNumber} added.";
                Log.Information("Successfully added new customer with account number {AccountNumber}", addedCustomer.AccountNumber);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add customer: {ex.Message}";
                HasError = true;
                StatusMessage = ErrorMessage;
                Log.Error(ex, "Failed to add new customer");
            }
        }
    }

    private async Task PersistNewCustomerAsync(UtilityCustomer customer)
    {
        try
        {
            var addedCustomer = await _customerRepository.AddAsync(customer);
            Customers.Add(addedCustomer);
            SelectedCustomer = addedCustomer;
            UpdateSummaryText();
            StatusMessage = $"Customer {addedCustomer.AccountNumber} added via dialog.";
            Log.Information("Successfully added new customer via dialog with account number {AccountNumber}", addedCustomer.AccountNumber);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to persist new customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to persist new customer from dialog");
        }
    }

    // New method to invoke edit dialog for existing customer
    private void ShowEditCustomerDialog()
    {
        if (_dialogService == null || SelectedCustomer == null) return;

        var parameters = new DialogParameters { { "customer", SelectedCustomer } };
        // use the same string key here as well
        _dialogService.ShowDialog("CustomerEditDialogView", parameters, r =>
        {
            if (r.Parameters.ContainsKey("canceled") && r.Parameters.GetValue<bool>("canceled")) return;
            if (r.Parameters.ContainsKey("customer"))
            {
                var returned = r.Parameters.GetValue<UtilityCustomer>("customer");
                // Persist changes
                _ = _customerRepository.UpdateAsync(returned);
            }
        });
    }

    /// <summary>
    /// Saves changes to the selected customer
    /// </summary>

    private async Task ExecuteSaveCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            StatusMessage = $"Saving customer {SelectedCustomer.AccountNumber}...";
            // Validate account number uniqueness
            if (await _customerRepository.ExistsByAccountNumberAsync(SelectedCustomer.AccountNumber, SelectedCustomer.Id))
            {
                ErrorMessage = "Account number already exists. Please choose a different account number.";
                HasError = true;
                StatusMessage = ErrorMessage;
                Log.Warning("Attempted to save customer with duplicate account number {AccountNumber}", SelectedCustomer.AccountNumber);
                return;
            }

            await _customerRepository.UpdateAsync(SelectedCustomer);
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = $"Customer {SelectedCustomer.AccountNumber} saved.";
            Log.Information("Successfully saved customer {AccountNumber}", SelectedCustomer.AccountNumber);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to save customer {AccountNumber}", SelectedCustomer.AccountNumber);
        }
    }

    /// <summary>
    /// Deletes the selected customer
    /// </summary>

    private async Task ExecuteDeleteCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            StatusMessage = $"Deleting customer {SelectedCustomer.AccountNumber}...";
            var accountNumber = SelectedCustomer.AccountNumber;
            var result = await _customerRepository.DeleteAsync(SelectedCustomer.Id);
            if (result)
            {
                Customers.Remove(SelectedCustomer);
                SelectedCustomer = null;
                UpdateSummaryText();
                HasError = false;
                ErrorMessage = string.Empty;
                StatusMessage = $"Customer {accountNumber} deleted.";
                Log.Information("Successfully deleted customer {AccountNumber}", accountNumber);
            }
            else
            {
                ErrorMessage = "Failed to delete customer - customer may not exist or may be referenced by other records.";
                HasError = true;
                StatusMessage = ErrorMessage;
                Log.Warning("Failed to delete customer {AccountNumber} - repository returned false", accountNumber);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            var accountNumber = SelectedCustomer?.AccountNumber ?? "unknown";
            Log.Error(ex, "Failed to delete customer {AccountNumber}", accountNumber);
        }
    }

    /// <summary>
    /// Generates the next available account number
    /// </summary>
    private async Task<string> GenerateNextAccountNumberAsync()
    {
        var count = await _customerRepository.GetCountAsync();
        return $"C{(count + 1):D6}"; // C000001, C000002, etc.
    }

    /// <summary>
    /// Updates the summary text based on current data
    /// </summary>
    private void UpdateSummaryText()
    {
        var totalCustomers = Customers.Count;
        var activeCustomers = Customers.Count(c => c.IsActive);
        var outsideCityLimits = Customers.Count(c => c.ServiceLocation == ServiceLocation.OutsideCityLimits);
        var totalBalance = Customers.Sum(c => c.CurrentBalance);

        SummaryText = $"{totalCustomers} customers ({activeCustomers} active), " +
                     $"{outsideCityLimits} outside city limits, " +
                     $"Total balance: {totalBalance:C}";
    }

    /// <summary>
    /// Clears the search and reloads all customers
    /// </summary>

    private async Task ExecuteClearSearchAsync()
    {
        SearchTerm = string.Empty;
        StatusMessage = "Clearing search results...";
        await ExecuteLoadCustomersAsync();
    }

    /// <summary>
    /// Clears any error state
    /// </summary>

    private void ExecuteClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        StatusMessage = "Ready";
        ClearErrors();
        Log.Information("Error cleared by user");
    }

    #region Bill Management

    /// <summary>
    /// Selected bill in the bill history grid
    /// </summary>
    private UtilityBill _selectedBill;
    public UtilityBill SelectedBill
    {
        get => _selectedBill;
        set => SetProperty(ref _selectedBill, value);
    }

    /// <summary>
    /// Loads bills for the currently selected customer
    /// </summary>
    private Task ExecuteLoadCustomerBillsAsync()
    {
        if (SelectedCustomer == null) return Task.CompletedTask;

        try
        {
            IsLoading = true;
            StatusMessage = $"Loading bills for {SelectedCustomer.DisplayName}...";

            // In a real implementation, you would fetch from a bill repository
            // For now, we'll generate sample data
            CustomerBills.Clear();

            // Generate sample bills (replace with actual repository call)
            var sampleBills = GenerateSampleBills(SelectedCustomer.Id);
            foreach (var bill in sampleBills)
            {
                CustomerBills.Add(bill);
            }

            StatusMessage = $"Loaded {CustomerBills.Count} bills for {SelectedCustomer.DisplayName}.";
            Log.Information("Successfully loaded {Count} bills for customer {CustomerId}", CustomerBills.Count, SelectedCustomer.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customer bills: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load bills for customer {CustomerId}", SelectedCustomer?.Id);
        }
        finally
        {
            IsLoading = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Calculates charges for a new bill
    /// </summary>
    public decimal CalculateWaterCharges(int gallonsUsed, ServiceLocation location)
    {
        // Rate structure: Base + tiered usage
        decimal baseCharge = location == ServiceLocation.InsideCityLimits ? 15.00m : 22.50m;
        decimal rate = location == ServiceLocation.InsideCityLimits ? 0.003m : 0.0045m;

        return baseCharge + (gallonsUsed * rate);
    }

    /// <summary>
    /// Calculates sewer charges based on water usage
    /// </summary>
    public decimal CalculateSewerCharges(int gallonsUsed, ServiceLocation location)
    {
        // Typically 80% of water usage
        decimal baseCharge = location == ServiceLocation.InsideCityLimits ? 12.00m : 18.00m;
        decimal rate = location == ServiceLocation.InsideCityLimits ? 0.0024m : 0.0036m;

        return baseCharge + (gallonsUsed * 0.8m * rate);
    }

    /// <summary>
    /// Calculates garbage service charges
    /// </summary>
    public decimal CalculateGarbageCharges(CustomerType customerType)
    {
        return customerType switch
        {
            CustomerType.Residential => 18.50m,
            CustomerType.Commercial => 45.00m,
            CustomerType.Industrial => 75.00m,
            CustomerType.Agricultural => 25.00m,
            _ => 18.50m
        };
    }

    /// <summary>
    /// Pays a selected bill
    /// </summary>

    private Task ExecutePayBillAsync()
    {
        if (SelectedBill == null) return Task.CompletedTask;

        try
        {
            StatusMessage = $"Processing payment for bill {SelectedBill.BillNumber}...";

            // In real implementation, integrate with payment gateway
            SelectedBill.AmountPaid = SelectedBill.TotalAmount;
            SelectedBill.Status = BillStatus.Paid;
            SelectedBill.PaidDate = DateTime.Now;

            // Update in repository
            // await _billRepository.UpdateAsync(SelectedBill);

            StatusMessage = $"Payment processed for bill {SelectedBill.BillNumber}.";
            Log.Information("Payment processed for bill {BillNumber}", SelectedBill.BillNumber);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to process payment: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to process payment for bill {BillNumber}", SelectedBill?.BillNumber);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates sample bills for demonstration (replace with actual repository)
    /// </summary>
    private List<UtilityBill> GenerateSampleBills(int customerId)
    {
        var bills = new List<UtilityBill>();
        var random = new Random();

        for (int i = 0; i < 6; i++)
        {
            var billDate = DateTime.Now.AddMonths(-i);
            var dueDate = billDate.AddDays(30);
            var waterUsage = random.Next(2000, 8000);

            var bill = new UtilityBill
            {
                Id = i + 1,
                CustomerId = customerId,
                BillNumber = $"BILL{DateTime.Now.Year}{(DateTime.Now.Month - i):D2}{customerId:D4}",
                BillDate = billDate,
                DueDate = dueDate,
                PeriodStartDate = billDate.AddDays(-30),
                PeriodEndDate = billDate,
                WaterUsageGallons = waterUsage,
                WaterCharges = 45.00m + (waterUsage * 0.003m),
                SewerCharges = 35.00m + (waterUsage * 0.8m * 0.0024m),
                GarbageCharges = 18.50m,
                StormwaterCharges = 5.00m,
                LateFees = i == 0 ? 0 : (i % 3 == 0 ? 10.00m : 0),
                Status = i > 2 ? BillStatus.Paid : (i == 0 ? BillStatus.Sent : BillStatus.Overdue),
                AmountPaid = i > 2 ? 0 : 0,
                Notes = i == 0 ? "Current bill" : $"Bill for period ending {billDate:MMM yyyy}"
            };

            if (bill.Status == BillStatus.Paid)
            {
                bill.AmountPaid = bill.TotalAmount;
                bill.PaidDate = dueDate.AddDays(-5);
            }

            bills.Add(bill);
        }

        return bills;
    }

    /// <summary>
    /// Analyzes the selected customer using Grok AI for natural language processing
    /// </summary>
    private async Task ExecuteAnalyzeSelectedCustomerAsync()
    {
        if (SelectedCustomer == null)
        {
            CustomerAnalysisResult = "No customer selected for analysis.";
            return;
        }

        try
        {
            IsAnalyzingCustomer = true;
            CustomerAnalysisResult = "Analyzing customer data...";
            StatusMessage = "Running AI analysis on customer data...";

            // Prepare customer data for analysis
            var customerData = new
            {
                SelectedCustomer.Id,
                SelectedCustomer.AccountNumber,
                SelectedCustomer.FirstName,
                SelectedCustomer.LastName,
                SelectedCustomer.CompanyName,
                SelectedCustomer.CustomerType,
                SelectedCustomer.ServiceAddress,
                SelectedCustomer.ServiceCity,
                SelectedCustomer.ServiceState,
                SelectedCustomer.ServiceZipCode,
                SelectedCustomer.CurrentBalance,
                SelectedCustomer.Status,
                SelectedCustomer.AccountOpenDate,
                SelectedCustomer.LastModifiedDate
            };

            // Call Grok API for analysis
            var analysis = await _grokSupercomputer.AnalyzeMunicipalDataAsync(
                customerData,
                $"Analyze this utility customer data and provide insights about their account status, payment patterns, service usage, and any recommendations for customer service or billing optimization."
            );

            CustomerAnalysisResult = analysis;
            StatusMessage = "Customer analysis completed.";
        }
        catch (Exception ex)
        {
            CustomerAnalysisResult = $"Error analyzing customer: {ex.Message}";
            StatusMessage = "Customer analysis failed.";
            Log.Error(ex, "Error analyzing selected customer with Grok AI");
        }
        finally
        {
            IsAnalyzingCustomer = false;
        }
    }

    #endregion

    #region INotifyDataErrorInfo Implementation

    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>
    /// Event raised when validation errors change
    /// </summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>
    /// Gets whether the ViewModel has validation errors
    /// </summary>
    public bool HasErrors => _errors.Any();

    /// <summary>
    /// Gets validation errors for a specific property or all properties
    /// </summary>
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.SelectMany(errors => errors);
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Validates a property and updates error collection
    /// </summary>
    private void ValidateProperty(string propertyName, object? value)
    {
        var errors = new List<string>();

        switch (propertyName)
        {
            case nameof(SearchTerm):
                if (!string.IsNullOrWhiteSpace(value as string) && (value as string)?.Length > 100)
                {
                    errors.Add("Search term cannot exceed 100 characters.");
                }
                break;
        }

        if (SelectedCustomer != null)
        {
            ValidateCustomerProperty(propertyName, value, errors);
        }

        _errors[propertyName] = errors;
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Validates customer-specific properties
    /// </summary>
    private void ValidateCustomerProperty(string propertyName, object? value, List<string> errors)
    {
        if (SelectedCustomer == null) return;

        switch (propertyName)
        {
            case nameof(SelectedCustomer.FirstName):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("First name is required.");
                }
                else if ((value as string)?.Length > 50)
                {
                    errors.Add("First name cannot exceed 50 characters.");
                }
                break;

            case nameof(SelectedCustomer.LastName):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Last name is required.");
                }
                else if ((value as string)?.Length > 50)
                {
                    errors.Add("Last name cannot exceed 50 characters.");
                }
                break;

            case nameof(SelectedCustomer.AccountNumber):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Account number is required.");
                }
                else if ((value as string)?.Length > 20)
                {
                    errors.Add("Account number cannot exceed 20 characters.");
                }
                break;

            case nameof(SelectedCustomer.ServiceAddress):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Service address is required.");
                }
                else if ((value as string)?.Length > 100)
                {
                    errors.Add("Service address cannot exceed 100 characters.");
                }
                break;

            case nameof(SelectedCustomer.ServiceCity):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Service city is required.");
                }
                else if ((value as string)?.Length > 50)
                {
                    errors.Add("Service city cannot exceed 50 characters.");
                }
                break;

            case nameof(SelectedCustomer.ServiceState):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Service state is required.");
                }
                else if ((value as string)?.Length != 2)
                {
                    errors.Add("Service state must be exactly 2 characters.");
                }
                break;

            case nameof(SelectedCustomer.ServiceZipCode):
                if (string.IsNullOrWhiteSpace(value as string))
                {
                    errors.Add("Service ZIP code is required.");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "", @"^\d{5}(-\d{4})?$"))
                {
                    errors.Add("Service ZIP code must be in format 12345 or 12345-6789.");
                }
                break;

            case nameof(SelectedCustomer.PhoneNumber):
                if (!string.IsNullOrWhiteSpace(value as string) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "", @"^[\d\s\-\(\)\+\.]{10,20}$"))
                {
                    errors.Add("Phone number format is invalid.");
                }
                break;

            case nameof(SelectedCustomer.EmailAddress):
                if (!string.IsNullOrWhiteSpace(value as string) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "",
                        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                {
                    errors.Add("Email address format is invalid.");
                }
                break;
        }
    }

    /// <summary>
    /// Handles property changes on the selected customer for validation
    /// </summary>
    private void OnSelectedCustomerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (SelectedCustomer != null && e.PropertyName != null)
        {
            var propertyInfo = typeof(UtilityCustomer).GetProperty(e.PropertyName);
            var value = propertyInfo?.GetValue(SelectedCustomer);
            ValidateProperty($"SelectedCustomer.{e.PropertyName}", value);
        }
    }

    /// <summary>
    /// Validates all properties of the selected customer
    /// </summary>
    private void ValidateAllCustomerProperties()
    {
        if (SelectedCustomer == null) return;

        var properties = typeof(UtilityCustomer).GetProperties();
        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                var value = property.GetValue(SelectedCustomer);
                ValidateProperty($"SelectedCustomer.{property.Name}", value);
            }
        }
    }

    /// <summary>
    /// Clears validation errors related to customer properties
    /// </summary>
    private void ClearCustomerErrors()
    {
        var customerErrorKeys = _errors.Keys.Where(key => key.StartsWith("SelectedCustomer.", StringComparison.Ordinal)).ToList();
        foreach (var key in customerErrorKeys)
        {
            _errors.Remove(key);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
        }
    }

    /// <summary>
    /// Clears all validation errors
    /// </summary>
    private void ClearErrors()
    {
        _errors.Clear();
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(null));
    }

    /// <summary>
    /// Disposes the ViewModel and cancels any ongoing operations
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    #endregion
}
