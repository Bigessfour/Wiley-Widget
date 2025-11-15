using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Budget Panel - manages budget-related operations
    /// </summary>
    public class BudgetPanelViewModel : BindableBase, INavigationAware
    {
        private decimal _totalBudget;
        private decimal _allocatedBudget;
        private decimal _remainingBudget;

        public decimal TotalBudget
        {
            get => _totalBudget;
            set => SetProperty(ref _totalBudget, value);
        }

        public decimal AllocatedBudget
        {
            get => _allocatedBudget;
            set => SetProperty(ref _allocatedBudget, value);
        }

        public decimal RemainingBudget
        {
            get => _remainingBudget;
            set => SetProperty(ref _remainingBudget, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("BudgetPanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("BudgetPanelViewModel navigated from");
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