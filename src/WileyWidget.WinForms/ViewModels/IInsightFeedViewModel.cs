#nullable enable

using System;
using System.Collections.ObjectModel;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Interface for the Insight Feed ViewModel.
    /// Defines the contract for displaying proactive AI insights in the UI.
    /// Excludes auto-generated RelayCommand properties since they're generated
    /// by MVVM Toolkit and can't be part of the interface contract.
    /// </summary>
    public interface IInsightFeedViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>
        /// Collection of insight cards to display in the grid.
        /// </summary>
        ObservableCollection<InsightCardModel> InsightCards { get; set; }

        /// <summary>
        /// Indicates whether insights are currently loading.
        /// </summary>
        bool IsLoading { get; set; }

        /// <summary>
        /// Status message to display in the UI.
        /// </summary>
        string StatusMessage { get; set; }

        /// <summary>
        /// Count of high-priority insights.
        /// </summary>
        int HighPriorityCount { get; set; }

        /// <summary>
        /// Count of medium-priority insights.
        /// </summary>
        int MediumPriorityCount { get; set; }

        /// <summary>
        /// Count of low-priority insights.
        /// </summary>
        int LowPriorityCount { get; set; }

        /// <summary>
        /// Refreshes the insights data.
        /// </summary>
        System.Threading.Tasks.Task RefreshAsync(System.Threading.CancellationToken ct = default);
    }
}
