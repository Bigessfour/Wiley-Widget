using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.WinForms.Models;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for BudgetAnalyticsPanel providing budget variance analytics,
/// department performance metrics, and financial forecasting.
/// </summary>
public partial class BudgetAnalyticsViewModel : ObservableObject, IBudgetAnalyticsViewModel
{
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string selectedDepartment = "All";

    [ObservableProperty]
    private string selectedDateRange = "Current Year";

    [ObservableProperty]
    private ObservableCollection<BudgetAnalyticsData> analyticsData = new();

    [ObservableProperty]
    private ObservableCollection<string> availableDepartments = new();

    [ObservableProperty]
    private ObservableCollection<string> availableDateRanges = new();

    [ObservableProperty]
    private DateTime lastRefreshTime = DateTime.Now;

    /// <summary>
    /// Initializes a new instance of the BudgetAnalyticsViewModel class.
    /// </summary>
    public BudgetAnalyticsViewModel()
    {
        // Initialize available options
        AvailableDepartments.Add("All");
        AvailableDepartments.Add("Administration");
        AvailableDepartments.Add("Operations");
        AvailableDepartments.Add("Marketing");
        AvailableDepartments.Add("Sales");

        AvailableDateRanges.Add("Current Year");
        AvailableDateRanges.Add("Last 12 Months");
        AvailableDateRanges.Add("Last Quarter");
        AvailableDateRanges.Add("Last Month");
    }

    [RelayCommand(CanExecute = nameof(CanLoadData))]
    public async Task LoadData()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            AnalyticsData.Clear();

            // Load sample data (replace with actual service call)
            var sampleData = GenerateSampleAnalyticsData();
            foreach (var item in sampleData)
            {
                AnalyticsData.Add(item);
            }

            LastRefreshTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load analytics data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task Refresh()
    {
        await LoadData();
    }

    [RelayCommand(CanExecute = nameof(CanFilterData))]
    public void FilterData()
    {
        // Implement filtering based on SelectedDepartment and SelectedDateRange
        // This would typically reload the analytics data with applied filters
    }

    private bool CanLoadData() => !IsLoading;
    private bool CanRefresh() => !IsLoading;
    private bool CanFilterData() => !IsLoading;

    private List<BudgetAnalyticsData> GenerateSampleAnalyticsData()
    {
        var data = new List<BudgetAnalyticsData>();

        string[] departments = { "Administration", "Operations", "Marketing", "Sales" };
        string[] periods = { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

        var random = new Random(42);

        foreach (var dept in departments)
        {
            foreach (var period in periods)
            {
                var budgeted = random.Next(50000, 200000);
                var actual = random.Next((int)(budgeted * 0.8), (int)(budgeted * 1.2));
                var variance = actual - budgeted;
                var variancePercent = ((double)variance / budgeted * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

                data.Add(new BudgetAnalyticsData
                {
                    DepartmentName = dept,
                    PeriodName = period,
                    BudgetedAmount = budgeted,
                    ActualAmount = actual,
                    VarianceAmount = variance,
                    VariancePercent = variancePercent,
                    Status = variance > 0 ? "Over Budget" : "Under Budget"
                });
            }
        }

        return data;
    }
}
