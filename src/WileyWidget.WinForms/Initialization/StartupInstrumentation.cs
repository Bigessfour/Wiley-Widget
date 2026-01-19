using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WileyWidget.WinForms.Initialization
{
    /// <summary>
    /// Provides diagnostic instrumentation for application startup phases.
    /// Tracks initialization timings and collects performance metrics for analysis and optimization.
    /// </summary>
    /// <remarks>
    /// This class is thread-safe and designed for production use. It helps identify
    /// startup bottlenecks and provides detailed diagnostics for slow startup scenarios.
    ///
    /// Usage:
    /// <code>
    /// StartupInstrumentation.RecordPhase("DI Build", 25);
    /// StartupInstrumentation.RecordPhase("Theme Init", 50);
    /// StartupInstrumentation.RecordPhase("DockingManager", 1000);
    /// var metrics = StartupInstrumentation.GetMetrics();
    /// StartupInstrumentation.LogMetrics(logger);
    /// </code>
    /// </remarks>
    public static class StartupInstrumentation
    {
        private static readonly object _lockObj = new object();
        private static readonly Dictionary<string, long> _phases = new Dictionary<string, long>();
        private static readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// Records the elapsed time for a startup phase.
        /// </summary>
        /// <param name="phaseName">Name of the startup phase (e.g., "DI Build", "DockingManager")</param>
        /// <param name="elapsedMilliseconds">Elapsed time in milliseconds</param>
        public static void RecordPhase(string phaseName, long elapsedMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                return;

            lock (_lockObj)
            {
                _phases[phaseName] = elapsedMilliseconds;
            }
        }

        /// <summary>
        /// Starts a timer for a startup phase and returns a disposable that records the phase when disposed.
        /// </summary>
        /// <param name="phaseName">Name of the startup phase</param>
        /// <returns>A disposable timer that records the phase time when disposed</returns>
        public static IDisposable TimerScope(string phaseName)
        {
            return new PhaseTimer(phaseName);
        }

        /// <summary>
        /// Gets a copy of all recorded initialization metrics.
        /// </summary>
        /// <returns>Dictionary of phase names and their elapsed milliseconds</returns>
        public static Dictionary<string, long> GetMetrics()
        {
            lock (_lockObj)
            {
                return new Dictionary<string, long>(_phases);
            }
        }

        /// <summary>
        /// Gets the total elapsed time since instrumentation started.
        /// </summary>
        public static long TotalElapsedMilliseconds => _totalStopwatch.ElapsedMilliseconds;

        /// <summary>
        /// Logs all initialization metrics using the provided logger.
        /// Includes phase timings, totals, and performance analysis.
        /// </summary>
        public static void LogMetrics(ILogger logger)
        {
            if (logger == null)
                return;

            lock (_lockObj)
            {
                if (_phases.Count == 0)
                {
                    logger.LogInformation("No startup metrics recorded");
                    return;
                }

                var totalPhaseTime = _phases.Values.Sum();
                var maxPhaseTime = _phases.Values.Max();
                var maxPhaseName = _phases.FirstOrDefault(p => p.Value == maxPhaseTime).Key ?? "Unknown";

                logger.LogInformation("=== STARTUP INITIALIZATION METRICS ===");
                logger.LogInformation("Total Tracked Time: {TotalTime}ms", totalPhaseTime);
                logger.LogInformation("Total Elapsed Time: {TotalElapsed}ms", _totalStopwatch.ElapsedMilliseconds);
                logger.LogInformation("Phase Count: {PhaseCount}", _phases.Count);
                logger.LogInformation("Longest Phase: {PhaseName} ({Duration}ms)", maxPhaseName, maxPhaseTime);
                logger.LogInformation("--- Phase Breakdown ---");

                foreach (var phase in _phases.OrderByDescending(p => p.Value))
                {
                    var percentage = totalPhaseTime > 0 ? (phase.Value * 100.0) / totalPhaseTime : 0;
                    logger.LogInformation("  {PhaseName,-30} {Duration,5}ms ({Percentage,5:F1}%)",
                        phase.Key, phase.Value, percentage);
                }

                logger.LogInformation("======================================");
            }
        }

        /// <summary>
        /// Clears all recorded metrics. Useful for testing or multiple startup scenarios.
        /// </summary>
        public static void Reset()
        {
            lock (_lockObj)
            {
                _phases.Clear();
                _totalStopwatch.Restart();
            }
        }

        /// <summary>
        /// Private helper class for timer scope pattern.
        /// Records phase timing automatically when disposed.
        /// </summary>
        private sealed class PhaseTimer : IDisposable
        {
            private readonly string _phaseName;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public PhaseTimer(string phaseName)
            {
                _phaseName = phaseName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _stopwatch.Stop();
                RecordPhase(_phaseName, _stopwatch.ElapsedMilliseconds);
            }
        }
    }

    /// <summary>
    /// Provides production-ready validation and initialization helpers for UI components.
    /// </summary>
    public static class InitializationValidation
    {
        /// <summary>
        /// Validates critical preconditions for MainForm initialization.
        /// </summary>
        /// <param name="serviceProvider">Service provider from DI container</param>
        /// <param name="themeService">Theme service for configuration</param>
        /// <param name="logger">Logger for diagnostics</param>
        /// <returns>True if validation succeeds; throws exception on critical failures</returns>
        public static bool ValidateMainFormPreconditions(
            IServiceProvider? serviceProvider,
            object? themeService,
            ILogger? logger)
        {
            logger?.LogInformation("Validating MainForm initialization preconditions");

            if (serviceProvider == null)
            {
                logger?.LogError("ServiceProvider is null - DI container not initialized");
                throw new InvalidOperationException("ServiceProvider is required for MainForm initialization");
            }

            if (themeService == null)
            {
                logger?.LogError("ThemeService is null - theme configuration not available");
                throw new InvalidOperationException("ThemeService is required for MainForm initialization");
            }

            logger?.LogInformation("MainForm preconditions validated successfully");
            return true;
        }

        /// <summary>
        /// Validates that DockingManager was created and initialized properly.
        /// </summary>
        public static bool ValidateDockingManagerCreation(
            object? dockingManager,
            object? panelNavigator,
            ILogger? logger)
        {
            logger?.LogInformation("Validating DockingManager initialization");

            if (dockingManager == null)
            {
                logger?.LogError("DockingManager creation failed - returned null from factory");
                return false;
            }

            if (panelNavigator == null)
            {
                logger?.LogWarning("PanelNavigationService is null after DockingManager initialization");
                return false;
            }

            logger?.LogInformation("DockingManager validation passed");
            return true;
        }

        /// <summary>
        /// Safe creation of exception context for startup failures.
        /// Captures diagnostic information for debugging.
        /// </summary>
        public static string CreateStartupFailureContext(
            string phase,
            Exception? exception,
            Dictionary<string, object?>? context = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== STARTUP FAILURE CONTEXT ===");
            sb.AppendLine($"Phase: {phase}");
            sb.AppendLine($"Time: {DateTime.Now:O}");
            sb.AppendLine($"Exception: {exception?.GetType().Name ?? "None"}");
            sb.AppendLine($"Message: {exception?.Message ?? "No message"}");
            sb.AppendLine($"Total Elapsed: {StartupInstrumentation.TotalElapsedMilliseconds}ms");

            if (context?.Count > 0)
            {
                sb.AppendLine("--- Context ---");
                foreach (var item in context)
                {
                    sb.AppendLine($"  {item.Key}: {item.Value ?? "null"}");
                }
            }

            sb.AppendLine("--- Phase Metrics ---");
            var metrics = StartupInstrumentation.GetMetrics();
            if (metrics.Count > 0)
            {
                foreach (var metric in metrics.OrderBy(m => m.Key))
                {
                    sb.AppendLine($"  {metric.Key}: {metric.Value}ms");
                }
            }
            else
            {
                sb.AppendLine("  No metrics recorded");
            }

            sb.AppendLine("==============================");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Production-ready error handling and recovery strategies for startup failures.
    /// </summary>
    public class StartupErrorHandler
    {
        private readonly ILogger? _logger;

        public StartupErrorHandler(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles DockingManager initialization failure with graceful degradation.
        /// </summary>
        public void HandleDockingManagerFailure(Exception exception)
        {
            _logger?.LogError(exception, "DockingManager initialization failed");

            var context = InitializationValidation.CreateStartupFailureContext(
                "DockingManager Initialization",
                exception,
                new Dictionary<string, object?>
                {
                    { "RecoveryAction", "Docking disabled - basic UI available" },
                    { "UserImpact", "Panels not docked, only basic layout visible" }
                });

            _logger?.LogError("{Context}", context);

            // Recovery: App continues with basic UI, docking disabled
            // User can still access functionality via menu/ribbon
        }

        /// <summary>
        /// Handles theme application failure with fallback to default theme.
        /// </summary>
        public void HandleThemeApplicationFailure(Exception exception, string requestedTheme)
        {
            _logger?.LogWarning(exception, "Theme application failed for theme '{Theme}'", requestedTheme);

            var context = InitializationValidation.CreateStartupFailureContext(
                "Theme Application",
                exception,
                new Dictionary<string, object?>
                {
                    { "RequestedTheme", requestedTheme },
                    { "RecoveryAction", "Falling back to default theme" },
                    { "UserImpact", "Application may appear with incorrect colors temporarily" }
                });

            _logger?.LogWarning("{Context}", context);

            // Recovery: Continue with default theme
            // User can switch themes via menu
        }

        /// <summary>
        /// Handles async initialization timeout.
        /// </summary>
        public void HandleInitializationTimeout(int timeoutSeconds)
        {
            _logger?.LogWarning(
                "Initialization timeout after {TimeoutSeconds}s - application may not be fully loaded",
                timeoutSeconds);

            var context = InitializationValidation.CreateStartupFailureContext(
                "Async Initialization Timeout",
                null,
                new Dictionary<string, object?>
                {
                    { "Timeout", $"{timeoutSeconds}s" },
                    { "RecoveryAction", "Application continued with partial initialization" },
                    { "UserImpact", "Some features may not be available until timeout expires or app restart" }
                });

            _logger?.LogWarning("{Context}", context);

            // Recovery: App continues with whatever was loaded so far
            // User sees loading state and can continue working
        }
    }
}
