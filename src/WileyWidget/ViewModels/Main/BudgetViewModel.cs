using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class BudgetViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<BudgetViewModel> _logger;

        private string _title = "Budget Management";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ObservableCollection<BudgetItem> _budgetItems = new();
        public ObservableCollection<BudgetItem> BudgetItems
        {
            get => _budgetItems;
            set => SetProperty(ref _budgetItems, value);
        }

        public BudgetViewModel(ILogger<BudgetViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Budget View");
            LoadBudgetAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Budget View");
        }

        private async Task LoadBudgetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading budget data");
                // Load budget data here
                _logger.LogInformation("Budget data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class BudgetItem
    {
        public string Category { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
    }
}