// HealthReportingService.cs - Implementation of health reporting service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.3)
// Date: November 9, 2025
//
// This service tracks and reports application health including:
// - Module health status monitoring
// - Health report generation and serialization
// - Timing and performance metrics logging

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for tracking and reporting application health status.
    /// Provides centralized health monitoring and logging capabilities.
    /// </summary>
    public class HealthReportingService : IHealthReportingService
    {
        private readonly ILogger<HealthReportingService> _logger;
        private readonly object _syncRoot = new object();
        private DateTimeOffset? _lastHealthReportUpdate;

        public HealthReportingService(ILogger<HealthReportingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the timestamp of the last health report update.
        /// </summary>
        public DateTimeOffset? LastHealthReportUpdate
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lastHealthReportUpdate;
                }
            }
            private set
            {
                lock (_syncRoot)
                {
                    _lastHealthReportUpdate = value;
                }
            }
        }

        /// <summary>
        /// Updates the latest health report with module status information.
        /// Tracks healthy vs total modules and logs detailed health information.
        /// </summary>
        public void UpdateLatestHealthReport(object report)
        {
            if (report == null)
            {
                Log.Warning("Module health report update skipped: report was null");
                _logger.LogWarning("Module health report update skipped: report was null");
                return;
            }

            try
            {
                if (report is IEnumerable<object> moduleHealthInfos)
                {
                    int totalModules = 0, healthyModules = 0;
                    var moduleDetails = new List<object>();

                    foreach (var healthInfo in moduleHealthInfos)
                    {
                        totalModules++;

                        // Check for HealthCheckResult type
                        if (healthInfo is HealthCheckResult healthResult && healthResult.Status == HealthStatus.Healthy)
                        {
                            healthyModules++;
                        }

                        // Add to details for JSON serialization
                        moduleDetails.Add(new
                        {
                            Module = healthInfo.GetType().GetProperty("ModuleName")?.GetValue(healthInfo)?.ToString() ?? "Unknown",
                            Status = healthInfo.GetType().GetProperty("Status")?.GetValue(healthInfo)?.ToString() ?? "Unknown",
                            Timestamp = DateTimeOffset.UtcNow
                        });
                    }

                    // Create structured health report
                    var healthReport = new
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        TotalModules = totalModules,
                        HealthyModules = healthyModules,
                        UnhealthyModules = totalModules - healthyModules,
                        HealthPercentage = totalModules > 0 ? (healthyModules * 100.0 / totalModules) : 0,
                        Modules = moduleDetails
                    };

                    // Serialize and log the health report
                    var jsonReport = JsonSerializer.Serialize(healthReport, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    Log.Information("[HEALTH_REPORT] Module health status:\n{HealthReport}", jsonReport);
                    _logger.LogInformation("[HEALTH_REPORT] Module health: {Healthy}/{Total} healthy ({Percentage:F1}%)",
                        healthyModules, totalModules, healthReport.HealthPercentage);

                    // Update timestamp
                    LastHealthReportUpdate = DateTimeOffset.UtcNow;

                    return;
                }

                // Fallback for non-enumerable reports
                Log.Debug("Module health report refreshed ({ReportType})", report.GetType().FullName);
                _logger.LogDebug("Module health report refreshed ({ReportType})", report.GetType().FullName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update health report");
                _logger.LogError(ex, "Failed to update health report");
            }
        }

        /// <summary>
        /// Logs a debug event with category and message.
        /// </summary>
        public void LogDebugEvent(string category, string message)
        {
            Log.Debug("[{Category}] {Message}", category, message);
            _logger.LogDebug("[{Category}] {Message}", category, message);
        }

        /// <summary>
        /// Logs startup timing information.
        /// </summary>
        public void LogStartupTiming(string message, TimeSpan elapsed)
        {
            Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
            _logger.LogDebug("{Message} completed in {Ms:F2}ms", message, elapsed.TotalMilliseconds);
        }
    }
}
