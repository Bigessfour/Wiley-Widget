using System.Threading;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for BudgetAnalyticsPanel providing budget variance analytics,
/// department performance metrics, and financial forecasting.
/// </summary>
public partial class BudgetAnalyticsViewModel : ObservableObject, IBudgetAnalyticsViewModel, ILazyLoadViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether data has been loaded.
    /// </summary>
    [ObservableProperty]
    private bool isDataLoaded;

    public async Task OnVisibilityChangedAsync(bool isVisible)
    {
        if (isVisible && !IsDataLoaded && !IsLoading)
        {
            await LoadDataCommand.ExecuteAsync(null);
            IsDataLoaded = true;
        }
    }
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
        // Initialize available date ranges
        AvailableDateRanges.Add("Current Year");
        AvailableDateRanges.Add("Last 12 Months");
        AvailableDateRanges.Add("Last Quarter");
        AvailableDateRanges.Add("Last Month");

        // Departments will be loaded from data (see LoadData method)
        AvailableDepartments.Add("All"); // Always add 'All' as first option
    }

    [RelayCommand(CanExecute = nameof(CanLoadData))]
    public async Task LoadData(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            AnalyticsData.Clear();

            // Load sample data (replace with actual service call)
            var sampleData = GenerateSampleAnalyticsData();

            // Extract unique departments from data and populate dropdown
            var allDepartments = sampleData
                .Select(x => x.DepartmentName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Update available departments list (preserving 'All' at the start)
            if (AvailableDepartments.Count == 0 || (AvailableDepartments.Count == 1 && AvailableDepartments[0] == "All"))
            {
                // First load - populate from data
                foreach (var dept in allDepartments)
                {
                    if (!AvailableDepartments.Contains(dept))
                        AvailableDepartments.Add(dept);
                }
            }

            // Apply filters and add to view
            var filteredData = ApplyFilters(sampleData);
            foreach (var item in filteredData)
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

    /// <summary>
    /// Applies department and date range filters to the analytics data.
    /// </summary>
    private List<BudgetAnalyticsData> ApplyFilters(List<BudgetAnalyticsData> allData)
    {
        var filtered = allData.AsEnumerable();

        // Apply department filter (unless 'All' is selected)
        if (SelectedDepartment != "All")
        {
            filtered = filtered.Where(x => x.DepartmentName == SelectedDepartment);
        }

        // Apply date range filter (periods)
        filtered = SelectedDateRange switch
        {
            "Last Month" => filtered.Where(x => x.PeriodName == "Dec"),
            "Last Quarter" => filtered.Where(x => new[] { "Oct", "Nov", "Dec" }.Contains(x.PeriodName)),
            "Last 12 Months" => filtered, // All 12 months
            _ => filtered  // "Current Year" - show all
        };

        return filtered.ToList();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task Refresh(CancellationToken cancellationToken = default)
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
