// Syncfusion Logging Bridge for WileyWidget
// Integrates Syncfusion internal diagnostics with Microsoft.Extensions.Logging
// Created: 2025-11-10
// Purpose: Capture Syncfusion SfSkinManager and control diagnostics that are normally silent

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace WileyWidget.Services.Logging
{
    /// <summary>
    /// Bridges Syncfusion diagnostic output to MEL/Serilog pipeline.
    /// Addresses the "silent after startup" logging gap for Syncfusion controls.
    /// Thread-safe and follows proper dispose patterns.
    /// </summary>
    public sealed class SyncfusionLoggingBridge : IDisposable
    {
        private readonly ILogger<SyncfusionLoggingBridge> _logger;
        private readonly object _lock = new object();
        private TraceListener? _syncfusionTraceListener;
        private bool _disposed;
        private bool _initialized;

        public SyncfusionLoggingBridge(ILogger<SyncfusionLoggingBridge> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes Syncfusion diagnostic trace listener.
        /// Call this AFTER Syncfusion license registration but BEFORE theme application.
        /// Thread-safe and idempotent (can be called multiple times safely).
        /// </summary>
        public void InitializeSyncfusionDiagnostics()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if (_initialized)
                {
                    _logger.LogDebug("[SYNCFUSION] Already initialized, skipping duplicate initialization");
                    return;
                }

                try
                {
                    // Create custom trace listener for Syncfusion
                    _syncfusionTraceListener = new SyncfusionTraceListener(_logger);

                    // Add to System.Diagnostics trace sources that Syncfusion uses
                    Trace.Listeners.Add(_syncfusionTraceListener);

                    // Enable Syncfusion WPF diagnostics (if available via reflection)
                    EnableSyncfusionInternalLogging();

                    _initialized = true;
                    _logger.LogInformation("[SYNCFUSION] Diagnostic logging bridge initialized");
                }
                catch (Exception ex)
                {
                    // Clean up partial initialization
                    if (_syncfusionTraceListener != null)
                    {
                        try
                        {
                            Trace.Listeners.Remove(_syncfusionTraceListener);
                            _syncfusionTraceListener.Dispose();
                        }
                        catch
                        {
                            // Suppress cleanup errors
                        }
                        _syncfusionTraceListener = null;
                    }

                    _logger.LogError(ex, "[SYNCFUSION] Failed to initialize diagnostic logging bridge");
                    throw; // Re-throw to notify caller of initialization failure
                }
            }
        }

        /// <summary>
        /// Attempts to enable Syncfusion's internal logging via reflection.
        /// Syncfusion doesn't expose public logging APIs, so this uses safe reflection.
        /// </summary>
        private void EnableSyncfusionInternalLogging()
        {
            try
            {
                // Attempt to locate Syncfusion diagnostic settings
                var skinManagerType = Type.GetType("Syncfusion.SfSkinManager.SfSkinManager, Syncfusion.SfSkinManager.WPF");
                if (skinManagerType != null)
                {
                    // Look for diagnostic/logging properties (these may not exist in all versions)
                    var diagnosticProperty = skinManagerType.GetProperty("EnableDiagnostics",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                    if (diagnosticProperty != null && diagnosticProperty.CanWrite)
                    {
                        diagnosticProperty.SetValue(null, true);
                        _logger.LogInformation("[SYNCFUSION] Internal diagnostics enabled via reflection");
                    }
                    else
                    {
                        _logger.LogDebug("[SYNCFUSION] No diagnostic property found (expected for v31.x)");
                    }
                }
            }
            catch (Exception ex)
            {
                // This is expected if Syncfusion doesn't expose diagnostic APIs
                _logger.LogDebug(ex, "[SYNCFUSION] Could not enable internal diagnostics (not critical)");
            }
        }

        /// <summary>
        /// Disposes the trace listener and removes it from System.Diagnostics.
        /// Thread-safe and can be called multiple times.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                try
                {
                    if (_syncfusionTraceListener != null)
                    {
                        Trace.Listeners.Remove(_syncfusionTraceListener);
                        _syncfusionTraceListener.Dispose();
                        _syncfusionTraceListener = null;
                    }

                    _logger.LogDebug("[SYNCFUSION] Diagnostic logging bridge disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SYNCFUSION] Error during dispose (non-critical)");
                }
                finally
                {
                    _disposed = true;
                    _initialized = false;
                }
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the bridge has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SyncfusionLoggingBridge));
            }
        }
    }

    /// <summary>
    /// Custom TraceListener that forwards Syncfusion diagnostics to MEL/Serilog.
    /// Filters messages to only capture Syncfusion-related traces.
    /// Thread-safe for concurrent trace calls.
    /// </summary>
    internal sealed class SyncfusionTraceListener : TraceListener
    {
        private readonly ILogger _logger;
        private readonly object _writeLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncfusionTraceListener"/> class.
        /// </summary>
        /// <param name="logger">The logger instance to forward traces to.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public SyncfusionTraceListener(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Writes a message to the logger if it contains Syncfusion-related keywords.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public override void Write(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Check if message is from Syncfusion (basic heuristic)
            if (message.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("SfSkin", StringComparison.OrdinalIgnoreCase))
            {
                lock (_writeLock)
                {
                    _logger.LogDebug("[SYNCFUSION TRACE] {Message}", message.TrimEnd());
                }
            }
        }

        /// <summary>
        /// Writes a line to the logger.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public override void WriteLine(string? message)
        {
            Write(message);
        }

        /// <summary>
        /// Writes trace event information to the logger with appropriate log level mapping.
        /// </summary>
        /// <param name="eventCache">The trace event cache (unused).</param>
        /// <param name="source">The trace source name.</param>
        /// <param name="eventType">The type of trace event.</param>
        /// <param name="id">The trace event ID.</param>
        /// <param name="message">The trace message.</param>
        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Only log Syncfusion-related traces
            if (!source.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                return;

            lock (_writeLock)
            {
                var logLevel = eventType switch
                {
                    TraceEventType.Critical => LogLevel.Critical,
                    TraceEventType.Error => LogLevel.Error,
                    TraceEventType.Warning => LogLevel.Warning,
                    TraceEventType.Information => LogLevel.Information,
                    _ => LogLevel.Debug
                };

                _logger.Log(logLevel, "[SYNCFUSION {Source}] {Message}", source, message.TrimEnd());
            }
        }

        /// <summary>
        /// Disposes resources used by the trace listener.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            // No unmanaged resources to clean up
            // Logger is not owned by this class
            base.Dispose(disposing);
        }
    }
}
