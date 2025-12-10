using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// AI usage tracking and logging service for XAI operations.
    /// Monitors queries, responses, errors, and usage metrics for municipal finance AI integration.
    /// Uses Serilog for structured logging with dedicated file sink.
    /// </summary>
    public class AILoggingService : IAILoggingService
    {
        private readonly ILogger<AILoggingService> _logger;
        private readonly Logger _aiUsageLogger;
        private readonly ConcurrentBag<AILogEntry> _logEntries;
        private readonly object _metricsLock = new object();
        private readonly ErrorReportingService _errorReportingService;
        private int _todayQueryCount;
        private double _totalResponseTime;
        private int _totalResponses;
        private int _totalErrors;
        private DateTime _lastResetDate;

        /// <summary>
        /// Initializes a new instance of the AILoggingService.
        /// </summary>
        /// <param name="logger">Standard logger for service operations</param>
        /// <param name="errorReportingService">Error reporting service for telemetry</param>
        public AILoggingService(ILogger<AILoggingService> logger, ErrorReportingService errorReportingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorReportingService = errorReportingService ?? throw new ArgumentNullException(nameof(errorReportingService));
            _logEntries = new ConcurrentBag<AILogEntry>();
            _lastResetDate = DateTime.UtcNow.Date;

            // Create dedicated Serilog logger for AI usage
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            _aiUsageLogger = new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(logsDirectory, "ai-usage.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    retainedFileCountLimit: 30,
                    formatProvider: CultureInfo.InvariantCulture,
                    flushToDiskInterval: TimeSpan.Zero)  // Immediate flush for AI operations
                .CreateLogger();

            _logger.LogInformation("AILoggingService initialized with dedicated Serilog file sink at {LogPath}",
                Path.Combine(logsDirectory, "ai-usage.log"));
        }

        /// <summary>
        /// Logs an AI query request to XAI service.
        /// </summary>
        public void LogQuery(string query, string context, string model)
        {
            try
            {
                ResetDailyCountersIfNeeded();

                lock (_metricsLock)
                {
                    _todayQueryCount++;
                }

                var entry = new AILogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EntryType = "Query",
                    Query = query,
                    Context = context,
                    Model = model
                };

                _logEntries.Add(entry);

                _aiUsageLogger.Information(
                    "AI Query | Model: {Model} | Query Length: {QueryLength} | Context Length: {ContextLength} | Query: {Query}",
                    model, query?.Length ?? 0, context?.Length ?? 0, TruncateForLog(query, 200));

                _logger.LogDebug("Logged AI query: Model={Model}, QueryLength={Length}", model, query?.Length ?? 0);

                // Track telemetry event for AI query
                _errorReportingService.TrackEvent("AI_Query_Logged", new Dictionary<string, object>
                {
                    ["Model"] = model,
                    ["QueryLength"] = query?.Length ?? 0,
                    ["ContextLength"] = context?.Length ?? 0,
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI query");
            }
        }

        /// <summary>
        /// Logs a successful AI response from XAI service.
        /// </summary>
        public void LogResponse(string query, string response, long responseTimeMs, int tokensUsed = 0)
        {
            try
            {
                lock (_metricsLock)
                {
                    _totalResponseTime += responseTimeMs;
                    _totalResponses++;
                }

                var entry = new AILogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EntryType = "Response",
                    Query = query,
                    Response = response,
                    ResponseTimeMs = responseTimeMs,
                    TokensUsed = tokensUsed
                };

                _logEntries.Add(entry);

                _aiUsageLogger.Information(
                    "AI Response | Response Time: {ResponseTime}ms | Tokens: {Tokens} | Response Length: {ResponseLength} | Query: {Query} | Response: {Response}",
                    responseTimeMs, tokensUsed, response?.Length ?? 0,
                    TruncateForLog(query, 100), TruncateForLog(response, 300));

                _logger.LogDebug("Logged AI response: ResponseTime={ResponseTime}ms, TokensUsed={Tokens}",
                    responseTimeMs, tokensUsed);

                // Track telemetry event for AI response
                _errorReportingService.TrackEvent("AI_Response_Logged", new Dictionary<string, object>
                {
                    ["ResponseTimeMs"] = responseTimeMs,
                    ["TokensUsed"] = tokensUsed,
                    ["ResponseLength"] = response?.Length ?? 0,
                    ["QueryLength"] = query?.Length ?? 0,
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI response");
            }
        }

        /// <summary>
        /// Logs informational messages about AI operations.
        /// </summary>
        public void LogInformation(string message)
        {
            try
            {
                var entry = new AILogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EntryType = "Information",
                    Query = message
                };

                _logEntries.Add(entry);

                _aiUsageLogger.Information("AI Info | Message: {Message}", TruncateForLog(message, 500));

                _logger.LogInformation("Logged AI information: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI information");
            }
        }

        /// <summary>
        /// Logs an error that occurred during AI processing.
        /// </summary>
        public void LogError(string query, string error, string errorType)
        {
            try
            {
                lock (_metricsLock)
                {
                    _totalErrors++;
                }

                var entry = new AILogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EntryType = "Error",
                    Query = query,
                    ErrorMessage = error,
                    ErrorType = errorType
                };

                _logEntries.Add(entry);

                _aiUsageLogger.Error(
                    "AI Error | Type: {ErrorType} | Query: {Query} | Error: {Error}",
                    errorType, TruncateForLog(query, 200), error);

                _logger.LogWarning("Logged AI error: Type={ErrorType}, Query={Query}", errorType, query);

                // Track telemetry event for AI error
                _errorReportingService.TrackEvent("AI_Error_Logged", new Dictionary<string, object>
                {
                    ["ErrorType"] = errorType,
                    ["QueryLength"] = query?.Length ?? 0,
                    ["ErrorMessage"] = error,
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI error");
            }
        }

        /// <summary>
        /// Logs an error with exception details.
        /// </summary>
        public void LogError(string query, Exception? exception)
        {
            try
            {
                var errorType = exception?.GetType().Name ?? "Unknown";
                var errorMessage = exception?.Message ?? "No error message";

                LogError(query, errorMessage, errorType);

                _aiUsageLogger.Error(exception,
                    "AI Exception | Query: {Query} | Exception Type: {ExceptionType}",
                    TruncateForLog(query, 200), errorType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI exception");
            }
        }

        /// <summary>
        /// Logs usage metrics for AI operations.
        /// </summary>
        public void LogMetric(string metricName, double metricValue, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var entry = new AILogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EntryType = "Metric",
                    MetricName = metricName,
                    MetricValue = metricValue,
                    Metadata = metadata
                };

                _logEntries.Add(entry);

                var metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : "{}";

                _aiUsageLogger.Information(
                    "AI Metric | Name: {MetricName} | Value: {MetricValue} | Metadata: {Metadata}",
                    metricName, metricValue, metadataJson);

                _logger.LogDebug("Logged AI metric: {MetricName}={MetricValue}", metricName, metricValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging AI metric");
            }
        }

        /// <summary>
        /// Gets usage statistics for a specified time period.
        /// </summary>
        public Task<Dictionary<string, object>> GetUsageStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var entriesInRange = _logEntries.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate).ToList();

                var queries = entriesInRange.Where(e => e.EntryType == "Query").ToList();
                var responses = entriesInRange.Where(e => e.EntryType == "Response").ToList();
                var errors = entriesInRange.Where(e => e.EntryType == "Error").ToList();

                var avgResponseTime = responses.Any() ? responses.Average(r => r.ResponseTimeMs) : 0;
                var totalTokens = responses.Sum(r => r.TokensUsed);
                var errorRate = queries.Any() ? (errors.Count * 100.0 / queries.Count) : 0;

                var statistics = new Dictionary<string, object>
                {
                    ["StartDate"] = startDate,
                    ["EndDate"] = endDate,
                    ["TotalQueries"] = queries.Count,
                    ["TotalResponses"] = responses.Count,
                    ["TotalErrors"] = errors.Count,
                    ["AverageResponseTimeMs"] = avgResponseTime,
                    ["TotalTokensUsed"] = totalTokens,
                    ["ErrorRatePercentage"] = errorRate,
                    ["SuccessRate"] = queries.Any() ? ((queries.Count - errors.Count) * 100.0 / queries.Count) : 0
                };

                _logger.LogInformation("Retrieved usage statistics: Queries={Queries}, Errors={Errors}, AvgResponseTime={AvgTime}ms",
                    queries.Count, errors.Count, avgResponseTime);

                return Task.FromResult(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage statistics");
                return Task.FromResult(new Dictionary<string, object>());
            }
        }

        /// <summary>
        /// Gets the count of queries made today.
        /// </summary>
        public int GetTodayQueryCount()
        {
            ResetDailyCountersIfNeeded();
            lock (_metricsLock)
            {
                return _todayQueryCount;
            }
        }

        /// <summary>
        /// Gets the average response time in milliseconds.
        /// </summary>
        public double GetAverageResponseTime()
        {
            lock (_metricsLock)
            {
                return _totalResponses > 0 ? _totalResponseTime / _totalResponses : 0;
            }
        }

        /// <summary>
        /// Gets the error rate as a percentage.
        /// </summary>
        public double GetErrorRate()
        {
            lock (_metricsLock)
            {
                var totalQueries = _todayQueryCount;
                return totalQueries > 0 ? (_totalErrors * 100.0 / totalQueries) : 0;
            }
        }

        /// <summary>
        /// Exports usage logs to a file for analysis.
        /// </summary>
        public async Task ExportLogsAsync(string filePath, DateTime startDate, DateTime endDate)
        {
            try
            {
                var entriesInRange = _logEntries
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(entriesInRange, jsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("Exported {Count} AI log entries to {FilePath}", entriesInRange.Count, filePath);
                _aiUsageLogger.Information("Log Export | File: {FilePath} | Entries: {Count} | Date Range: {Start} to {End}",
                    filePath, entriesInRange.Count, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting AI logs to {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Resets daily counters if a new day has started.
        /// </summary>
        private void ResetDailyCountersIfNeeded()
        {
            var today = DateTime.UtcNow.Date;
            lock (_metricsLock)
            {
                if (today > _lastResetDate)
                {
                    _todayQueryCount = 0;
                    _lastResetDate = today;
                    _logger.LogInformation("Daily AI counters reset for {Date}", today);
                }
            }
        }

        /// <summary>
        /// Truncates text for logging to avoid excessive log file size.
        /// </summary>
        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Internal class representing an AI log entry.
        /// </summary>
        private class AILogEntry
        {
            public DateTime Timestamp { get; set; }
            public string EntryType { get; set; }
            public string Query { get; set; }
            public string Context { get; set; }
            public string Model { get; set; }
            public string Response { get; set; }
            public long ResponseTimeMs { get; set; }
            public int TokensUsed { get; set; }
            public string ErrorMessage { get; set; }
            public string ErrorType { get; set; }
            public string MetricName { get; set; }
            public double MetricValue { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }
    }
}
