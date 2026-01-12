#nullable enable

using System;
using System.Collections.ObjectModel;
using WileyWidget.WinForms.Models;

namespace WileyWidget.ViewModels;

/// <summary>
/// Interface for budget analytics view model operations.
/// </summary>
public interface IBudgetAnalyticsViewModel : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Gets a value indicating whether data is currently loading.</summary>
    bool IsLoading { get; set; }

    /// <summary>Gets the error message, if any.</summary>
    string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the selected department filter.</summary>
    string SelectedDepartment { get; set; }

    /// <summary>Gets or sets the selected date range filter.</summary>
    string SelectedDateRange { get; set; }

    /// <summary>Gets the analytics data collection.</summary>
    ObservableCollection<BudgetAnalyticsData> AnalyticsData { get; set; }

    /// <summary>Gets the available departments for filtering.</summary>
    ObservableCollection<string> AvailableDepartments { get; set; }

    /// <summary>Gets the available date ranges for filtering.</summary>
    ObservableCollection<string> AvailableDateRanges { get; set; }

    /// <summary>Gets the last refresh time.</summary>
    DateTime LastRefreshTime { get; set; }

    /// <summary>Loads analytics data asynchronously.</summary>
    Task LoadData();

    /// <summary>Refreshes analytics data asynchronously.</summary>
    Task Refresh();

    /// <summary>Filters analytics data based on selected criteria.</summary>
    void FilterData();
}
