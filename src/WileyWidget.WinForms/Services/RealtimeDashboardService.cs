using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Provides real-time dashboard updates and data visualization synchronization.
    /// Implements publish-subscribe pattern for efficient data propagation.
    /// </summary>
    public class RealtimeDashboardService : IDisposable
    {
        private readonly ILogger<RealtimeDashboardService> _logger;
        private readonly System.Threading.Timer _updateTimer;
        private readonly Dictionary<string, DashboardSubscription> _subscriptions = new();
        private volatile bool _disposed;

        public event EventHandler<DashboardDataUpdatedEventArgs>? DataUpdated;
        public event EventHandler<DashboardErrorEventArgs>? ErrorOccurred;

        public RealtimeDashboardService(ILogger<RealtimeDashboardService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Update dashboard every 5 seconds if subscribed
            _updateTimer = new System.Threading.Timer(UpdateDashboard, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Subscribes to real-time dashboard updates for a specific metric.
        /// </summary>
        public void Subscribe(string metricId, Action<object> callback)
        {
            if (string.IsNullOrWhiteSpace(metricId) || callback == null)
                return;

            if (!_subscriptions.ContainsKey(metricId))
            {
                _subscriptions[metricId] = new DashboardSubscription { MetricId = metricId, Callbacks = new() };
            }

            _subscriptions[metricId].Callbacks.Add(callback);
            _logger.LogDebug("Subscribed to metric: {MetricId}", metricId);
        }

        /// <summary>
        /// Unsubscribes from dashboard updates.
        /// </summary>
        public void Unsubscribe(string metricId, Action<object> callback)
        {
            if (_subscriptions.TryGetValue(metricId, out var subscription))
            {
                subscription.Callbacks.Remove(callback);
                if (subscription.Callbacks.Count == 0)
                {
                    _subscriptions.Remove(metricId);
                }
            }
        }

        /// <summary>
        /// Forces an immediate dashboard update.
        /// </summary>
        public async Task UpdateNowAsync(CancellationToken cancellationToken = default)
        {
            await UpdateDashboardAsync();
        }

        /// <summary>
        /// Updates dashboard metrics in real-time.
        /// </summary>
        private void UpdateDashboard(object? state)
        {
            if (_subscriptions.Count == 0 || _disposed)
                return;

            try
            {
                _ = UpdateDashboardAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard update failed");
                ErrorOccurred?.Invoke(this, new DashboardErrorEventArgs { Exception = ex });
            }
        }

        private async Task UpdateDashboardAsync(CancellationToken cancellationToken = default)
        {
            // Simulate fetching updated data
            await Task.Delay(100);

            var metrics = GenerateDashboardMetrics();

            foreach (var subscription in _subscriptions.Values)
            {
                if (metrics.TryGetValue(subscription.MetricId, out var data))
                {
                    DataUpdated?.Invoke(this, new DashboardDataUpdatedEventArgs
                    {
                        MetricId = subscription.MetricId,
                        Data = data
                    });

                    foreach (var callback in subscription.Callbacks)
                    {
                        try
                        {
                            callback?.Invoke(data);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Callback error for metric {MetricId}", subscription.MetricId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates sample dashboard metrics for demonstration.
        /// In production, would fetch from real services.
        /// </summary>
        private Dictionary<string, object> GenerateDashboardMetrics()
        {
            var random = new Random();
            return new Dictionary<string, object>
            {
                ["TotalBudget"] = 5000000 + random.Next(-100000, 100000),
                ["TotalSpent"] = 3200000 + random.Next(-50000, 50000),
                ["VariancePercent"] = 64 + random.Next(-5, 5),
                ["AccountsOverBudget"] = 12 + random.Next(-2, 5),
                ["MonthlyTrend"] = new[] { 45, 52, 48, 61, 55, 58, 63, 59, 64, 67, 71, 75 },
                ["DepartmentMetrics"] = new Dictionary<string, decimal>
                {
                    ["Public Works"] = 1200000m,
                    ["Parks & Recreation"] = 450000m,
                    ["Library"] = 350000m,
                    ["Police"] = 2100000m,
                    ["Fire"] = 1800000m,
                    ["Administration"] = 900000m
                }
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _updateTimer?.Dispose();
                _subscriptions.Clear();
            }

            _disposed = true;
        }

        private class DashboardSubscription
        {
            public string MetricId { get; set; } = string.Empty;
            public List<Action<object>> Callbacks { get; set; } = new();
        }
    }

    /// <summary>
    /// Event args for dashboard data updates.
    /// </summary>
    public class DashboardDataUpdatedEventArgs : EventArgs
    {
        public string MetricId { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    /// <summary>
    /// Event args for dashboard errors.
    /// </summary>
    public class DashboardErrorEventArgs : EventArgs
    {
        public Exception? Exception { get; set; }
    }
}
