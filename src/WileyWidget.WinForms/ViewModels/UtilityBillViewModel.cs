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
    /// Provides billing, payment tracking, and customer management capabilities.
    /// </summary>
    public partial class UtilityBillViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<UtilityBillViewModel> _logger;
        private readonly IUtilityBillRepository _billRepository;
        private readonly IUtilityCustomerRepository _customerRepository;

        [ObservableProperty]
        private ObservableCollection<UtilityBill> utilityBills = new();

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> customers = new();

        [ObservableProperty]
        private UtilityBill? selectedBill;

        [ObservableProperty]
        private UtilityCustomer? selectedCustomer;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private BillStatus? filterStatus;

        [ObservableProperty]
        private bool showOverdueOnly;

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
            CreateBillCommand = new AsyncRelayCommand(CreateBillAsync);
            SaveBillCommand = new AsyncRelayCommand(SaveBillAsync);
            DeleteBillCommand = new AsyncRelayCommand(DeleteBillAsync);
            MarkAsPaidCommand = new AsyncRelayCommand(MarkAsPaidAsync);
            GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync);
        }

        private async Task LoadBillsAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading utility bills...";
                ErrorMessage = string.Empty;

                var bills = await _billRepository.GetAllAsync();
                UtilityBills.Clear();

                foreach (var bill in bills)
                {
                    UtilityBills.Add(bill);
                }

                UpdateSummary();
                StatusText = $"Loaded {UtilityBills.Count} utility bills";
                _logger.LogInformation("Loaded {Count} utility bills", UtilityBills.Count);
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

                var customerList = await _customerRepository.GetAllAsync();
                Customers.Clear();

                foreach (var customer in customerList)
                {
                    Customers.Add(customer);
                }

                StatusText = $"Loaded {Customers.Count} customers";
                _logger.LogInformation("Loaded {Count} customers", Customers.Count);
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
                    return;
                }

                IsLoading = true;
                StatusText = "Creating new bill...";

                var newBill = new UtilityBill
                {
                    CustomerId = SelectedCustomer.Id,
                    Customer = SelectedCustomer,
                    BillNumber = GenerateBillNumber(),
                    BillDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    PeriodStartDate = DateTime.Today.AddMonths(-1),
                    PeriodEndDate = DateTime.Today,
                    Status = BillStatus.Pending
                };

                await _billRepository.AddAsync(newBill);
                UtilityBills.Add(newBill);
                SelectedBill = newBill;

                UpdateSummary();
                StatusText = "Bill created successfully";
                _logger.LogInformation("Created new utility bill {BillNumber}", newBill.BillNumber);
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

                await _billRepository.UpdateAsync(SelectedBill);
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
                    $"Are you sure you want to delete bill {SelectedBill.BillNumber}?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;

                IsLoading = true;
                StatusText = "Deleting bill...";

                var billNumber = SelectedBill.BillNumber;
                await _billRepository.DeleteAsync(SelectedBill.Id);
                UtilityBills.Remove(SelectedBill);
                SelectedBill = null;

                UpdateSummary();
                StatusText = "Bill deleted successfully";
                _logger.LogInformation("Deleted utility bill {BillNumber}", billNumber);
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

                IsLoading = true;
                StatusText = "Marking bill as paid...";

                SelectedBill.Status = BillStatus.Paid;
                SelectedBill.PaidDate = DateTime.Today;
                SelectedBill.AmountPaid = SelectedBill.TotalAmount;

                await _billRepository.UpdateAsync(SelectedBill);
                UpdateSummary();

                StatusText = "Bill marked as paid";
                _logger.LogInformation("Marked utility bill {BillNumber} as paid", SelectedBill.BillNumber);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error marking bill as paid: {ex.Message}";
                StatusText = "Error marking bill as paid";
                _logger.LogError(ex, "Error marking utility bill as paid {BillNumber}", SelectedBill?.BillNumber);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Task GenerateReportAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Generating report...";

                // Report generation logic would go here
                // For now, just show a placeholder message
                StatusText = "Report generation not yet implemented";
                _logger.LogInformation("Report generation requested but not implemented");
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
            return Task.CompletedTask;
        }

        private void UpdateSummary()
        {
            TotalOutstanding = UtilityBills.Sum(b => b.AmountDue);
            OverdueCount = UtilityBills.Count(b => b.IsOverdue);
            TotalRevenue = UtilityBills.Sum(b => b.TotalAmount);
            BillsThisMonth = UtilityBills.Count(b =>
                b.BillDate.Year == DateTime.Today.Year &&
                b.BillDate.Month == DateTime.Today.Month);
        }

        private string GenerateBillNumber()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            return $"UB-{timestamp}";
        }

        partial void OnSearchTextChanged(string value)
        {
            // Filter bills based on search text
            // Implementation would filter the displayed bills
        }

        partial void OnFilterStatusChanged(BillStatus? value)
        {
            // Filter bills by status
            // Implementation would filter the displayed bills
        }

        partial void OnShowOverdueOnlyChanged(bool value)
        {
            // Filter to show only overdue bills
            // Implementation would filter the displayed bills
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up managed resources if needed
            }
            // Clean up unmanaged resources if any
            _logger.LogDebug("UtilityBillViewModel disposed");
        }
    }
}
