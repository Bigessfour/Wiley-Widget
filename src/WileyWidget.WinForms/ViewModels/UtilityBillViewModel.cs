using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for utility bill management with full CRUD operations.
    /// Provides billing, payment tracking, customer management, and filtering capabilities.
    /// </summary>
    public partial class UtilityBillViewModel : ObservableObject, IDisposable
    {
        #region Fields

        private readonly ILogger<UtilityBillViewModel> _logger;
        private readonly IUtilityBillRepository _billRepository;
        private readonly IUtilityCustomerRepository _customerRepository;
        private bool _disposed;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<UtilityBill> utilityBills = new();

        [ObservableProperty]
        private ObservableCollection<UtilityBill> filteredBills = new();

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> customers = new();

        [ObservableProperty]
        private UtilityBill? selectedBill;

        [ObservableProperty]
        private UtilityCustomer? selectedCustomer;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private BillStatus? selectedStatus;

        [ObservableProperty]
        private bool isOverdueOnly;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusText = "Ready";

        [ObservableProperty]
        private string errorMessage = string.Empty;

        // Summary properties
        [ObservableProperty]
        private decimal totalOutstanding;

        [ObservableProperty]
        private int overdueCount;

        [ObservableProperty]
        private decimal totalRevenue;

        [ObservableProperty]
        private int billsThisMonth;

        #endregion

        #region Commands

        /// <summary>Gets the command to load utility bills.</summary>
        public IAsyncRelayCommand LoadBillsCommand { get; }

        /// <summary>Gets the command to load customers.</summary>
        public IAsyncRelayCommand LoadCustomersCommand { get; }

        /// <summary>Gets the command to create a new bill.</summary>
        public IAsyncRelayCommand CreateBillCommand { get; }

        /// <summary>Gets the command to save changes to a bill.</summary>
        public IAsyncRelayCommand SaveBillCommand { get; }

        /// <summary>Gets the command to delete a bill.</summary>
        public IAsyncRelayCommand DeleteBillCommand { get; }

        /// <summary>Gets the command to mark a bill as paid.</summary>
        public IAsyncRelayCommand MarkAsPaidCommand { get; }

        /// <summary>Gets the command to generate a bill report.</summary>
        public IAsyncRelayCommand GenerateReportCommand { get; }

        #endregion

        #region Constructor

        public UtilityBillViewModel(
            IUtilityBillRepository billRepository,
            IUtilityCustomerRepository customerRepository,
            ILogger<UtilityBillViewModel> logger)
        {
            _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LoadBillsCommand = new AsyncRelayCommand(LoadBillsAsync);
            LoadCustomersCommand = new AsyncRelayCommand(LoadCustomersAsync);
            CreateBillCommand = new AsyncRelayCommand(CreateBillAsync, () => SelectedCustomer != null);
            SaveBillCommand = new AsyncRelayCommand(SaveBillAsync, () => SelectedBill != null);
            DeleteBillCommand = new AsyncRelayCommand(DeleteBillAsync, () => SelectedBill != null);
            MarkAsPaidCommand = new AsyncRelayCommand(MarkAsPaidAsync, () => SelectedBill != null && !SelectedBill.IsPaid);
            GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync);

            _logger.LogDebug("UtilityBillViewModel initialized");
        }

        #endregion

        #region Command Implementations

        private async Task LoadBillsAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading utility bills...";
                ErrorMessage = string.Empty;

                _logger.LogInformation("Loading utility bills from repository");

                var bills = await _billRepository.GetAllAsync();
                UtilityBills.Clear();

                foreach (var bill in bills)
                {
                    UtilityBills.Add(bill);
                }

                ApplyFilters();
                UpdateSummary();

                StatusText = $"Loaded {UtilityBills.Count} utility bills";
                _logger.LogInformation("Loaded {Count} utility bills successfully", UtilityBills.Count);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading bills: {ex.Message}";
                StatusText = "Error loading bills";
                _logger.LogError(ex, "Error loading utility bills");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading customers...";

                _logger.LogInformation("Loading customers from repository");

                var customerList = await _customerRepository.GetAllAsync();
                Customers.Clear();

                foreach (var customer in customerList)
                {
                    Customers.Add(customer);
                }

                StatusText = $"Loaded {Customers.Count} customers";
                _logger.LogInformation("Loaded {Count} customers successfully", Customers.Count);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading customers: {ex.Message}";
                StatusText = "Error loading customers";
                _logger.LogError(ex, "Error loading customers");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateBillAsync()
        {
            try
            {
                if (SelectedCustomer == null)
                {
                    ErrorMessage = "Please select a customer first";
                    _logger.LogWarning("Attempted to create bill without selecting customer");
                    return;
                }

                IsLoading = true;
                StatusText = "Creating new bill...";

                var newBill = new UtilityBill
                {
                    CustomerId = SelectedCustomer.Id,
                    Customer = SelectedCustomer,
                    BillNumber = await GenerateUniqueBillNumberAsync(),
                    BillDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    PeriodStartDate = DateTime.Today.AddMonths(-1),
                    PeriodEndDate = DateTime.Today,
                    Status = BillStatus.Pending,
                    WaterCharges = 0m,
                    SewerCharges = 0m,
                    GarbageCharges = 0m,
                    StormwaterCharges = 0m,
                    LateFees = 0m,
                    OtherCharges = 0m,
                    AmountPaid = 0m
                };

                var createdBill = await _billRepository.AddAsync(newBill);
                UtilityBills.Add(createdBill);
                SelectedBill = createdBill;

                ApplyFilters();
                UpdateSummary();

                StatusText = "Bill created successfully";
                _logger.LogInformation("Created new utility bill {BillNumber} for customer {CustomerId}", createdBill.BillNumber, SelectedCustomer.Id);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error creating bill: {ex.Message}";
                StatusText = "Error creating bill";
                _logger.LogError(ex, "Error creating utility bill");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveBillAsync()
        {
            try
            {
                if (SelectedBill == null)
                {
                    ErrorMessage = "No bill selected";
                    return;
                }

                IsLoading = true;
                StatusText = "Saving bill...";

                // Validate unique bill number
                if (await _billRepository.BillNumberExistsAsync(SelectedBill.BillNumber, SelectedBill.Id))
                {
                    ErrorMessage = $"Bill number {SelectedBill.BillNumber} already exists";
                    StatusText = "Validation failed";
                    _logger.LogWarning("Attempted to save bill with duplicate bill number: {BillNumber}", SelectedBill.BillNumber);
                    return;
                }

                var updatedBill = await _billRepository.UpdateAsync(SelectedBill);

                // Update the bill in the collection
                var index = UtilityBills.IndexOf(SelectedBill);
                if (index >= 0)
                {
                    UtilityBills[index] = updatedBill;
                    SelectedBill = updatedBill;
                }

                ApplyFilters();
                UpdateSummary();

                StatusText = "Bill saved successfully";
                _logger.LogInformation("Updated utility bill {BillNumber}", SelectedBill.BillNumber);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error saving bill: {ex.Message}";
                StatusText = "Error saving bill";
                _logger.LogError(ex, "Error saving utility bill {BillNumber}", SelectedBill?.BillNumber);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteBillAsync()
        {
            try
            {
                if (SelectedBill == null)
                {
                    ErrorMessage = "No bill selected";
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete bill {SelectedBill.BillNumber}?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    _logger.LogDebug("Bill deletion cancelled by user");
                    return;
                }

                IsLoading = true;
                StatusText = "Deleting bill...";

                var billNumber = SelectedBill.BillNumber;
                var success = await _billRepository.DeleteAsync(SelectedBill.Id);

                if (success)
                {
                    UtilityBills.Remove(SelectedBill);
                    SelectedBill = null;

                    ApplyFilters();
                    UpdateSummary();

                    StatusText = "Bill deleted successfully";
                    _logger.LogInformation("Deleted utility bill {BillNumber}", billNumber);
                }
                else
                {
                    ErrorMessage = "Failed to delete bill";
                    StatusText = "Delete failed";
                    _logger.LogWarning("Failed to delete utility bill {BillNumber}", billNumber);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting bill: {ex.Message}";
                StatusText = "Error deleting bill";
                _logger.LogError(ex, "Error deleting utility bill {BillNumber}", SelectedBill?.BillNumber);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task MarkAsPaidAsync()
        {
            try
            {
                if (SelectedBill == null)
                {
                    ErrorMessage = "No bill selected";
                    return;
                }

                if (SelectedBill.IsPaid)
                {
                    MessageBox.Show(
                        "This bill is already marked as paid.",
                        "Already Paid",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Prompt for payment amount
                var amountDue = SelectedBill.AmountDue;
                var promptMessage = $"Bill Number: {SelectedBill.BillNumber}\n" +
                                  $"Amount Due: {amountDue:C}\n\n" +
                                  $"Enter payment amount (or leave blank for full payment):";

                string? paymentInput = Microsoft.VisualBasic.Interaction.InputBox(
                    promptMessage,
                    "Record Payment",
                    amountDue.ToString("F2", System.Globalization.CultureInfo.CurrentCulture),
                    -1, -1);

                if (string.IsNullOrWhiteSpace(paymentInput))
                {
                    _logger.LogDebug("Payment entry cancelled by user");
                    return;
                }

                if (!decimal.TryParse(paymentInput, out var paymentAmount) || paymentAmount <= 0)
                {
                    MessageBox.Show(
                        "Please enter a valid payment amount greater than zero.",
                        "Invalid Amount",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (paymentAmount > SelectedBill.TotalAmount)
                {
                    MessageBox.Show(
                        $"Payment amount ({paymentAmount:C}) cannot exceed total bill amount ({SelectedBill.TotalAmount:C}).",
                        "Invalid Amount",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                IsLoading = true;
                StatusText = "Recording payment...";

                var success = await _billRepository.RecordPaymentAsync(SelectedBill.Id, paymentAmount, DateTime.Today);

                if (success)
                {
                    // Reload the bill to get updated values
                    var updatedBill = await _billRepository.GetByIdAsync(SelectedBill.Id);
                    if (updatedBill != null)
                    {
                        var index = UtilityBills.IndexOf(SelectedBill);
                        if (index >= 0)
                        {
                            UtilityBills[index] = updatedBill;
                            SelectedBill = updatedBill;
                        }
                    }

                    ApplyFilters();
                    UpdateSummary();

                    StatusText = $"Payment of {paymentAmount:C} recorded successfully";
                    _logger.LogInformation("Recorded payment of {Amount:C} for utility bill {BillNumber}", paymentAmount, SelectedBill.BillNumber);

                    MessageBox.Show(
                        $"Payment recorded successfully.\n\nAmount Paid: {paymentAmount:C}\nRemaining Balance: {SelectedBill.AmountDue:C}",
                        "Payment Recorded",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    ErrorMessage = "Failed to record payment";
                    StatusText = "Payment recording failed";
                    _logger.LogWarning("Failed to record payment for bill {BillNumber}", SelectedBill.BillNumber);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error recording payment: {ex.Message}";
                StatusText = "Error recording payment";
                _logger.LogError(ex, "Error recording payment for utility bill {BillNumber}", SelectedBill?.BillNumber);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateReportAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Generating report...";

                _logger.LogInformation("Generating utility bill report");

                // Simulate report generation
                await Task.Delay(500);

                // Placeholder for future ExportService integration
                var billCount = FilteredBills.Count;
                var totalAmount = FilteredBills.Sum(b => b.TotalAmount);
                var totalDue = FilteredBills.Sum(b => b.AmountDue);

                var reportMessage = $"Utility Bill Report\n\n" +
                                  $"Total Bills: {billCount}\n" +
                                  $"Total Amount: {totalAmount:C}\n" +
                                  $"Total Outstanding: {totalDue:C}\n" +
                                  $"Overdue Count: {OverdueCount}\n\n" +
                                  $"Export functionality coming soon!";

                MessageBox.Show(
                    reportMessage,
                    "Report Generated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                StatusText = "Report generated";
                _logger.LogInformation("Report generated successfully with {Count} bills", billCount);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error generating report: {ex.Message}";
                StatusText = "Error generating report";
                _logger.LogError(ex, "Error generating utility bill report");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates summary statistics based on current filtered bills.
        /// </summary>

        private void UpdateSummary()
        {
            TotalOutstanding = UtilityBills.Sum(b => b.AmountDue);
            OverdueCount = UtilityBills.Count(b => b.IsOverdue);
            TotalRevenue = UtilityBills.Where(b => b.Status == BillStatus.Paid).Sum(b => b.TotalAmount);
            BillsThisMonth = UtilityBills.Count(b =>
                b.BillDate.Year == DateTime.Today.Year &&
                b.BillDate.Month == DateTime.Today.Month);

            _logger.LogDebug("Summary updated: Outstanding={Outstanding:C}, Overdue={Overdue}, Revenue={Revenue:C}, ThisMonth={ThisMonth}",
                TotalOutstanding, OverdueCount, TotalRevenue, BillsThisMonth);
        }

        /// <summary>
        /// Generates a unique bill number with timestamp.
        /// </summary>
        private async Task<string> GenerateUniqueBillNumberAsync()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var billNumber = $"UB-{timestamp}";

            // Ensure uniqueness
            var counter = 1;
            while (await _billRepository.BillNumberExistsAsync(billNumber))
            {
                billNumber = $"UB-{timestamp}-{counter}";
                counter++;
            }

            return billNumber;
        }

        /// <summary>
        /// Applies search and filter criteria to the bills collection.
        /// </summary>
        private void ApplyFilters()
        {
            try
            {
                var filtered = UtilityBills.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLowerInvariant();
                    filtered = filtered.Where(b =>
                        (b.BillNumber?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (b.Customer?.DisplayName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (b.Customer?.AccountNumber?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                // Apply status filter
                if (SelectedStatus.HasValue)
                {
                    filtered = filtered.Where(b => b.Status == SelectedStatus.Value);
                }

                // Apply overdue filter
                if (IsOverdueOnly)
                {
                    filtered = filtered.Where(b => b.IsOverdue);
                }

                // Update filtered collection
                FilteredBills.Clear();
                foreach (var bill in filtered)
                {
                    FilteredBills.Add(bill);
                }

                _logger.LogDebug("Filters applied: {Count} bills match criteria", FilteredBills.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
            }
        }

        #endregion

        #region Property Change Handlers

        partial void OnSearchTextChanged(string value)
        {
            _logger.LogDebug("Search text changed: {SearchText}", value);
            ApplyFilters();
        }

        partial void OnSelectedStatusChanged(BillStatus? value)
        {
            _logger.LogDebug("Status filter changed: {Status}", value);
            ApplyFilters();
        }

        partial void OnIsOverdueOnlyChanged(bool value)
        {
            _logger.LogDebug("Overdue filter changed: {IsOverdueOnly}", value);
            ApplyFilters();
        }

        partial void OnSelectedCustomerChanged(UtilityCustomer? value)
        {
            CreateBillCommand.NotifyCanExecuteChanged();
            _logger.LogDebug("Selected customer changed: {CustomerId}", value?.Id);
        }

        partial void OnSelectedBillChanged(UtilityBill? value)
        {
            SaveBillCommand.NotifyCanExecuteChanged();
            DeleteBillCommand.NotifyCanExecuteChanged();
            MarkAsPaidCommand.NotifyCanExecuteChanged();
            _logger.LogDebug("Selected bill changed: {BillId}", value?.Id);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogDebug("UtilityBillViewModel disposing");
                    // Dispose managed resources if needed
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
