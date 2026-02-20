using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// Contract for the advanced scenarios tab (vNext) with AI narrative and exports.
/// </summary>
public interface IAdvancedScenariosTabViewModel : INotifyPropertyChanged
{
    decimal RateIncreasePercent { get; set; }
    decimal ExpenseIncreasePercent { get; set; }
    decimal RevenueTargetPercent { get; set; }
    int ProjectionYears { get; set; }

    ObservableCollection<YearlyProjection> Projections { get; }
    ObservableCollection<string> Recommendations { get; }
    string AiNarrative { get; set; }
    string StatusMessage { get; set; }
    bool IsLoading { get; }

    IAsyncRelayCommand RunScenarioCommand { get; }
    IAsyncRelayCommand ExportProjectionsCommand { get; }
    IAsyncRelayCommand ResetCommand { get; }
}
