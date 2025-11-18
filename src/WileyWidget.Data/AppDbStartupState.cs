using System;
using System.Threading;
using Serilog;

namespace WileyWidget.Data
{
    /// <summary>
    /// Tracks database initialization and fallback state for the current process.
    /// Enables coordinated degraded-mode behavior (e.g. in-memory database fallback)
    /// after catastrophic startup failures. Thread-safe, process-level static state.
    /// </summary>
    public static class AppDbStartupState
    {
        private static int _initializationAttempted;
        private static int _fallbackActivated;
        private static string? _fallbackReason;
        private static int _degradedMode;

        /// <summary>
        /// True if any initialization attempt (migrate / ensure created) has occurred.
        /// </summary>
        public static bool InitializationAttempted => _initializationAttempted == 1;

        /// <summary>
        /// True if an in-memory / degraded fallback path was activated.
        /// </summary>
        public static bool FallbackActivated => _fallbackActivated == 1;

        /// <summary>
        /// Reason provided when fallback was activated.
        /// </summary>
        public static string? FallbackReason => _fallbackReason;

        /// <summary>
        /// True if application is running in degraded (in-memory) mode.
        /// </summary>
        public static bool IsDegradedMode => _degradedMode == 1;

        /// <summary>
        /// Mark that initialization has started (idempotent).
        /// </summary>
        public static void MarkInitializationAttempted() => Interlocked.CompareExchange(ref _initializationAttempted, 1, 0);

        /// <summary>
        /// Activate degraded mode with a reason. Idempotent; first reason wins.
        /// </summary>
        /// <param name="reason">Human-readable reason (logged)</param>
        public static void ActivateFallback(string reason)
        {
            if (Interlocked.CompareExchange(ref _fallbackActivated, 1, 0) == 0)
            {
                _fallbackReason = reason;
                Log.Warning("[DB_FALLBACK] Activating in-memory degraded mode: {Reason}", reason);
                Interlocked.CompareExchange(ref _degradedMode, 1, 0);
            }
        }

        /// <summary>
        /// Reset all state - primarily for unit tests.
        /// </summary>
        public static void ResetForTests()
        {
            _initializationAttempted = 0;
            _fallbackActivated = 0;
            _fallbackReason = null;
            _degradedMode = 0;
        }
    }
}
