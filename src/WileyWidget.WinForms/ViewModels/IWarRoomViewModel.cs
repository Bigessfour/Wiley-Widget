#nullable enable

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Interface for the WarRoomViewModel.
    /// Defines the contract for dependency injection and testability.
    /// </summary>
    public interface IWarRoomViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        string ScenarioInput { get; set; }
        bool IsAnalyzing { get; set; }
        string StatusMessage { get; set; }
        string RequiredRateIncrease { get; set; }
        decimal RiskLevel { get; set; }
        ObservableCollection<ScenarioProjection> Projections { get; }
        ObservableCollection<DepartmentImpact> DepartmentImpacts { get; }
        decimal BaselineMonthlyRevenue { get; set; }
        decimal ProjectedMonthlyRevenue { get; set; }
        decimal RevenueDifference { get; set; }
        bool HasResults { get; set; }

        IAsyncRelayCommand RunScenarioCommand { get; }
        IAsyncRelayCommand ExportForecastCommand { get; }
        IRelayCommand ResetCommand { get; }
    }
}
