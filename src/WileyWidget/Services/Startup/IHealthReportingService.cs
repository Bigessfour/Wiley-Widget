// IHealthReportingService.cs - Interface for health reporting service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.3)
// Date: November 9, 2025
//
// This service is responsible for tracking and reporting application health including:
// - Module health status tracking
// - Health report updates and serialization
// - Timestamp tracking for health checks

using System;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for tracking and reporting application health status.
    /// Monitors module health and provides formatted health reports.
    /// </summary>
    public interface IHealthReportingService
    {
        /// <summary>
        /// Updates the latest health report with module status information.
        /// Tracks healthy vs total modules and logs detailed health information.
        /// </summary>
        /// <param name="report">Health report object (can be IEnumerable of module health info)</param>
        void UpdateLatestHealthReport(object report);

        /// <summary>
        /// Gets the timestamp of the last health report update.
        /// </summary>
        DateTimeOffset? LastHealthReportUpdate { get; }

        /// <summary>
        /// Logs a debug event with category and message.
        /// </summary>
        void LogDebugEvent(string category, string message);

        /// <summary>
        /// Logs startup timing information.
        /// </summary>
        void LogStartupTiming(string message, TimeSpan elapsed);
    }
}
