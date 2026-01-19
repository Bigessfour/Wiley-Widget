using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WileyWidget.WinForms.Diagnostics
{
    /// <summary>
    /// Startup initialization instrumentation helper.
    /// Records timing metrics for each initialization phase to identify bottlenecks
    /// and measure improvements over time.
    /// </summary>
    public class StartupInstrumentation
    {
        private static readonly object _lockObject = new object();
        private static readonly Dictionary<string, long> _phaseTimes = new();
        private static readonly List<string> _phaseOrder = new();

        /// <summary>
        /// Records the time taken for a specific initialization phase.
        /// </summary>
        /// <param name="phaseName">Name of the phase (e.g., "DockingManager Init", "Theme Application")</param>
        /// <param name="milliseconds">Time taken in milliseconds</param>
        public static void RecordPhaseTime(string phaseName, long milliseconds)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                return;

            lock (_lockObject)
            {
                _phaseTimes[phaseName] = milliseconds;
                if (!_phaseOrder.Contains(phaseName))
                {
                    _phaseOrder.Add(phaseName);
                }
            }
        }

        /// <summary>
        /// Gets all recorded initialization metrics in order of execution.
        /// </summary>
        /// <returns>Dictionary of phase names and their durations in milliseconds</returns>
        public static Dictionary<string, long> GetInitializationMetrics()
        {
            lock (_lockObject)
            {
                return _phaseOrder.ToDictionary(
                    phase => phase,
                    phase => _phaseTimes.TryGetValue(phase, out var time) ? time : 0L
                );
            }
        }

        /// <summary>
        /// Gets total time spent in all recorded phases.
        /// </summary>
        /// <returns>Total time in milliseconds</returns>
        public static long GetTotalInitializationTime()
        {
            lock (_lockObject)
            {
                return _phaseTimes.Values.Sum();
            }
        }

        /// <summary>
        /// Logs initialization state and metrics for diagnostics.
        /// </summary>
        /// <param name="logger">Logger instance for output</param>
        public static void LogInitializationState(ILogger? logger)
        {
            if (logger == null)
                return;

            lock (_lockObject)
            {
                if (_phaseTimes.Count == 0)
                {
                    logger.LogInformation("No initialization metrics recorded yet");
                    return;
                }

                logger.LogInformation("=== Startup Initialization Metrics ===");
                foreach (var phase in _phaseOrder)
                {
                    if (_phaseTimes.TryGetValue(phase, out var time))
                    {
                        logger.LogInformation("  {Phase}: {Milliseconds}ms", phase, time);
                    }
                }

                var totalTime = _phaseTimes.Values.Sum();
                logger.LogInformation("  Total: {Milliseconds}ms", totalTime);
            }
        }

        /// <summary>
        /// Clears all recorded metrics. Useful for testing or resetting between sessions.
        /// </summary>
        public static void Clear()
        {
            lock (_lockObject)
            {
                _phaseTimes.Clear();
                _phaseOrder.Clear();
            }
        }

        /// <summary>
        /// Gets a formatted string representation of all metrics for logging or display.
        /// </summary>
        /// <returns>Human-readable metrics summary</returns>
        public static string GetFormattedMetrics()
        {
            lock (_lockObject)
            {
                if (_phaseTimes.Count == 0)
                    return "No initialization metrics recorded";

                var lines = new List<string> { "=== Startup Initialization Metrics ===" };
                foreach (var phase in _phaseOrder)
                {
                    if (_phaseTimes.TryGetValue(phase, out var time))
                    {
                        lines.Add($"  {phase}: {time}ms");
                    }
                }

                var totalTime = _phaseTimes.Values.Sum();
                lines.Add($"  Total: {totalTime}ms");

                return string.Join(Environment.NewLine, lines);
            }
        }

        /// <summary>
        /// Helper to measure the time taken by an action synchronously.
        /// </summary>
        /// <param name="phaseName">Name of the phase to record</param>
        /// <param name="action">Action to measure</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void MeasurePhase(string phaseName, Action action, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(phaseName) || action == null)
                return;

            var sw = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                sw.Stop();
                RecordPhaseTime(phaseName, sw.ElapsedMilliseconds);
                logger?.LogDebug("Phase {Phase} completed in {Milliseconds}ms", phaseName, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Helper to measure the time taken by an async operation.
        /// </summary>
        /// <param name="phaseName">Name of the phase to record</param>
        /// <param name="action">Async action to measure</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static async System.Threading.Tasks.Task MeasurePhaseAsync(
            string phaseName,
            Func<System.Threading.Tasks.Task> action,
            ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(phaseName) || action == null)
                return;

            var sw = Stopwatch.StartNew();
            try
            {
                await action();
            }
            finally
            {
                sw.Stop();
                RecordPhaseTime(phaseName, sw.ElapsedMilliseconds);
                logger?.LogDebug("Phase {Phase} completed in {Milliseconds}ms", phaseName, sw.ElapsedMilliseconds);
            }
        }
    }
}
