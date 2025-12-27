using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Threading;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Audit Log panel, providing data binding and commands for audit entry management.
/// Supports filtering, pagination, and async data loading.
/// </summary>
/// <summary>
/// Represents a class for auditlogviewmodel.
/// </summary>
/// <summary>
/// Represents a class for auditlogviewmodel.
/// </summary>
/// <summary>
/// Represents a class for auditlogviewmodel.
/// </summary>
/// <summary>
/// Represents a class for auditlogviewmodel.
/// </summary>
public class AuditLogViewModel : INotifyPropertyChanged
{
    private readonly ILogger<AuditLogViewModel> _logger;
    /// <summary>
    /// Represents the _auditservice.
    /// </summary>
    /// <summary>
    /// Represents the _auditservice.
    /// </summary>
    private readonly IAuditService _auditService;
    /// <summary>
    /// Represents the _isloading.
    /// </summary>

    private bool _isLoading;
    private string? _errorMessage;
    private DateTime _startDate = DateTime.Now.AddDays(-30);
    private DateTime _endDate = DateTime.Now;
    private string? _selectedActionType;
    private string? _selectedUser;
    private int _skip = 0;
    private int _take = 100;

    /// <summary>
    /// Observable collection of audit entries for data binding.
    /// </summary>
    public ObservableCollection<AuditEntry> Entries { get; } = new();

    /// <summary>
    /// Indicates whether data is currently being loaded.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Error message to display to the user.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Start date for filtering audit entries.
    /// </summary>
    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate != value)
            {
                _startDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// End date for filtering audit entries.
    /// </summary>
    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate != value)
            {
                _endDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Selected action type for filtering (null for all).
    /// </summary>
    public string? SelectedActionType
    {
        get => _selectedActionType;
        set
        {
            if (_selectedActionType != value)
            {
                _selectedActionType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Selected user for filtering (null for all).
    /// </summary>
    public string? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (_selectedUser != value)
            {
                _selectedUser = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Number of entries to skip for pagination.
    /// </summary>
    public int Skip
    {
        get => _skip;
        set
        {
            if (_skip != value)
            {
                _skip = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Number of entries to take for pagination.
    /// </summary>
    public int Take
    {
        get => _take;
        set
        {
            if (_take != value)
            {
                _take = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Command to load audit entries asynchronously.
    /// </summary>
    /// <summary>
    /// Gets or sets the loadentriescommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the loadentriescommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the loadentriescommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the loadentriescommand.
    /// </summary>
    public AsyncRelayCommand LoadEntriesCommand { get; }

    /// <summary>
    /// Command to export entries to CSV.
    /// </summary>
    /// <summary>
    /// Gets or sets the exporttocsvcommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the exporttocsvcommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the exporttocsvcommand.
    /// </summary>
    /// <summary>
    /// Gets or sets the exporttocsvcommand.
    /// </summary>
    public RelayCommand ExportToCsvCommand { get; }

    /// <summary>
    /// Initializes a new instance with required dependencies.
    /// </summary>
    public AuditLogViewModel(
        ILogger<AuditLogViewModel> logger,
        IAuditService auditService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

        LoadEntriesCommand = new AsyncRelayCommand(LoadEntriesAsync);
        ExportToCsvCommand = new RelayCommand(ExportToCsv);
    }

    /// <summary>
    /// Loads audit entries asynchronously with current filters and pagination.
    /// </summary>
    public async Task LoadEntriesAsync()
    {
        if (IsLoading) return;

        try
        {
            // Set UI state on UI thread to avoid cross-thread property changed events
            await UiThread.InvokeAsync(() =>
            {
                IsLoading = true;
                ErrorMessage = null;
            });

            _logger.LogInformation("Loading audit entries with filters: StartDate={StartDate}, EndDate={EndDate}, ActionType={ActionType}, User={User}, Skip={Skip}, Take={Take}",
                StartDate, EndDate, SelectedActionType, SelectedUser, Skip, Take);

            // Load filtered entries
            var entries = await _auditService.GetAuditEntriesAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: SelectedActionType,
                user: SelectedUser,
                skip: Skip,
                take: Take);

            // Update collection on UI thread to avoid cross-thread exceptions in bindings
            await UiThread.InvokeAsync(() =>
            {
                Entries.Clear();
                foreach (var entry in entries)
                {
                    Entries.Add(entry);
                }
            });

            _logger.LogInformation("Loaded {Count} audit entries", entries.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load audit entries");
            await UiThread.InvokeAsync(() => ErrorMessage = $"Failed to load audit entries: {ex.Message}");
        }
        finally
        {
            await UiThread.InvokeAsync(() => IsLoading = false);
        }
    }

    /// <summary>
    /// Exports current entries to CSV (placeholder implementation).
    /// </summary>
    /// <summary>
    /// Performs exporttocsv.
    /// </summary>
    private void ExportToCsv()
    {
        // Implementation would go here
        // For now, just log
        _logger.LogInformation("Export to CSV requested");
    }

    /// <summary>
    /// Resets filters to default values.
    /// </summary>
    /// <summary>
    /// Performs resetfilters.
    /// </summary>
    /// <summary>
    /// Performs resetfilters.
    /// </summary>
    /// <summary>
    /// Performs resetfilters.
    /// </summary>
    /// <summary>
    /// Performs resetfilters.
    /// </summary>
    public void ResetFilters()
    {
        StartDate = DateTime.Now.AddDays(-30);
        EndDate = DateTime.Now;
        SelectedActionType = null;
        SelectedUser = null;
        Skip = 0;
    }

    /// <summary>
    /// Checks if there are more entries available for pagination.
    /// </summary>
    public async Task<bool> HasMoreEntriesAsync()
    {
        try
        {
            var count = await _auditService.GetAuditEntriesCountAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: SelectedActionType,
                user: SelectedUser);

            return Skip + Take < count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for more entries");
            return false;
        }
    }

    /// <summary>
    /// Loads the next page of entries.
    /// </summary>
    public async Task LoadNextPageAsync()
    {
        if (await HasMoreEntriesAsync())
        {
            Skip += Take;
            await LoadEntriesAsync();
        }
    }

    /// <summary>
    /// Loads the previous page of entries.
    /// </summary>
    public async Task LoadPreviousPageAsync()
    {
        if (Skip > 0)
        {
            Skip = Math.Max(0, Skip - Take);
            await LoadEntriesAsync();
        }
    }

    /// <summary>
    /// Property changed event for data binding.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises PropertyChanged event for the specified property.
    /// </summary>
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        // Ensure PropertyChanged is raised on UI thread to avoid cross-thread exceptions in controls
        var dispatcher = Program.Services != null
            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services)
            : null;

        if (dispatcher == null || dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return;
        }

        _ = dispatcher.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
}
