using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class BudgetViewModel : ObservableRecipient
    {
        private readonly ILogger<BudgetViewModel> _logger;

        [ObservableProperty]
        private string title = "Budget Management";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<BudgetItem> budgetItems = new();

        public BudgetViewModel(ILogger<BudgetViewModel> logger)
        {
            _logger = logger;

            LoadBudgetCommand = new AsyncRelayCommand(LoadBudgetAsync);
            SaveBudgetCommand = new AsyncRelayCommand(SaveBudgetAsync);
        }

        public IAsyncRelayCommand LoadBudgetCommand { get; }
        public IAsyncRelayCommand SaveBudgetCommand { get; }

        private async Task LoadBudgetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading budget data");

                // Placeholder for budget loading logic
                BudgetItems.Clear();
                BudgetItems.Add(new BudgetItem { Name = "Sample Budget Item", Amount = 1000.00m });

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

        private async Task SaveBudgetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Saving budget data");

                // Placeholder for budget saving logic

                _logger.LogInformation("Budget data saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save budget data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Budget View");
            LoadBudgetCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Budget View");
        }
    }

    public class BudgetItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}