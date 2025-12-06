using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WileyWidget.Services
{
    /// <summary>
    /// Utility for automatic performance logging of operations.
    /// Logs elapsed time when operation exceeds threshold (default: 100ms).
    /// Usage:
    ///   using (var perf = new PerformanceLogger(_logger, "LoadData", 100))
    ///   {
    ///       // operation here
    ///   }
    /// Output: "Performance: LoadData completed in 245ms (threshold: 100ms)"
    /// </summary>
    public class PerformanceLogger : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly int _thresholdMs;
        private readonly Stopwatch _stopwatch;
        private bool _disposed = false;

        public PerformanceLogger(ILogger logger, string operationName, int thresholdMs = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _thresholdMs = thresholdMs;
            _stopwatch = Stopwatch.StartNew();

            _logger.LogDebug("▶ Operation started: {Operation}", _operationName);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _stopwatch.Stop();

            if (_stopwatch.ElapsedMilliseconds > _thresholdMs)
            {
                _logger.LogInformation(
                    "⏱ Performance: {Operation} completed in {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    _operationName, _stopwatch.ElapsedMilliseconds, _thresholdMs);
            }
            else
            {
                _logger.LogDebug(
                    "◀ Operation completed: {Operation} in {ElapsedMs}ms",
                    _operationName, _stopwatch.ElapsedMilliseconds);
            }

            _disposed = true;
        }
    }
}
