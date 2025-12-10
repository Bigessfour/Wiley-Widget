using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the Customers view. Lightweight MVVM wrapper around
    /// <see cref="IUtilityCustomerRepository"/> to provide async commands
    /// and observable collections for WinForms binding.
    /// </summary>
    public partial class CustomersViewModel : ObservableRecipient
    {
        private readonly ILogger<CustomersViewModel> _logger;
        private readonly IUtilityCustomerRepository _repo;

        [ObservableProperty]
        private string title = "Customers";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> customers = new();

        [ObservableProperty]
        private UtilityCustomer? selectedCustomer;

        [ObservableProperty]
        private string? searchText;

        /// <summary>Gets the command to load all customers.</summary>
        public IAsyncRelayCommand LoadCustomersCommand { get; }

        /// <summary>Gets the command to search customers.</summary>
        public IAsyncRelayCommand SearchCommand { get; }

        /// <summary>Gets the command to add a new customer.</summary>
        public IAsyncRelayCommand AddCustomerCommand { get; }

        /// <summary>Gets the command to delete a customer.</summary>
        public IAsyncRelayCommand DeleteCustomerCommand { get; }

        public CustomersViewModel(ILogger<CustomersViewModel> logger, IUtilityCustomerRepository repo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            LoadCustomersCommand = new AsyncRelayCommand(LoadCustomersAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            AddCustomerCommand = new AsyncRelayCommand(AddCustomerAsync);
            DeleteCustomerCommand = new AsyncRelayCommand<int>(async id => await DeleteCustomerAsync(id));

            _logger.LogInformation("CustomersViewModel constructed");
        }

        public async Task LoadCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var list = await _repo.GetAllAsync();

                Customers.Clear();
                foreach (var c in list)
                {
                    Customers.Add(c);
                }

                _logger.LogInformation("Loaded {Count} customers", Customers.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("LoadCustomersAsync canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load customers");
                Customers.Clear();
                ErrorMessage = "Failed to load customers";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                var term = SearchText ?? string.Empty;
                var results = await _repo.SearchAsync(term);
                Customers.Clear();
                foreach (var c in results) Customers.Add(c);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search failed");
                ErrorMessage = "Search failed";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Task AddCustomerAsync(CancellationToken cancellationToken = default)
        {
            // Create an empty customer object for the form to edit
            var c = new UtilityCustomer
            {
                AccountOpenDate = DateTime.Now,
                Status = CustomerStatus.Active,
                CustomerType = CustomerType.Residential,
                ServiceLocation = ServiceLocation.InsideCityLimits
            };

            Customers.Insert(0, c);
            SelectedCustomer = c;
            return Task.CompletedTask;
        }

        public async Task<bool> SaveCustomerAsync(UtilityCustomer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));

            IsLoading = true;
            try
            {
                if (customer.Id == 0)
                {
                    var added = await _repo.AddAsync(customer);
                    // Replace placeholder with persisted instance when possible
                    var idx = Customers.IndexOf(customer);
                    if (idx >= 0)
                    {
                        Customers[idx] = added;
                    }
                }
                else
                {
                    await _repo.UpdateAsync(customer);
                }

                await LoadCustomersAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save customer {Account}", customer?.AccountNumber);
                ErrorMessage = "Failed to save customer";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task<bool> DeleteCustomerAsync(int id)
        {
            IsLoading = true;
            try
            {
                var ok = await _repo.DeleteAsync(id);
                if (ok)
                {
                    var existing = Customers.FirstOrDefault(c => c.Id == id);
                    if (existing != null) Customers.Remove(existing);
                    return true;
                }

                ErrorMessage = "Customer not found";
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete customer {Id}", id);
                ErrorMessage = "Failed to delete customer";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await LoadCustomersAsync(ct);
        }
    }
}
