using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the Activity Log panel.
    /// Provides an audit trail of recent application navigation and events.
    /// </summary>
    public partial class ActivityLogViewModel : ObservableRecipient
    {
        private readonly ILogger<ActivityLogViewModel> _logger;
        private readonly IActivityLogRepository? _activityLogRepository;

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
        /// <param name="activityLogRepository">Optional activity log repository for data operations.</param>
        public ActivityLogViewModel(
            ILogger<ActivityLogViewModel> logger,
            IActivityLogRepository? activityLogRepository = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activityLogRepository = activityLogRepository;

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
        /// Loads activity entries from the repository or generates sample data.
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

                if (_activityLogRepository != null)
                {
                    try
                    {
                        // Try to load from repository
                        var activityItems = await _activityLogRepository.GetRecentActivitiesAsync(take: 500, cancellationToken: cancellationToken);
                        entries = new ObservableCollection<ActivityLog>(
                            activityItems.Select(item => new ActivityLog
                            {
                                Timestamp = item.Timestamp,
                                Activity = item.Activity,
                                Details = item.Details,
                                Status = item.ActivityType,
                                User = item.User,
                                Category = item.Category,
                                Icon = item.Icon,
                                ActivityType = item.ActivityType,
                                Severity = ""
                            }));
                        _logger.LogInformation("Loaded {Count} activity entries from repository", entries.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load activity from repository, using sample data");
                        entries = GenerateSampleActivityData();
                    }
                }
                else
                {
                    // No repository available, generate sample data for demo
                    _logger.LogInformation("No activity log repository configured, generating sample data");
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
        /// Sample generation is disabled in production; this returns an empty collection and logs a warning.
        /// </summary>
        /// <returns>Collection of sample activity entries.</returns>
        private ObservableCollection<ActivityLog> GenerateSampleActivityData()
        {
            _logger.LogWarning("GenerateSampleActivityData called: sample activity generation disabled. Ensure activity log repository is configured.");
            return new ObservableCollection<ActivityLog>();
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
