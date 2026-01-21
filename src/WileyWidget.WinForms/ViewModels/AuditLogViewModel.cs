using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Audit Log panel, providing data binding and commands for audit entry management.
/// Supports filtering, pagination, charting, and async data loading.
/// </summary>
public class AuditLogViewModel : INotifyPropertyChanged, IDisposable, ILazyLoadViewModel
{
    private bool _isDataLoaded;
    public bool IsDataLoaded
    {
        get => _isDataLoaded;
        private set
        {
            if (_isDataLoaded != value)
            {
                _isDataLoaded = value;
                OnPropertyChanged(nameof(IsDataLoaded));
            }
        }
    }

    public async Task OnVisibilityChangedAsync(bool isVisible)
    {
        if (isVisible && !IsDataLoaded && !IsLoading)
        {
            await LoadEntriesAsync();
            await LoadChartDataAsync();
            IsDataLoaded = true;
        }
    }

    private readonly ILogger<AuditLogViewModel> _logger;
    private readonly IAuditService _auditService;

    private bool _isLoading;
    private bool _isChartLoading;
    private string? _errorMessage;
    private DateTime _startDate = DateTime.Now.AddDays(-30);
    private DateTime _endDate = DateTime.Now;
    private string? _selectedActionType;
    private string? _selectedUser;
    private int _skip = 0;
    private int _take = 100;

    private int _totalEvents;
    private int _peakEvents;
    private DateTime _lastChartUpdated;
    private ChartGroupingPeriod _chartGrouping = ChartGroupingPeriod.Month;

    private CancellationTokenSource? _chartLoadCancellationTokenSource;

    /// <summary>
    /// Observable collection of audit entries for data binding.
    /// </summary>
    public ObservableCollection<AuditEntry> Entries { get; } = new();

    /// <summary>
    /// Observable collection of chart points for chart display.
    /// </summary>
    public ObservableCollection<AuditChartPoint> ChartData { get; } = new();

    /// <summary>
    /// Indicates whether grid data is currently being loaded.
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
    /// Indicates whether chart data is currently being loaded.
    /// </summary>
    public bool IsChartLoading
    {
        get => _isChartLoading;
        set
        {
            if (_isChartLoading != value)
            {
                _isChartLoading = value;
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
    /// Total events in the current chart dataset.
    /// </summary>
    public int TotalEvents
    {
        get => _totalEvents;
        private set
        {
            if (_totalEvents != value)
            {
                _totalEvents = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Peak events in a single period in the current chart dataset.
    /// </summary>
    public int PeakEvents
    {
        get => _peakEvents;
        private set
        {
            if (_peakEvents != value)
            {
                _peakEvents = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Timestamp when chart was last updated.
    /// </summary>
    public DateTime LastChartUpdated
    {
        get => _lastChartUpdated;
        private set
        {
            if (_lastChartUpdated != value)
            {
                _lastChartUpdated = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Controls how audit entries are grouped for charting.
    /// </summary>
    public ChartGroupingPeriod ChartGrouping
    {
        get => _chartGrouping;
        set
        {
            if (_chartGrouping != value)
            {
                _chartGrouping = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Command to load audit entries asynchronously.
    /// </summary>
    public AsyncRelayCommand LoadEntriesCommand { get; }

    /// <summary>
    /// Command to load chart data asynchronously.
    /// </summary>
    public AsyncRelayCommand LoadChartDataCommand { get; }

    /// <summary>
    /// Command to export entries to CSV.
    /// </summary>
    public RelayCommand<string?> ExportToCsvCommand { get; }

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
        LoadChartDataCommand = new AsyncRelayCommand(LoadChartDataAsync);
        ExportToCsvCommand = new RelayCommand<string?>(ExportToCsv);

        _logger.LogDebug("AuditLogViewModel initialized");
    }

    /// <summary>
    /// Parameterless constructor for design-time/fallback scenarios.
    /// Populates sample chart data for visual design-time preview.
    /// </summary>
    public AuditLogViewModel()
        : this(NullLogger<AuditLogViewModel>.Instance, new FallbackAuditService())
    {
        // Populate sample chart data for design-time preview
        ChartData.Clear();
        foreach (var p in CreateSampleChartData(ChartGrouping, StartDate, EndDate))
            ChartData.Add(p);

        TotalEvents = ChartData.Sum(c => c.Count);
        PeakEvents = ChartData.Any() ? ChartData.Max(c => c.Count) : 0;
        LastChartUpdated = DateTime.Now;
    }

    /// <summary>
    /// Loads audit entries asynchronously with current filters and pagination.
    /// </summary>
    public async Task LoadEntriesAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            _logger.LogInformation("Loading audit entries with filters: StartDate={StartDate}, EndDate={EndDate}, ActionType={ActionType}, User={User}, Skip={Skip}, Take={Take}",
                StartDate, EndDate, SelectedActionType, SelectedUser, Skip, Take);

            // Clear existing entries
            Entries.Clear();

            // Load filtered entries
            var entries = await _auditService.GetAuditEntriesAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: SelectedActionType,
                user: SelectedUser,
                skip: Skip,
                take: Take);

            // Add to observable collection
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            _logger.LogInformation("Loaded {Count} audit entries", entries.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load audit entries");
            ErrorMessage = $"Failed to load audit entries: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads aggregated chart data based on the selected period grouping (Day/Week/Month).
    /// Fetches audit entries for the current filter range and groups them into <see cref="ChartData"/>.
    /// </summary>
    public async Task LoadChartDataAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent chart loads
        if (IsChartLoading) return;

        // Cancel previous chart load
        _chartLoadCancellationTokenSource?.Cancel();
        _chartLoadCancellationTokenSource?.Dispose();
        _chartLoadCancellationTokenSource = new CancellationTokenSource();
        var ct = _chartLoadCancellationTokenSource.Token;

        try
        {
            IsChartLoading = true;
            ErrorMessage = null;

            _logger.LogInformation("Loading chart data: Grouping={Grouping}, StartDate={Start}, EndDate={End}", ChartGrouping, StartDate, EndDate);

            // Fetch all entries for the chart range in a single call (or paging if needed)
            var entries = await _auditService.GetAuditEntriesAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: SelectedActionType,
                user: SelectedUser,
                skip: null,
                take: null);

            if (ct.IsCancellationRequested) return;

            // If no entries, provide a realistic sample dataset as fallback
            if (!entries.Any())
            {
                ChartData.Clear();
                foreach (var p in CreateSampleChartData(ChartGrouping, StartDate, EndDate))
                    ChartData.Add(p);

                TotalEvents = ChartData.Sum(c => c.Count);
                PeakEvents = ChartData.Any() ? ChartData.Max(c => c.Count) : 0;
                LastChartUpdated = DateTime.Now;

                return;
            }

            // Group entries according to the selected grouping
            var groups = entries
                .GroupBy(e => GetGroupingKey(e.Timestamp, ChartGrouping))
                .OrderBy(g => g.Key)
                .Select(g => new AuditChartPoint { Period = g.Key, Count = g.Count() })
                .ToArray();

            ChartData.Clear();
            foreach (var gr in groups)
                ChartData.Add(gr);

            TotalEvents = ChartData.Sum(c => c.Count);
            PeakEvents = ChartData.Any() ? ChartData.Max(c => c.Count) : 0;
            LastChartUpdated = DateTime.Now;

            _logger.LogInformation("Chart data loaded with {Buckets} buckets, TotalEvents={Total}", ChartData.Count, TotalEvents);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Chart data load cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chart data");
            ErrorMessage = $"Failed to load chart data: {ex.Message}";
        }
        finally
        {
            IsChartLoading = false;
        }
    }

    /// <summary>
    /// Exports current entries to CSV file.
    /// </summary>
    /// <param name="filePath">Target file path for CSV export.</param>
    private void ExportToCsv(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("ExportToCsv called with empty file path");
            return;
        }

        try
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            // Write CSV data
            csv.WriteRecords(Entries.Select(e => new
            {
                e.Id,
                Timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                e.User,
                e.Action,
                e.EntityType,
                e.EntityId,
                e.Changes,
                OldValues = e.OldValues ?? string.Empty,
                NewValues = e.NewValues ?? string.Empty
            }));

            _logger.LogInformation("Exported {Count} audit entries to {FilePath}", Entries.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit entries to CSV");
            throw;
        }
    }

    /// <summary>
    /// Gets distinct users from current entries for filter population.
    /// </summary>
    public async Task<List<string>> GetDistinctUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get users from current date range
            var entries = await _auditService.GetAuditEntriesAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: null,
                user: null,
                skip: 0,
                take: 1000); // Get more entries for filter population

            return entries
                .Select(e => e.User)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .OrderBy(u => u)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distinct users");
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets distinct action types from current entries for filter population.
    /// </summary>
    public async Task<List<string>> GetDistinctActionTypesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get actions from current date range
            var entries = await _auditService.GetAuditEntriesAsync(
                startDate: StartDate,
                endDate: EndDate,
                actionType: null,
                user: null,
                skip: 0,
                take: 1000); // Get more entries for filter population

            return entries
                .Select(e => e.Action)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distinct action types");
            return new List<string>();
        }
    }

    /// <summary>
    /// Resets filters to default values.
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
    public async Task<bool> HasMoreEntriesAsync(CancellationToken cancellationToken = default)
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
    public async Task LoadNextPageAsync(CancellationToken cancellationToken = default)
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
    public async Task LoadPreviousPageAsync(CancellationToken cancellationToken = default)
    {
        if (Skip > 0)
        {
            Skip = Math.Max(0, Skip - Take);
            await LoadEntriesAsync();
        }
    }

    /// <summary>
    /// Returns the grouping key (start date) for a given timestamp and grouping option.
    /// </summary>
    private static DateTime GetGroupingKey(DateTime timestamp, ChartGroupingPeriod grouping)
    {
        return grouping switch
        {
            ChartGroupingPeriod.Day => timestamp.Date,
            ChartGroupingPeriod.Week => GetWeekStart(timestamp),
            ChartGroupingPeriod.Month => new DateTime(timestamp.Year, timestamp.Month, 1),
            _ => timestamp.Date,
        };
    }

    /// <summary>
    /// Get the start of the week (Monday as first day) for the provided date.
    /// </summary>
    private static DateTime GetWeekStart(DateTime dt)
    {
        // Monday-based week start
        int diff = ((int)dt.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return dt.Date.AddDays(-diff);
    }

    /// <summary>
    /// Creates realistic sample chart data (deterministic) for design-time and service fallback scenarios.
    /// </summary>
    private static IEnumerable<AuditChartPoint> CreateSampleChartData(ChartGroupingPeriod grouping, DateTime startDate, DateTime endDate)
    {
        var result = new List<AuditChartPoint>();

        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var cursor = (grouping == ChartGroupingPeriod.Month)
            ? new DateTime(startDate.Year, startDate.Month, 1)
            : startDate.Date;

        while (cursor <= endDate.Date)
        {
            var daysSpan = (endDate.Date - startDate.Date).Days + 1;
            // deterministic but varied distribution
            var seed = (int)(cursor.Ticks % 100);
            var count = (seed % 7) + (daysSpan > 30 ? 5 : 1);

            result.Add(new AuditChartPoint { Period = cursor, Count = count });

            cursor = grouping switch
            {
                ChartGroupingPeriod.Day => cursor.AddDays(1),
                ChartGroupingPeriod.Week => cursor.AddDays(7),
                ChartGroupingPeriod.Month => cursor.AddMonths(1),
                _ => cursor.AddDays(1),
            };
        }

        return result.OrderBy(r => r.Period).ToArray();
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Disposes managed resources (cancellation tokens).
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chartLoadCancellationTokenSource?.Cancel();
            _chartLoadCancellationTokenSource?.Dispose();
            _chartLoadCancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Represents a single data point for the audit events chart.
    /// </summary>
    public class AuditChartPoint
    {
        /// <summary>
        /// The start of the period this point represents (date for day, week-start for week, first-of-month for month).
        /// </summary>
        public required DateTime Period { get; init; }

        /// <summary>
        /// Number of events in the period.
        /// </summary>
        public required int Count { get; init; }

        /// <summary>
        /// Friendly label for display; consumer may ignore and format axis labels instead.
        /// </summary>
        public string Label => Period.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// How to group audit entries for chart aggregation.
    /// </summary>
    public enum ChartGroupingPeriod
    {
        Day,
        Week,
        Month
    }

    /// <summary>
    /// Simple fallback implementation of <see cref="IAuditService"/> used for design-time preview.
    /// Not suitable for production; returns deterministic sample entries.
    /// </summary>
    private class FallbackAuditService : IAuditService
    {
        public Task AuditAsync(string eventName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IEnumerable<AuditEntry>> GetAuditEntriesAsync(DateTime? startDate = null, DateTime? endDate = null, string? actionType = null, string? user = null, int? skip = null, int? take = null, CancellationToken cancellationToken = default)
        {
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-14);
            var list = new List<AuditEntry>();

            var rand = new Random(42);
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                var eventsForDay = rand.Next(0, 6);
                for (int i = 0; i < eventsForDay; i++)
                {
                    list.Add(new AuditEntry
                    {
                        Id = list.Count + 1,
                        Action = i % 2 == 0 ? "CREATE" : "UPDATE",
                        User = i % 3 == 0 ? "admin" : "user",
                        Timestamp = d.AddHours(rand.Next(0, 23)).AddMinutes(rand.Next(0, 59)),
                        EntityType = "Record",
                        EntityId = rand.Next(1, 1000),
                        Changes = "Sample change"
                    });
                }
            }

            var result = list.AsEnumerable();
            if (!string.IsNullOrEmpty(actionType)) result = result.Where(r => r.Action.Equals(actionType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(user)) result = result.Where(r => r.User.Equals(user, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(result);
        }

        public Task<int> GetAuditEntriesCountAsync(DateTime? startDate = null, DateTime? endDate = null, string? actionType = null, string? user = null, CancellationToken cancellationToken = default)
        {
            return GetAuditEntriesAsync(startDate, endDate, actionType, user, null, null)
                .ContinueWith(t => t.Result.Count());
        }
    }
}
