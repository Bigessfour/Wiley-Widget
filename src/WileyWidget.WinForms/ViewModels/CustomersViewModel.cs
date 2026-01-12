using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the Customers panel. Provides full MVVM functionality with async commands,
    /// observable collections for WinForms binding, filtering, and QuickBooks synchronization.
    /// </summary>
    public partial class CustomersViewModel : ObservableRecipient
    {
        private readonly ILogger<CustomersViewModel> _logger;
        private readonly IUtilityCustomerRepository _repo;
        private readonly IQuickBooksService _quickBooksService;

        #region Observable Properties

        [ObservableProperty]
        private string title = "Customers";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? statusText = "Ready";

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> customers = new();

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> filteredCustomers = new();

        [ObservableProperty]
        private UtilityCustomer? selectedCustomer;

        [ObservableProperty]
        private string? searchText;

        [ObservableProperty]
        private CustomerType? filterCustomerType;

        [ObservableProperty]
        private ServiceLocation? filterServiceLocation;

        [ObservableProperty]
        private CustomerStatus? filterStatus;

        [ObservableProperty]
        private bool showActiveOnly = true;

        // Summary Properties
        [ObservableProperty]
        private int totalCustomers;

        [ObservableProperty]
        private int activeCustomers;

        [ObservableProperty]
        private int residentialCustomers;

        [ObservableProperty]
        private int commercialCustomers;

        [ObservableProperty]
        private decimal totalOutstandingBalance;

        [ObservableProperty]
        private int customersWithBalance;

        [ObservableProperty]
        private string? syncStatusMessage;

        #endregion

        #region Commands

        /// <summary>Gets the command to load all customers.</summary>
        public IAsyncRelayCommand LoadCustomersCommand { get; }

        /// <summary>Gets the command to refresh customers.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Gets the command to search customers.</summary>
        public IAsyncRelayCommand SearchCommand { get; }

        /// <summary>Gets the command to add a new customer.</summary>
        public IAsyncRelayCommand AddCustomerCommand { get; }

        /// <summary>Gets the command to save the current customer.</summary>
        public IAsyncRelayCommand SaveCustomerCommand { get; }

        /// <summary>Gets the command to delete a customer.</summary>
        public IAsyncRelayCommand<int> DeleteCustomerCommand { get; }

        /// <summary>Gets the command to synchronize with QuickBooks.</summary>
        public IAsyncRelayCommand SyncWithQuickBooksCommand { get; }

        /// <summary>Gets the command to clear all filters.</summary>
        public IRelayCommand ClearFiltersCommand { get; }

        /// <summary>Gets the command to export customers to CSV.</summary>
        public IAsyncRelayCommand<string> ExportToCsvCommand { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomersViewModel"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="repo">Customer repository for data operations.</param>
        /// <param name="quickBooksService">QuickBooks service for synchronization.</param>
        public CustomersViewModel(
            ILogger<CustomersViewModel> logger,
            IUtilityCustomerRepository repo,
            IQuickBooksService quickBooksService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));

            // Initialize commands
            LoadCustomersCommand = new AsyncRelayCommand(LoadCustomersAsync);
            RefreshCommand = new AsyncRelayCommand(LoadCustomersAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            AddCustomerCommand = new AsyncRelayCommand(AddCustomerAsync);
            SaveCustomerCommand = new AsyncRelayCommand(SaveSelectedCustomerAsync, CanSaveCustomer);
            DeleteCustomerCommand = new AsyncRelayCommand<int>(DeleteCustomerAsync, CanDeleteCustomer);
            SyncWithQuickBooksCommand = new AsyncRelayCommand(SyncWithQuickBooksAsync);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            ExportToCsvCommand = new AsyncRelayCommand<string>(ExportToCsvAsync);

            // Wire up property change notifications for filtering
            PropertyChanged += OnPropertyChangedForFiltering;

            _logger.LogInformation("CustomersViewModel constructed");
        }

        #region Initialization

        /// <summary>
        /// Initializes the view model by loading customer data.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Initializing CustomersViewModel");
            await LoadCustomersAsync(ct);
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads all customers from the repository.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoadCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusText = "Loading customers...";

                _logger.LogDebug("Loading customers from repository");

                var list = await _repo.GetAllAsync();

                Customers.Clear();
                foreach (var c in list)
                {
                    Customers.Add(c);
                }

                // Apply filters and update summaries
                ApplyFilters();
                UpdateSummaries();

                StatusText = $"Loaded {Customers.Count} customers";
                _logger.LogInformation("Loaded {Count} customers", Customers.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("LoadCustomersAsync canceled");
                StatusText = "Loading canceled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load customers");

                // Fallback to sample data
                LoadSampleData();
                ApplyFilters();
                UpdateSummaries();

                ErrorMessage = $"Failed to load customers: {ex.Message}. Showing sample data.";
                StatusText = "Error loading - showing sample data";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads realistic sample data as a fallback.
        /// </summary>
        private void LoadSampleData()
        {
            _logger.LogWarning("Loading sample customer data");

            Customers.Clear();
            var sampleCustomers = new[]
            {
                new UtilityCustomer
                {
                    Id = 1,
                    AccountNumber = "10001",
                    FirstName = "John",
                    LastName = "Smith",
                    ServiceAddress = "123 Main St",
                    ServiceCity = "Springfield",
                    ServiceState = "IL",
                    ServiceZipCode = "62701",
                    PhoneNumber = "(217) 555-0123",
                    EmailAddress = "john.smith@email.com",
                    CustomerType = CustomerType.Residential,
                    ServiceLocation = ServiceLocation.InsideCityLimits,
                    Status = CustomerStatus.Active,
                    AccountOpenDate = DateTime.Now.AddYears(-3),
                    CurrentBalance = 125.50m
                },
                new UtilityCustomer
                {
                    Id = 2,
                    AccountNumber = "10002",
                    FirstName = "Sarah",
                    LastName = "Johnson",
                    CompanyName = "Johnson Hardware",
                    ServiceAddress = "456 Oak Ave",
                    ServiceCity = "Springfield",
                    ServiceState = "IL",
                    ServiceZipCode = "62702",
                    PhoneNumber = "(217) 555-0456",
                    EmailAddress = "sarah@johnsonhardware.com",
                    CustomerType = CustomerType.Commercial,
                    ServiceLocation = ServiceLocation.InsideCityLimits,
                    Status = CustomerStatus.Active,
                    AccountOpenDate = DateTime.Now.AddYears(-5),
                    CurrentBalance = 875.00m
                },
                new UtilityCustomer
                {
                    Id = 3,
                    AccountNumber = "10003",
                    FirstName = "Robert",
                    LastName = "Williams",
                    ServiceAddress = "789 County Rd 400",
                    ServiceCity = "Springfield",
                    ServiceState = "IL",
                    ServiceZipCode = "62703",
                    PhoneNumber = "(217) 555-0789",
                    CustomerType = CustomerType.Residential,
                    ServiceLocation = ServiceLocation.OutsideCityLimits,
                    Status = CustomerStatus.Active,
                    AccountOpenDate = DateTime.Now.AddYears(-1),
                    CurrentBalance = 0m
                }
            };

            foreach (var customer in sampleCustomers)
            {
                Customers.Add(customer);
            }
        }

        #endregion

        #region Search and Filter

        /// <summary>
        /// Searches customers based on search text.
        /// </summary>
        private async Task SearchAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                StatusText = "Searching...";

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    await LoadCustomersAsync(cancellationToken);
                    return;
                }

                var term = SearchText.Trim();
                _logger.LogDebug("Searching customers with term: {Term}", term);

                var results = await _repo.SearchAsync(term);

                Customers.Clear();
                foreach (var c in results)
                {
                    Customers.Add(c);
                }

                ApplyFilters();
                UpdateSummaries();

                StatusText = $"Found {FilteredCustomers.Count} customers";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search failed");
                ErrorMessage = "Search failed";
                StatusText = "Search error";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Handles property changes for filter-related properties.
        /// </summary>
        private void OnPropertyChangedForFiltering(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SearchText) or nameof(FilterCustomerType) or
                nameof(FilterServiceLocation) or nameof(FilterStatus) or nameof(ShowActiveOnly))
            {
                ApplyFilters();
            }
        }

        /// <summary>
        /// Applies current filters to the customer collection.
        /// </summary>
        private void ApplyFilters()
        {
            var query = Customers.AsEnumerable();

            // Apply status filter
            if (ShowActiveOnly)
            {
                query = query.Where(c => c.Status == CustomerStatus.Active);
            }

            // Apply type filter
            if (FilterCustomerType.HasValue)
            {
                query = query.Where(c => c.CustomerType == FilterCustomerType.Value);
            }

            // Apply location filter
            if (FilterServiceLocation.HasValue)
            {
                query = query.Where(c => c.ServiceLocation == FilterServiceLocation.Value);
            }

            // Apply status filter (when not using ShowActiveOnly)
            if (!ShowActiveOnly && FilterStatus.HasValue)
            {
                query = query.Where(c => c.Status == FilterStatus.Value);
            }

            // Apply text search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLowerInvariant();
                query = query.Where(c =>
                    c.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.AccountNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.ServiceAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.PhoneNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            FilteredCustomers.Clear();
            foreach (var customer in query)
            {
                FilteredCustomers.Add(customer);
            }

            _logger.LogDebug("Filtered to {Count} customers", FilteredCustomers.Count);
        }

        /// <summary>
        /// Clears all active filters.
        /// </summary>
        private void ClearFilters()
        {
            SearchText = null;
            FilterCustomerType = null;
            FilterServiceLocation = null;
            FilterStatus = null;
            ShowActiveOnly = true;

            ApplyFilters();
            StatusText = "Filters cleared";
        }

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Creates a new customer with default values.
        /// </summary>
        private Task AddCustomerAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Adding new customer");

            var newCustomer = new UtilityCustomer
            {
                AccountNumber = GenerateNextAccountNumber(),
                AccountOpenDate = DateTime.Now,
                Status = CustomerStatus.Active,
                CustomerType = CustomerType.Residential,
                ServiceLocation = ServiceLocation.InsideCityLimits,
                ServiceState = "IL"
            };

            Customers.Insert(0, newCustomer);
            SelectedCustomer = newCustomer;
            ApplyFilters();

            StatusText = "New customer created - please fill in details";
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates the next available account number.
        /// </summary>
        private string GenerateNextAccountNumber()
        {
            if (!Customers.Any()) return "10001";

            var maxAccount = Customers
                .Select(c => c.AccountNumber)
                .Where(a => int.TryParse(a, out _))
                .Select(a => int.Parse(a, System.Globalization.CultureInfo.InvariantCulture))
                .DefaultIfEmpty(10000)
                .Max();

            return (maxAccount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Saves the currently selected customer.
        /// </summary>
        private async Task SaveSelectedCustomerAsync(CancellationToken cancellationToken = default)
        {
            if (SelectedCustomer == null) return;

            await SaveCustomerAsync(SelectedCustomer);
        }

        /// <summary>
        /// Saves a customer to the repository.
        /// </summary>
        /// <param name="customer">The customer to save.</param>
        /// <returns>True if save succeeded, false otherwise.</returns>
        public async Task<bool> SaveCustomerAsync(UtilityCustomer customer)
        {
            ArgumentNullException.ThrowIfNull(customer);

            IsLoading = true;
            StatusText = "Saving customer...";

            try
            {
                _logger.LogDebug("Saving customer {Account}", customer.AccountNumber);

                // Validate account number uniqueness
                if (customer.Id == 0 || !string.IsNullOrEmpty(customer.AccountNumber))
                {
                    var exists = await _repo.ExistsByAccountNumberAsync(customer.AccountNumber, customer.Id);
                    if (exists)
                    {
                        ErrorMessage = $"Account number {customer.AccountNumber} already exists";
                        StatusText = "Save failed - duplicate account number";
                        return false;
                    }
                }

                if (customer.Id == 0)
                {
                    var added = await _repo.AddAsync(customer);

                    // Replace placeholder with persisted instance
                    var idx = Customers.IndexOf(customer);
                    if (idx >= 0)
                    {
                        Customers[idx] = added;
                    }

                    _logger.LogInformation("Added new customer {Id} - {Account}", added.Id, added.AccountNumber);
                }
                else
                {
                    await _repo.UpdateAsync(customer);
                    _logger.LogInformation("Updated customer {Id} - {Account}", customer.Id, customer.AccountNumber);
                }

                await LoadCustomersAsync();
                StatusText = "Customer saved successfully";
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save customer {Account}", customer.AccountNumber);
                ErrorMessage = $"Failed to save customer: {ex.Message}";
                StatusText = "Save failed";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Deletes a customer by ID.
        /// </summary>
        /// <param name="id">Customer ID.</param>
        /// <returns>True if delete succeeded, false otherwise.</returns>
        public async Task<bool> DeleteCustomerAsync(int id)
        {
            IsLoading = true;
            StatusText = "Deleting customer...";

            try
            {
                _logger.LogDebug("Deleting customer {Id}", id);

                var ok = await _repo.DeleteAsync(id);
                if (ok)
                {
                    var existing = Customers.FirstOrDefault(c => c.Id == id);
                    if (existing != null)
                    {
                        Customers.Remove(existing);
                    }

                    ApplyFilters();
                    UpdateSummaries();

                    StatusText = "Customer deleted successfully";
                    _logger.LogInformation("Deleted customer {Id}", id);
                    return true;
                }

                ErrorMessage = "Customer not found";
                StatusText = "Delete failed - customer not found";
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete customer {Id}", id);
                ErrorMessage = $"Failed to delete customer: {ex.Message}";
                StatusText = "Delete failed";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Determines if the current customer can be saved.
        /// </summary>
        private bool CanSaveCustomer() => SelectedCustomer != null && !IsLoading;

        /// <summary>
        /// Determines if a customer can be deleted.
        /// </summary>
        private bool CanDeleteCustomer(int id) => id > 0 && !IsLoading;

        #endregion

        #region QuickBooks Synchronization

        /// <summary>
        /// Synchronizes customers with QuickBooks.
        /// </summary>
        private async Task SyncWithQuickBooksAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                StatusText = "Syncing with QuickBooks...";
                SyncStatusMessage = "Connecting to QuickBooks...";

                _logger.LogInformation("Starting QuickBooks customer sync");

                // Check connection
                var isConnected = await _quickBooksService.IsConnectedAsync();
                if (!isConnected)
                {
                    SyncStatusMessage = "Not connected to QuickBooks. Please connect first.";
                    ErrorMessage = "QuickBooks is not connected";
                    StatusText = "Sync failed - not connected";
                    return;
                }

                SyncStatusMessage = "Fetching QuickBooks customers...";
                var qbCustomers = await _quickBooksService.GetCustomersAsync();

                SyncStatusMessage = $"Retrieved {qbCustomers.Count} customers from QuickBooks";
                _logger.LogInformation("Retrieved {Count} customers from QuickBooks", qbCustomers.Count);

                // Here you would implement the actual sync logic
                // This is a simplified example
                int syncCount = 0;
                foreach (var qbCustomer in qbCustomers)
                {
                    // Check if customer exists
                    var existing = await _repo.GetByAccountNumberAsync(qbCustomer.Id);
                    if (existing == null)
                    {
                        // Create new customer from QuickBooks data
                        var newCustomer = MapQuickBooksCustomer(qbCustomer);
                        await _repo.AddAsync(newCustomer);
                        syncCount++;
                    }
                }

                SyncStatusMessage = $"Synced {syncCount} customers from QuickBooks";
                StatusText = $"QuickBooks sync complete: {syncCount} customers synced";

                await LoadCustomersAsync(cancellationToken);

                _logger.LogInformation("QuickBooks sync completed: {Count} customers synced", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickBooks sync failed");
                ErrorMessage = $"QuickBooks sync failed: {ex.Message}";
                SyncStatusMessage = "Sync failed";
                StatusText = "Sync error";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Maps a QuickBooks customer to a UtilityCustomer.
        /// </summary>
        private UtilityCustomer MapQuickBooksCustomer(Intuit.Ipp.Data.Customer qbCustomer)
        {
            var customer = new UtilityCustomer
            {
                AccountNumber = qbCustomer.Id ?? "UNKNOWN",
                FirstName = qbCustomer.GivenName ?? "",
                LastName = qbCustomer.FamilyName ?? "",
                CompanyName = qbCustomer.CompanyName,
                PhoneNumber = qbCustomer.PrimaryPhone?.FreeFormNumber,
                EmailAddress = qbCustomer.PrimaryEmailAddr?.Address,
                AccountOpenDate = DateTime.Now,
                Status = qbCustomer.Active ? CustomerStatus.Active : CustomerStatus.Inactive,
                CustomerType = CustomerType.Residential,
                ServiceLocation = ServiceLocation.InsideCityLimits
            };

            // Map billing address
            if (qbCustomer.BillAddr != null)
            {
                customer.ServiceAddress = qbCustomer.BillAddr.Line1 ?? "";
                customer.ServiceCity = qbCustomer.BillAddr.City ?? "";
                customer.ServiceState = qbCustomer.BillAddr.CountrySubDivisionCode ?? "IL";
                customer.ServiceZipCode = qbCustomer.BillAddr.PostalCode ?? "";
            }

            return customer;
        }

        #endregion

        #region Summary Calculations

        /// <summary>
        /// Updates summary statistics.
        /// </summary>
        private void UpdateSummaries()
        {
            TotalCustomers = Customers.Count;
            ActiveCustomers = Customers.Count(c => c.Status == CustomerStatus.Active);
            ResidentialCustomers = Customers.Count(c => c.CustomerType == CustomerType.Residential);
            CommercialCustomers = Customers.Count(c => c.CustomerType == CustomerType.Commercial);
            TotalOutstandingBalance = Customers.Sum(c => c.CurrentBalance);
            CustomersWithBalance = Customers.Count(c => c.CurrentBalance > 0);

            _logger.LogDebug("Updated summaries: Total={Total}, Active={Active}", TotalCustomers, ActiveCustomers);
        }

        #endregion

        #region Export

        /// <summary>
        /// Exports filtered customers to CSV format.
        /// </summary>
        private async Task ExportToCsvAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogWarning("ExportToCsvAsync called with null or empty filePath");
                return;
            }

            try
            {
                IsLoading = true;
                StatusText = "Exporting to CSV...";

                _logger.LogInformation("Exporting {Count} customers to {File}", FilteredCustomers.Count, filePath);

                await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                // Write CSV header
                await writer.WriteLineAsync("Account Number,Display Name,Balance,Status,Last Payment Date");

                // Write data rows
                foreach (var customer in FilteredCustomers)
                {
                    var line = string.Join(",", new[]
                    {
                        $"\"{customer.AccountNumber}\"",
                        $"\"{customer.DisplayName}\"",
                        customer.CurrentBalance.ToString("F2", CultureInfo.InvariantCulture),
                        $"\"{customer.StatusDescription}\"",
                        customer.LastPaymentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""
                    });

                    await writer.WriteLineAsync(line);
                }

                StatusText = $"Exported {FilteredCustomers.Count} customers to CSV";
                _logger.LogInformation("Export completed to {File}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed");
                ErrorMessage = "Export failed";
                StatusText = "Export error";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}
