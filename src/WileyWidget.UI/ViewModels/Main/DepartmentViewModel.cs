using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Messages;
using WileyWidget.Abstractions;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// ViewModel for managing municipal departments with full CRUD operations
/// </summary>
public class DepartmentViewModel : BindableBase, IDataErrorInfo, IDisposable, INavigationAware
{
    // Dependencies
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEventAggregator _eventAggregator;
    private readonly IDispatcherHelper _dispatcherHelper;
    private readonly ICacheService? _cacheService;

    // Disposable resources
    private readonly List<IDisposable> _disposables = new();

    // Core state
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    // Primary data
    public ObservableCollection<Department> DepartmentList { get; } = new();

    private Department _selectedDepartment;
    public Department SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (!EqualityComparer<Department>.Default.Equals(_selectedDepartment, value))
            {
                _selectedDepartment = value;
                RaisePropertyChanged();
                UpdateCommandStates();
            }
        }
    }

    // Loading state
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                RaisePropertyChanged();
            }
        }
    }

    // Search and filter
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                RaisePropertyChanged();
                FilterDepartments();
            }
        }
    }

    // Filtered collection for display
    public ObservableCollection<Department> FilteredDepartmentList { get; } = new();

    // Constructor
    public DepartmentViewModel(
        IDepartmentRepository departmentRepository,
        IEventAggregator eventAggregator,
        IDispatcherHelper dispatcherHelper,
        ICacheService? cacheService = null)
    {
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _dispatcherHelper = dispatcherHelper ?? throw new ArgumentNullException(nameof(dispatcherHelper));
        _cacheService = cacheService;

        InitializeCommands();
    }

    private void InitializeCommands()
    {
        LoadDepartmentsCommand = new DelegateCommand(async () => await LoadDepartmentsAsync());
        AddDepartmentCommand = new DelegateCommand(async () => await AddDepartmentAsync(), CanAddDepartment);
        SaveDepartmentCommand = new DelegateCommand(async () => await SaveDepartmentAsync(), CanSaveDepartment);
        DeleteDepartmentCommand = new DelegateCommand(async () => await DeleteDepartmentAsync(), CanDeleteDepartment);
        ClearSearchCommand = new DelegateCommand(ClearSearch);
        RefreshCommand = new DelegateCommand(async () => await LoadDepartmentsAsync());
        ExportToExcelCommand = new DelegateCommand(async () => await ExportToExcelAsync());
        ExportToCsvCommand = new DelegateCommand(async () => await ExportToCsvAsync());
        ExportSelectionCommand = new DelegateCommand(async () => await ExportSelectionAsync(), CanExportSelection);
        ClearFiltersCommand = new DelegateCommand(ClearFilters);
        ClearGroupingCommand = new DelegateCommand(ClearGrouping);
        CopyToClipboardCommand = new DelegateCommand(async () => await CopyToClipboardAsync());
    }

    // Commands
    public DelegateCommand LoadDepartmentsCommand { get; private set; }
    public DelegateCommand AddDepartmentCommand { get; private set; }
    public DelegateCommand SaveDepartmentCommand { get; private set; }
    public DelegateCommand DeleteDepartmentCommand { get; private set; }
    public DelegateCommand ClearSearchCommand { get; private set; }
    public DelegateCommand RefreshCommand { get; private set; }
    public DelegateCommand ExportToExcelCommand { get; private set; }
    public DelegateCommand ExportToCsvCommand { get; private set; }
    public DelegateCommand ExportSelectionCommand { get; private set; }
    public DelegateCommand ClearFiltersCommand { get; private set; }
    public DelegateCommand ClearGroupingCommand { get; private set; }
    public DelegateCommand CopyToClipboardCommand { get; private set; }

    // Command predicates
    private bool CanAddDepartment() => !IsLoading;
    private bool CanSaveDepartment() => !IsLoading && SelectedDepartment != null && !string.IsNullOrWhiteSpace(SelectedDepartment.Name);
    private bool CanDeleteDepartment() => !IsLoading && SelectedDepartment != null;

    private void UpdateCommandStates()
    {
        AddDepartmentCommand.RaiseCanExecuteChanged();
        SaveDepartmentCommand.RaiseCanExecuteChanged();
        DeleteDepartmentCommand.RaiseCanExecuteChanged();
    }

    // Main operations
    private async Task LoadDepartmentsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading departments...";
            ErrorMessage = string.Empty;

            var departments = await _departmentRepository.GetAllAsync();
            var departmentList = departments?.ToList() ?? new List<Department>();

            await _dispatcherHelper.InvokeAsync(() =>
            {
                DepartmentList.Clear();
                foreach (var dept in departmentList.OrderBy(d => d.Name))
                {
                    DepartmentList.Add(dept);
                }
                FilterDepartments();
            });

            StatusMessage = $"Loaded {DepartmentList.Count} departments";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load departments");
            ErrorMessage = $"Failed to load departments: {ex.Message}";
            StatusMessage = "Error loading departments";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddDepartmentAsync()
    {
        try
        {
            var newDepartment = new Department
            {
                Name = "New Department",
                DepartmentCode = string.Empty
            };

            await _departmentRepository.AddAsync(newDepartment);

            await _dispatcherHelper.InvokeAsync(() =>
            {
                DepartmentList.Add(newDepartment);
                SelectedDepartment = newDepartment;
                FilterDepartments();
            });

            StatusMessage = "Department added successfully";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add department");
            ErrorMessage = $"Failed to add department: {ex.Message}";
        }
    }

    private async Task SaveDepartmentAsync()
    {
        if (SelectedDepartment == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Saving department...";
            ErrorMessage = string.Empty;

            await _departmentRepository.UpdateAsync(SelectedDepartment);

            StatusMessage = "Department saved successfully";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save department");
            ErrorMessage = $"Failed to save department: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteDepartmentAsync()
    {
        if (SelectedDepartment == null) return;

        try
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the department '{SelectedDepartment.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Deleting department...";
            ErrorMessage = string.Empty;

            await _departmentRepository.DeleteAsync(SelectedDepartment.Id);

            await _dispatcherHelper.InvokeAsync(() =>
            {
                DepartmentList.Remove(SelectedDepartment);
                SelectedDepartment = DepartmentList.FirstOrDefault();
                FilterDepartments();
            });

            StatusMessage = "Department deleted successfully";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete department");
            ErrorMessage = $"Failed to delete department: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void ClearGrouping()
    {
        // Clear any grouping if implemented
    }

    private void FilterDepartments()
    {
        _dispatcherHelper.Invoke(() =>
        {
            FilteredDepartmentList.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? DepartmentList
                : DepartmentList.Where(d =>
                    d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (d.DepartmentCode?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var dept in filtered.OrderBy(d => d.Name))
            {
                FilteredDepartmentList.Add(dept);
            }
        });
    }

    private async Task ExportToExcelAsync()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"Departments_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await Task.Run(() =>
                {
                    var csvContent = GenerateCsvContent(FilteredDepartmentList);
                    var csvFileName = saveFileDialog.FileName.Replace(".xlsx", ".csv", StringComparison.OrdinalIgnoreCase);
                    System.IO.File.WriteAllText(csvFileName, csvContent);
                });

                StatusMessage = $"Data exported to {saveFileDialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export to Excel");
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task ExportToCsvAsync()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"Departments_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var csvContent = GenerateCsvContent(FilteredDepartmentList);
                await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, csvContent);
                StatusMessage = $"Data exported to {saveFileDialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export to CSV");
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

    private bool CanExportSelection() => FilteredDepartmentList.Any();

    private async Task ExportSelectionAsync()
    {
        // For now, export all filtered items
        await ExportToCsvAsync();
    }

    private async Task CopyToClipboardAsync()
    {
        try
        {
            var csvContent = GenerateCsvContent(FilteredDepartmentList);
            await _dispatcherHelper.InvokeAsync(() =>
            {
                System.Windows.Clipboard.SetText(csvContent);
            });
            StatusMessage = "Data copied to clipboard";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy to clipboard");
            ErrorMessage = $"Copy failed: {ex.Message}";
        }
    }

    private string GenerateCsvContent(IEnumerable<Department> departments)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,Name,DepartmentCode,ParentId");

        foreach (var dept in departments)
        {
            csv.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{dept.Id},\"{dept.Name}\",\"{dept.DepartmentCode}\",{dept.ParentId}");
        }

        return csv.ToString();
    }

    // Navigation
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // Load data when navigated to
        _ = LoadDepartmentsAsync();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // Cleanup if needed
    }

    // IDataErrorInfo implementation
    public string Error => ErrorMessage;

    public string this[string columnName]
    {
        get
        {
            if (SelectedDepartment == null) return string.Empty;

            return columnName switch
            {
                nameof(SelectedDepartment.Name) =>
                    string.IsNullOrWhiteSpace(SelectedDepartment.Name) ? "Department name is required" : string.Empty,
                nameof(SelectedDepartment.DepartmentCode) =>
                    SelectedDepartment.DepartmentCode?.Length > 20 ? "Department code must be 20 characters or less" : string.Empty,
                _ => string.Empty
            };
        }
    }

    // IDisposable
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _disposables.ForEach(d => d.Dispose());
            _disposables.Clear();
        }
    }
}
