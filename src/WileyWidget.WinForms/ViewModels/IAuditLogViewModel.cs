#nullable enable

using System;
using System.Collections.ObjectModel;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Interface for the Audit Log ViewModel.
    /// Defines the contract for audit entry management with filtering, pagination, and charting.
    /// </summary>
    public interface IAuditLogViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>Observable collection of audit entries for data binding.</summary>
        ObservableCollection<AuditEntry> Entries { get; }

        /// <summary>Observable collection of chart points for chart display.</summary>
        ObservableCollection<AuditLogViewModel.AuditChartPoint> ChartData { get; }

        /// <summary>Indicates whether grid data is currently being loaded.</summary>
        bool IsLoading { get; set; }

        /// <summary>Indicates whether chart data is currently being loaded.</summary>
        bool IsChartLoading { get; set; }

        /// <summary>Error message to display to the user.</summary>
        string? ErrorMessage { get; set; }

        /// <summary>Start date for filtering audit entries.</summary>
        DateTime StartDate { get; set; }

        /// <summary>End date for filtering audit entries.</summary>
        DateTime EndDate { get; set; }

        /// <summary>Selected action type for filtering (null for all).</summary>
        string? SelectedActionType { get; set; }

        /// <summary>Selected user for filtering (null for all).</summary>
        string? SelectedUser { get; set; }

        /// <summary>Number of entries to skip for pagination.</summary>
        int Skip { get; set; }

        /// <summary>Number of entries to take for pagination.</summary>
        int Take { get; set; }

        /// <summary>Total events in the current chart dataset.</summary>
        int TotalEvents { get; }

        /// <summary>Peak events in a single period in the current chart dataset.</summary>
        int PeakEvents { get; }

        /// <summary>Timestamp when chart was last updated.</summary>
        DateTime LastChartUpdated { get; }

        /// <summary>Controls how audit entries are grouped for charting.</summary>
        AuditLogViewModel.ChartGroupingPeriod ChartGrouping { get; set; }
    }
}
