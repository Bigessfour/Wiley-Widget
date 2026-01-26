using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the Activity Log panel.
    /// Provides an audit trail of recent application navigation and events.
    /// </summary>
    public partial class ActivityLogViewModel : ObservableRecipient
    {
        private readonly ILogger<ActivityLogViewModel> _logger;
        private readonly IActivityLogService? _activityLogService;

        #region Observable Properties

        [ObservableProperty]
        private string title = "Recent Activity";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? statusText = "Ready";

        [ObservableProperty]
        private ObservableCollection<ActivityLog> activityEntries = new();

        #endregion

        #region Commands

        /// <summary>Gets the command to load activity entries.</summary>
        public IAsyncRelayCommand LoadActivityCommand { get; }

        /// <summary>Gets the command to refresh activity entries.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Gets the command to clear all activity entries.</summary>
        public IRelayCommand ClearCommand { get; }

        /// <summary>Gets the command to export activity entries to CSV.</summary>
        public IAsyncRelayCommand<string> ExportToCsvCommand { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityLogViewModel"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="activityLogService">Optional activity log service for data operations.</param>
        public ActivityLogViewModel(
            ILogger<ActivityLogViewModel> logger,
            IActivityLogService? activityLogService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activityLogService = activityLogService;

            // Initialize commands
            LoadActivityCommand = new AsyncRelayCommand(LoadActivityAsync);
            RefreshCommand = new AsyncRelayCommand(LoadActivityAsync);
            ClearCommand = new RelayCommand(ClearActivityLog);
            ExportToCsvCommand = new AsyncRelayCommand<string?>(ExportToCsvAsync);

            _logger.LogInformation("ActivityLogViewModel constructed");
        }

        #region Initialization

        /// <summary>
        /// Initializes the view model by loading activity entries.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Initializing ActivityLogViewModel");
            await LoadActivityAsync(ct);
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads activity entries from the service or generates sample data.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task LoadActivityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusText = "Loading activity...";

                _logger.LogDebug("Loading activity entries");

                ObservableCollection<ActivityLog> entries;

                if (_activityLogService != null)
                {
                    try
                    {
                        // Try to load from service - get in-memory collection
                        var recentActivities = _activityLogService.GetRecentActivities();
                        entries = new ObservableCollection<ActivityLog>(recentActivities);
                        _logger.LogInformation("Loaded {Count} activity entries from service", entries.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load activity from service, using sample data");
                        entries = GenerateSampleActivityData();
                    }
                }
                else
                {
                    // No service available, generate sample data for demo
                    _logger.LogInformation("No activity log service configured, generating sample data");
                    entries = GenerateSampleActivityData();
                }

                ActivityEntries.Clear();
                foreach (var entry in entries)
                {
                    ActivityEntries.Add(entry);
                }

                StatusText = $"Loaded {ActivityEntries.Count} activities";
                _logger.LogInformation("ActivityLog now contains {Count} entries", ActivityEntries.Count);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Load cancelled";
                _logger.LogInformation("Activity load cancelled");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading activity: {ex.Message}";
                StatusText = "Error loading activity";
                _logger.LogError(ex, "Error loading activity entries");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates sample activity data for demonstration purposes.
        /// </summary>
        /// <returns>Collection of sample activity entries.</returns>
        private ObservableCollection<ActivityLog> GenerateSampleActivityData()
        {
            var now = DateTime.Now;
            var entries = new ObservableCollection<ActivityLog>();

            // Generate sample activities
            var sampleActivities = new[]
            {
                ("Opened Customers Panel", "Navigated to Customer Management panel", "Success"),
                ("Data Load", "Loaded 45 utility customers from database", "Success"),
                ("Filter Applied", "Filtered by Active status (35 results)", "Success"),
                ("Export CSV", "Exported customer data to file", "Success"),
                ("Opened Dashboard", "Navigated to Dashboard panel", "Success"),
                ("Chart Rendered", "Rendered revenue trend chart (12 months)", "Success"),
                ("Opened Budget Analysis", "Navigated to Budget panel", "Success"),
                ("Analysis Calculation", "Computed budget variance analysis", "Success"),
                ("Data Refresh", "Refreshed all panel data", "Success"),
                ("Opened War Room", "Navigated to War Room scenario analysis", "Success"),
                ("Scenario Created", "Generated new what-if scenario", "Success"),
                ("Risk Assessment", "Calculated risk metrics", "Success"),
                ("Opened Settings", "Opened application settings", "Success"),
                ("Theme Changed", "Applied Office2019Colorful theme", "Success"),
                ("Profile Updated", "Updated user preferences", "Success"),
            };

            for (int i = 0; i < sampleActivities.Length; i++)
            {
                var (activity, details, status) = sampleActivities[i];
                entries.Add(new ActivityLog
                {
                    Id = i + 1,
                    Timestamp = now.AddMinutes(-sampleActivities.Length + i),
                    Activity = activity,
                    Details = details,
                    Status = status,
                    User = Environment.UserName
                });
            }

            return entries;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Clears all activity log entries.
        /// </summary>
        public void ClearActivityLog()
        {
            _logger.LogInformation("Clearing activity log");
            ActivityEntries.Clear();
            StatusText = "Activity log cleared";
        }

        #endregion

        #region Export

        /// <summary>
        /// Exports activity entries to a CSV file.
        /// </summary>
        /// <param name="filePath">Destination file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ExportToCsvAsync(string? filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        // Write header
                        writer.WriteLine("Timestamp,Activity,Details,Status");

                        // Write entries
                        foreach (var entry in ActivityEntries)
                        {
                            var line = $"\"{entry.Timestamp:g}\",\"{entry.Activity}\",\"{entry.Details}\",\"{entry.Status}\"";
                            writer.WriteLine(line);
                        }
                    }

                    _logger.LogInformation("Activity log exported to {Path}", filePath);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Export failed: {ex.Message}";
                    _logger.LogError(ex, "Failed to export activity log to {Path}", filePath);
                    throw;
                }
            }, cancellationToken);
        }

        #endregion
    }
}
