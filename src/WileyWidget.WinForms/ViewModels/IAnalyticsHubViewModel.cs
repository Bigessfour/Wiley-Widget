using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// Interface for the Analytics Hub ViewModel.
/// </summary>
public interface IAnalyticsHubViewModel : INotifyPropertyChanged
{
    int SelectedFiscalYear { get; set; }
    ObservableCollection<int> FiscalYears { get; }
    string SearchText { get; set; }

    OverviewTabViewModel Overview { get; }
    TrendsTabViewModel Trends { get; }
    ScenariosTabViewModel Scenarios { get; }
    AdvancedScenariosTabViewModel AdvancedScenarios { get; }
    VariancesTabViewModel Variances { get; }

    IAsyncRelayCommand RefreshAllCommand { get; }
}
