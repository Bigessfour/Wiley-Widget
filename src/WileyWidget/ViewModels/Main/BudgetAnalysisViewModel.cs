using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Budget Analysis functionality
    /// </summary>
    public class BudgetAnalysisViewModel : BindableBase, INavigationAware
    {
        private string _selectedPeriod;
        private decimal _totalBudget;
        private decimal _totalSpent;
        private decimal _remainingBudget;

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set => SetProperty(ref _selectedPeriod, value);
        }

        public decimal TotalBudget
        {
            get => _totalBudget;
            set => SetProperty(ref _totalBudget, value);
        }

        public decimal TotalSpent
        {
            get => _totalSpent;
            set => SetProperty(ref _totalSpent, value);
        }

        public decimal RemainingBudget
        {
            get => _remainingBudget;
            set => SetProperty(ref _remainingBudget, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("BudgetAnalysisViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("BudgetAnalysisViewModel navigated from");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Synchronous navigation handler
        }
    }
}