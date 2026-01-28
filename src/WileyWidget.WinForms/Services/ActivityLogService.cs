using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Service for logging application activities (navigation, actions, events).
    /// Provides in-memory activity tracking and integrates with activity grid display.
    /// </summary>
    public interface IActivityLogService
    {
        /// <summary>
        /// Logs a navigation activity.
        /// </summary>
        /// <param name="actionName">Name of the action (e.g., "Navigate to Dashboard")</param>
        /// <param name="details">Additional details about the action</param>
        /// <param name="status">Status of the activity (Success, Failed, etc.)</param>
        Task LogNavigationAsync(string actionName, string details, string status = "Success");

        /// <summary>
        /// Logs a general application activity.
        /// </summary>
        Task LogActivityAsync(string activity, string details, string status = "Success", string category = "General");

        /// <summary>
        /// Gets the collection of recent activities.
        /// </summary>
        ObservableCollection<ActivityLog> GetRecentActivities();

        /// <summary>
        /// Gets the list of activity entries asynchronously.
        /// </summary>
        Task<System.Collections.Generic.List<ActivityLog>> GetActivityEntriesAsync();

        /// <summary>
        /// Clears all logged activities.
        /// </summary>
        void ClearActivities();
    }

    /// <summary>
    /// Default implementation of IActivityLogService.
    /// Stores activities in-memory and persists to database.
    /// </summary>
    public class ActivityLogService : IActivityLogService
    {
        private readonly ILogger<ActivityLogService>? _logger;
        private readonly WileyWidget.Business.Interfaces.IActivityLogRepository? _repository;
        private readonly ObservableCollection<ActivityLog> _activities = new();
        private const int MaxActivities = 500; // Keep only last 500 activities in memory

        public ActivityLogService(
            ILogger<ActivityLogService>? logger = null,
            WileyWidget.Business.Interfaces.IActivityLogRepository? repository = null)
        {
            _logger = logger;
            _repository = repository;
        }

        /// <summary>
        /// Logs a navigation activity to in-memory collection and Serilog.
        /// </summary>
        public async Task LogNavigationAsync(string actionName, string details, string status = "Success")
        {
            await LogActivityAsync(actionName, details, status, "Navigation");
        }

        /// <summary>
        /// Logs a general application activity.
        /// </summary>
        public async Task LogActivityAsync(string activity, string details, string status = "Success", string category = "General")
        {
            try
            {
                var entry = new ActivityLog
                {
                    Timestamp = DateTime.UtcNow,
                    Activity = activity,
                    Details = details,
                    Status = status,
                    Category = category,
                    User = Environment.UserName,
                    ActivityType = category,
                    Severity = status == "Success" ? "Info" : "Warning"
                };

                // Add to in-memory collection (always, for UI)
                _activities.Insert(0, entry);

                // Keep only last N entries
                while (_activities.Count > MaxActivities)
                {
                    _activities.RemoveAt(_activities.Count - 1);
                }

                // Log via Serilog
                _logger?.LogInformation(
                    "[ACTIVITY_LOG] {Activity} | {Details} | Status: {Status}",
                    activity, details, status);

                Serilog.Log.Information(
                    "[ACTIVITY_LOG] {Activity} | {Details} | Category: {Category} | Status: {Status}",
                    activity, details, category, status);

                // Persist to database if repository available
                if (_repository != null)
                {
                    try
                    {
                        await _repository.LogActivityAsync(entry);
                        _logger?.LogDebug("Activity persisted to database: {Activity}", activity);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to persist activity to database: {Activity}", activity);
                        // Continue - in-memory logging still works
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to log activity: {Activity}", activity);
            }
        }

        /// <summary>
        /// Gets the observable collection of recent activities.
        /// This collection is automatically synchronized with UI displays.
        /// </summary>
        public ObservableCollection<ActivityLog> GetRecentActivities()
        {
            return _activities;
        }

        /// <summary>
        /// Gets the list of activity entries asynchronously.
        /// </summary>
        public async Task<System.Collections.Generic.List<ActivityLog>> GetActivityEntriesAsync()
        {
            try
            {
                return await Task.FromResult(new System.Collections.Generic.List<ActivityLog>(_activities));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve activity entries");
                return new System.Collections.Generic.List<ActivityLog>(); // Return empty list instead of throwing
            }
        }

        /// <summary>
        /// Clears all logged activities.
        /// </summary>
        public void ClearActivities()
        {
            _activities.Clear();
            _logger?.LogInformation("Activity log cleared by user");
        }
    }
}
