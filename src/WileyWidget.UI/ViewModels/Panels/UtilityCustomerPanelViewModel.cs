using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.ViewModels.Panels
{
    /// <summary>
    /// ViewModel for the Utility Customer Panel View
    /// Provides comprehensive customer management functionality including CRUD operations,
    /// search, validation, and bill management.
    /// </summary>
    public class UtilityCustomerPanelViewModel : BindableBase, INotifyDataErrorInfo, IDisposable, INavigationAware
    {
        private readonly IUtilityCustomerRepository _customerRepository;
        private readonly IGrokSupercomputer _grokSupercomputer;
        private readonly Prism.Dialogs.IDialogService? _dialogService;
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
                    }
                    else
                    {
                        // Clear customer-related errors
                        ClearCustomerErrors();
                    }
                    // Update commands that depend on the selected customer
                    EditCustomerCommand?.RaiseCanExecuteChanged();
                    SaveCustomerCommand?.RaiseCanExecuteChanged();
                    DeleteCustomerCommand?.RaiseCanExecuteChanged();
                    LoadCustomerBillsCommand?.RaiseCanExecuteChanged();
                    AnalyzeSelectedCustomerCommand?.RaiseCanExecuteChanged();
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

        #region AsyncCommand Aliases - For XAML Binding Compatibility
        // These are aliases to the existing commands for XAML bindings that expect "AsyncCommand" suffix
        // Provided for backward compatibility and consistent XAML binding patterns
        public DelegateCommand LoadCustomersAsyncCommand => LoadCustomersCommand;
        public DelegateCommand LoadActiveCustomersAsyncCommand => LoadActiveCustomersCommand;
        public DelegateCommand LoadCustomersOutsideCityLimitsAsyncCommand => LoadCustomersOutsideCityLimitsCommand;
        public DelegateCommand SearchCustomersAsyncCommand => SearchCustomersCommand;
        public DelegateCommand AddCustomerAsyncCommand => AddCustomerCommand;
        public DelegateCommand SaveCustomerAsyncCommand => SaveCustomerCommand;
        public DelegateCommand DeleteCustomerAsyncCommand => DeleteCustomerCommand;
        public DelegateCommand ClearSearchAsyncCommand => ClearSearchCommand;
        public DelegateCommand LoadCustomerBillsAsyncCommand => LoadCustomerBillsCommand;
        public DelegateCommand PayBillAsyncCommand => PayBillCommand;
        #endregion

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public UtilityCustomerPanelViewModel(IUnitOfWork unitOfWork, IGrokSupercomputer grokSupercomputer, Prism.Dialogs.IDialogService? dialogService = null, ICacheService? cacheService = null)
        {
            if (unitOfWork is null)
            {
                throw new ArgumentNullException(nameof(unitOfWork));
            }

            _customerRepository = unitOfWork.UtilityCustomers
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
                    await ExecuteLoadCustomersAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to auto-load customers in UtilityCustomerPanelViewModel");
                    StatusMessage = "Failed to load customers";
                    HasError = true;
                    ErrorMessage = ex.Message;
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
                    if (navigationContext.Parameters.ContainsKey("refresh") &&
                        bool.TryParse(navigationContext.Parameters["refresh"].ToString(), out var refresh) && refresh)
                    {
                        await ExecuteLoadCustomersAsync();
                    }

                    if (navigationContext.Parameters.ContainsKey("loadCustomerId") &&
                        int.TryParse(navigationContext.Parameters["loadCustomerId"].ToString(), out var customerId))
                    {
                        // Load specific customer if requested
                        var customer = Customers.FirstOrDefault(c => c.Id == customerId);
                        if (customer != null)
                        {
                            SelectedCustomer = customer;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Navigation was cancelled, ignore
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling navigation parameters in UtilityCustomerPanelViewModel");
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
                Log.Error(ex, "Error cancelling operations in OnNavigatedFrom");
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
                StatusMessage = "Loading customers...";
                HasError = false;
                ErrorMessage = string.Empty;

                token.ThrowIfCancellationRequested();

                var customers = await _customerRepository.GetAllAsync();
                var customerList = customers.ToList();

                token.ThrowIfCancellationRequested();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Clear();
                    foreach (var customer in customerList)
                    {
                        Customers.Add(customer);
                    }
                    UpdateSummaryText();
                });

                StatusMessage = $"Loaded {customerList.Count} customers";
                Log.Information("Loaded {Count} customers successfully", customerList.Count);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Operation cancelled";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load customers");
                StatusMessage = "Failed to load customers";
                HasError = true;
                ErrorMessage = ex.Message;
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
                StatusMessage = "Loading active customers...";
                HasError = false;
                ErrorMessage = string.Empty;

                var customers = await _customerRepository.GetActiveCustomersAsync();
                var customerList = customers.ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Clear();
                    foreach (var customer in customerList)
                    {
                        Customers.Add(customer);
                    }
                    UpdateSummaryText();
                });

                StatusMessage = $"Loaded {customerList.Count} active customers";
                Log.Information("Loaded {Count} active customers successfully", customerList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load active customers");
                StatusMessage = "Failed to load active customers";
                HasError = true;
                ErrorMessage = ex.Message;
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
                StatusMessage = "Loading customers outside city limits...";
                HasError = false;
                ErrorMessage = string.Empty;

                var customers = await _customerRepository.GetCustomersOutsideCityLimitsAsync();
                var customerList = customers.ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Clear();
                    foreach (var customer in customerList)
                    {
                        Customers.Add(customer);
                    }
                    UpdateSummaryText();
                });

                StatusMessage = $"Loaded {customerList.Count} customers outside city limits";
                Log.Information("Loaded {Count} customers outside city limits successfully", customerList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load customers outside city limits");
                StatusMessage = "Failed to load customers outside city limits";
                HasError = true;
                ErrorMessage = ex.Message;
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
                StatusMessage = "Searching customers...";
                HasError = false;
                ErrorMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(SearchTerm))
                {
                    await ExecuteLoadCustomersAsync();
                    return;
                }

                var customers = await _customerRepository.SearchAsync(SearchTerm);
                var customerList = customers.ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Clear();
                    foreach (var customer in customerList)
                    {
                        Customers.Add(customer);
                    }
                    UpdateSummaryText();
                });

                StatusMessage = $"Found {customerList.Count} customers matching '{SearchTerm}'";
                Log.Information("Search completed: found {Count} customers for term '{Term}'", customerList.Count, SearchTerm);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to search customers");
                StatusMessage = "Failed to search customers";
                HasError = true;
                ErrorMessage = ex.Message;
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
                    if (r.Parameters.ContainsKey("canceled") && r.Parameters.GetValue<bool>("canceled"))
                        return;
                    if (r.Parameters.ContainsKey("customer"))
                    {
                        var savedCustomer = r.Parameters.GetValue<UtilityCustomer>("customer");
                        if (savedCustomer != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Customers.Add(savedCustomer);
                                SelectedCustomer = savedCustomer;
                                UpdateSummaryText();
                            });
                            StatusMessage = "Customer added successfully";
                        }
                    }
                });
            }
            else
            {
                // Fallback: create and persist without dialog
                try
                {
                    IsLoading = true;
                    StatusMessage = "Adding new customer...";

                    var newCustomer = new UtilityCustomer
                    {
                        AccountNumber = await GenerateNextAccountNumberAsync(),
                        FirstName = "New",
                        LastName = "Customer",
                        Status = CustomerStatus.Active,
                        CustomerType = CustomerType.Residential,
                        ServiceLocation = ServiceLocation.InsideCityLimits,
                        AccountOpenDate = DateTime.Now,
                        CreatedDate = DateTime.Now
                    };

                    await _customerRepository.AddAsync(newCustomer);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Customers.Add(newCustomer);
                        SelectedCustomer = newCustomer;
                        UpdateSummaryText();
                    });

                    StatusMessage = "New customer added successfully";
                    Log.Information("Added new customer with account number {AccountNumber}", newCustomer.AccountNumber);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to add new customer");
                    StatusMessage = "Failed to add customer";
                    HasError = true;
                    ErrorMessage = ex.Message;
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private async Task PersistNewCustomerAsync(UtilityCustomer customer)
        {
            try
            {
                await _customerRepository.AddAsync(customer);
                Log.Information("Persisted new customer {AccountNumber}", customer.AccountNumber);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to persist new customer {AccountNumber}", customer.AccountNumber);
                throw;
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
                if (r.Parameters.ContainsKey("canceled") && r.Parameters.GetValue<bool>("canceled"))
                    return;
                if (r.Parameters.ContainsKey("customer"))
                {
                    var updatedCustomer = r.Parameters.GetValue<UtilityCustomer>("customer");
                    if (updatedCustomer != null)
                    {
                        // The customer object is already updated via binding, just refresh UI
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateSummaryText();
                        });
                        StatusMessage = "Customer updated successfully";
                    }
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
                IsLoading = true;
                StatusMessage = "Saving customer...";
                HasError = false;
                ErrorMessage = string.Empty;

                // Validate before saving
                if (HasErrors)
                {
                    StatusMessage = "Please fix validation errors before saving";
                    HasError = true;
                    ErrorMessage = "Validation errors must be resolved";
                    return;
                }

                SelectedCustomer.LastModifiedDate = DateTime.Now;
                await _customerRepository.UpdateAsync(SelectedCustomer);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateSummaryText();
                });

                StatusMessage = "Customer saved successfully";
                Log.Information("Saved customer {AccountNumber}", SelectedCustomer.AccountNumber);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save customer {AccountNumber}", SelectedCustomer?.AccountNumber);
                StatusMessage = "Failed to save customer";
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Deletes the selected customer
        /// </summary>
        private async Task ExecuteDeleteCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete customer '{SelectedCustomer.DisplayName}' (Account: {SelectedCustomer.AccountNumber})?\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Deleting customer...";
                HasError = false;
                ErrorMessage = string.Empty;

                await _customerRepository.DeleteAsync(SelectedCustomer.Id);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Remove(SelectedCustomer);
                    SelectedCustomer = Customers.FirstOrDefault();
                    UpdateSummaryText();
                });

                StatusMessage = "Customer deleted successfully";
                Log.Information("Deleted customer {AccountNumber}", SelectedCustomer?.AccountNumber);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete customer {AccountNumber}", SelectedCustomer?.AccountNumber);
                StatusMessage = "Failed to delete customer";
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates the next available account number
        /// </summary>
        private async Task<string> GenerateNextAccountNumberAsync()
        {
            try
            {
                // Generate account number based on current date and sequence
                var datePart = DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture);
                var sequence = (await _customerRepository.GetCountAsync() + 1).ToString("D4", CultureInfo.InvariantCulture);
                return $"{datePart}{sequence}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate next account number, using fallback");
                return DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture); // Fallback to timestamp
            }
        }

        /// <summary>
        /// Updates the summary text based on current data
        /// </summary>
        private void UpdateSummaryText()
        {
            var total = Customers.Count;
            var active = Customers.Count(c => c.Status == CustomerStatus.Active);
            var inactive = total - active;

            SummaryText = $"{total} customers ({active} active, {inactive} inactive)";
        }

        /// <summary>
        /// Clears the search and reloads all customers
        /// </summary>
        private async Task ExecuteClearSearchAsync()
        {
            SearchTerm = string.Empty;
            await ExecuteLoadCustomersAsync();
        }

        /// <summary>
        /// Clears any error state
        /// </summary>
        private void ExecuteClearError()
        {
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Ready";
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
                StatusMessage = "Loading customer bills...";

                // For now, generate sample bills since bill repository might not be implemented
                var bills = GenerateSampleBills(SelectedCustomer.Id);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CustomerBills.Clear();
                    foreach (var bill in bills)
                    {
                        CustomerBills.Add(bill);
                    }
                });

                StatusMessage = $"Loaded {bills.Count} bills for customer";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load customer bills");
                StatusMessage = "Failed to load bills";
                HasError = true;
                ErrorMessage = ex.Message;
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
            // Base rate per 1,000 gallons
            decimal baseRate = location == ServiceLocation.InsideCityLimits ? 3.50m : 5.25m;
            return (gallonsUsed / 1000.0m) * baseRate;
        }

        /// <summary>
        /// Calculates sewer charges based on water usage
        /// </summary>
        public decimal CalculateSewerCharges(int gallonsUsed, ServiceLocation location)
        {
            // Sewer is typically 75% of water charges
            return CalculateWaterCharges(gallonsUsed, location) * 0.75m;
        }

        /// <summary>
        /// Calculates garbage service charges
        /// </summary>
        public decimal CalculateGarbageCharges(CustomerType customerType)
        {
            return customerType switch
            {
                CustomerType.Residential => 25.00m,
                CustomerType.Commercial => 45.00m,
                CustomerType.Industrial => 75.00m,
                _ => 25.00m
            };
        }

        /// <summary>
        /// Pays a selected bill
        /// </summary>
        private async Task ExecutePayBillAsync()
        {
            if (SelectedBill == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Processing payment...";

                // Mark bill as paid
                SelectedBill.Status = BillStatus.Paid;
                SelectedBill.PaidDate = DateTime.Now;

                // Update customer's current balance
                if (SelectedCustomer != null)
                {
                    SelectedCustomer.CurrentBalance -= SelectedBill.TotalAmount;
                    await _customerRepository.UpdateAsync(SelectedCustomer);
                }

                StatusMessage = $"Payment of {SelectedBill.TotalAmount:C} processed successfully";
                Log.Information("Processed payment for bill {BillId}", SelectedBill.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process bill payment");
                StatusMessage = "Failed to process payment";
                HasError = true;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates sample bills for demonstration (replace with actual repository)
        /// </summary>
        private List<UtilityBill> GenerateSampleBills(int customerId)
        {
            var bills = new List<UtilityBill>();
            var random = new Random(customerId);

            for (int i = 0; i < 12; i++)
            {
                var billDate = DateTime.Now.AddMonths(-i);
                var gallonsUsed = random.Next(5000, 15000);
                var location = SelectedCustomer?.ServiceLocation ?? ServiceLocation.InsideCityLimits;

                var waterCharges = CalculateWaterCharges(gallonsUsed, location);
                var sewerCharges = CalculateSewerCharges(gallonsUsed, location);
                var garbageCharges = CalculateGarbageCharges(SelectedCustomer?.CustomerType ?? CustomerType.Residential);

                bills.Add(new UtilityBill
                {
                    Id = customerId * 100 + i,
                    CustomerId = customerId,
                    BillDate = billDate,
                    DueDate = billDate.AddDays(30),
                    WaterUsageGallons = gallonsUsed,
                    WaterCharges = waterCharges,
                    SewerCharges = sewerCharges,
                    GarbageCharges = garbageCharges,
                    Status = random.NextDouble() > 0.3 ? BillStatus.Paid : BillStatus.Pending,
                    AmountPaid = random.NextDouble() > 0.3 ? waterCharges + sewerCharges + garbageCharges : 0,
                    PaidDate = random.NextDouble() > 0.3 ? billDate.AddDays(random.Next(1, 25)) : null
                });
            }

            return bills.OrderByDescending(b => b.BillDate).ToList();
        }

        /// <summary>
        /// Analyzes the selected customer using Grok AI for natural language processing
        /// </summary>
        private async Task ExecuteAnalyzeSelectedCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            try
            {
                IsAnalyzingCustomer = true;
                CustomerAnalysisResult = "Analyzing customer data...";

                var analysisPrompt = $"Analyze this utility customer data and provide insights:\n\n" +
                    $"Name: {SelectedCustomer.DisplayName}\n" +
                    $"Account: {SelectedCustomer.AccountNumber}\n" +
                    $"Type: {SelectedCustomer.CustomerType}\n" +
                    $"Location: {SelectedCustomer.ServiceLocation}\n" +
                    $"Status: {SelectedCustomer.Status}\n" +
                    $"Balance: {SelectedCustomer.CurrentBalance:C}\n" +
                    $"Service Address: {SelectedCustomer.ServiceAddress}, {SelectedCustomer.ServiceCity}, {SelectedCustomer.ServiceState} {SelectedCustomer.ServiceZipCode}\n" +
                    $"Account Open: {SelectedCustomer.AccountOpenDate:d}\n" +
                    $"Notes: {SelectedCustomer.Notes}\n\n" +
                    "Provide a brief analysis of payment patterns, risk factors, and recommendations.";

                var analysis = await _grokSupercomputer.AnalyzeMunicipalDataAsync(analysisPrompt, "Customer Analysis");
                CustomerAnalysisResult = analysis;

                StatusMessage = "Customer analysis completed";
                Log.Information("Completed AI analysis for customer {AccountNumber}", SelectedCustomer.AccountNumber);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to analyze customer {AccountNumber}", SelectedCustomer?.AccountNumber);
                CustomerAnalysisResult = $"Analysis failed: {ex.Message}";
                StatusMessage = "Customer analysis failed";
                HasError = true;
                ErrorMessage = ex.Message;
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
                return _errors.SelectMany(e => e.Value);
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
                        errors.Add("Search term cannot exceed 100 characters");
                    }
                    break;
            }

            if (SelectedCustomer != null)
            {
                ValidateCustomerProperty(propertyName, value, errors);
            }

            if (errors.Any())
            {
                _errors[propertyName] = errors;
            }
            else
            {
                _errors.Remove(propertyName);
            }

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
                        errors.Add("First name is required");
                    }
                    else if ((value as string)?.Length > 50)
                    {
                        errors.Add("First name cannot exceed 50 characters");
                    }
                    break;

                case nameof(SelectedCustomer.LastName):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Last name is required");
                    }
                    else if ((value as string)?.Length > 50)
                    {
                        errors.Add("Last name cannot exceed 50 characters");
                    }
                    break;

                case nameof(SelectedCustomer.AccountNumber):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Account number is required");
                    }
                    else if ((value as string)?.Length > 20)
                    {
                        errors.Add("Account number cannot exceed 20 characters");
                    }
                    break;

                case nameof(SelectedCustomer.ServiceAddress):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Service address is required");
                    }
                    else if ((value as string)?.Length > 100)
                    {
                        errors.Add("Service address cannot exceed 100 characters");
                    }
                    break;

                case nameof(SelectedCustomer.ServiceCity):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Service city is required");
                    }
                    else if ((value as string)?.Length > 50)
                    {
                        errors.Add("Service city cannot exceed 50 characters");
                    }
                    break;

                case nameof(SelectedCustomer.ServiceState):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Service state is required");
                    }
                    else if ((value as string)?.Length != 2)
                    {
                        errors.Add("Service state must be exactly 2 characters");
                    }
                    break;

                case nameof(SelectedCustomer.ServiceZipCode):
                    if (string.IsNullOrWhiteSpace(value as string))
                    {
                        errors.Add("Service ZIP code is required");
                    }
                    else if (!System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "", @"^\d{5}(-\d{4})?$"))
                    {
                        errors.Add("Service ZIP code must be in format 12345 or 12345-1234");
                    }
                    break;

                case nameof(SelectedCustomer.PhoneNumber):
                    if (!string.IsNullOrWhiteSpace(value as string) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "", @"^[\d\s\-\(\)\+\.]{10,20}$"))
                    {
                        errors.Add("Phone number format is invalid");
                    }
                    break;

                case nameof(SelectedCustomer.EmailAddress):
                    if (!string.IsNullOrWhiteSpace(value as string) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(value as string ?? "",
                            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                    {
                        errors.Add("Email address format is invalid");
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
                ValidateProperty(e.PropertyName, SelectedCustomer.GetType().GetProperty(e.PropertyName)?.GetValue(SelectedCustomer));
            }
        }

        /// <summary>
        /// Validates all properties of the selected customer
        /// </summary>
        private void ValidateAllCustomerProperties()
        {
            if (SelectedCustomer == null) return;

            ValidateProperty(nameof(SelectedCustomer.FirstName), SelectedCustomer.FirstName);
            ValidateProperty(nameof(SelectedCustomer.LastName), SelectedCustomer.LastName);
            ValidateProperty(nameof(SelectedCustomer.AccountNumber), SelectedCustomer.AccountNumber);
            ValidateProperty(nameof(SelectedCustomer.ServiceAddress), SelectedCustomer.ServiceAddress);
            ValidateProperty(nameof(SelectedCustomer.ServiceCity), SelectedCustomer.ServiceCity);
            ValidateProperty(nameof(SelectedCustomer.ServiceState), SelectedCustomer.ServiceState);
            ValidateProperty(nameof(SelectedCustomer.ServiceZipCode), SelectedCustomer.ServiceZipCode);
            ValidateProperty(nameof(SelectedCustomer.PhoneNumber), SelectedCustomer.PhoneNumber);
            ValidateProperty(nameof(SelectedCustomer.EmailAddress), SelectedCustomer.EmailAddress);
        }

        /// <summary>
        /// Clears validation errors related to customer properties
        /// </summary>
        private void ClearCustomerErrors()
        {
            var customerProperties = new[]
            {
                nameof(SelectedCustomer.FirstName),
                nameof(SelectedCustomer.LastName),
                nameof(SelectedCustomer.AccountNumber),
                nameof(SelectedCustomer.ServiceAddress),
                nameof(SelectedCustomer.ServiceCity),
                nameof(SelectedCustomer.ServiceState),
                nameof(SelectedCustomer.ServiceZipCode),
                nameof(SelectedCustomer.PhoneNumber),
                nameof(SelectedCustomer.EmailAddress)
            };

            foreach (var property in customerProperties)
            {
                _errors.Remove(property);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
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
            }
        }

        #endregion
    }
}
