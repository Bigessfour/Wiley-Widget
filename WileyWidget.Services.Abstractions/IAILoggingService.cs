using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for AI usage tracking and logging service.
    /// Monitors XAI API calls, response times, errors, and usage metrics for municipal finance AI operations.
    /// </summary>
    public interface IAILoggingService
    {
        /// <summary>
        /// Logs an AI query request to XAI service.
        /// </summary>
        /// <param name="query">The query text sent to AI</param>
        /// <param name="context">Context information provided with the query</param>
        /// <param name="model">AI model used (e.g., grok-4-0709)</param>
        void LogQuery(string query, string context, string model);

        /// <summary>
        /// Logs a successful AI response from XAI service.
        /// </summary>
        /// <param name="query">The original query</param>
        /// <param name="response">The AI response text</param>
        /// <param name="responseTimeMs">Response time in milliseconds</param>
        /// <param name="tokensUsed">Number of tokens consumed</param>
        void LogResponse(string query, string response, long responseTimeMs, int tokensUsed = 0);

        /// <summary>
        /// Logs an error that occurred during AI processing.
        /// </summary>
        /// <param name="query">The query that caused the error</param>
        /// <param name="error">The exception or error message</param>
        /// <param name="errorType">Type of error (e.g., API, Network, Timeout)</param>
        void LogError(string query, string error, string errorType);

        /// <summary>
        /// Logs an error that occurred during AI processing with exception details.
        /// </summary>
        /// <param name="query">The query that caused the error</param>
        /// <param name="exception">The exception that occurred</param>
        void LogError(string query, Exception exception);

        /// <summary>
        /// Logs usage metrics for AI operations.
        /// </summary>
        /// <param name="metricName">Name of the metric (e.g., DailyQueries, AverageResponseTime)</param>
        /// <param name="metricValue">Value of the metric</param>
        /// <param name="metadata">Additional metadata for the metric</param>
        void LogMetric(string metricName, double metricValue, Dictionary<string, object> metadata = null);

        /// <summary>
        /// Gets usage statistics for a specified time period.
        /// </summary>
        /// <param name="startDate">Start date for statistics</param>
        /// <param name="endDate">End date for statistics</param>
        /// <returns>Dictionary containing usage statistics</returns>
        Task<Dictionary<string, object>> GetUsageStatisticsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets the count of queries made today.
        /// </summary>
        /// <returns>Number of queries today</returns>
        int GetTodayQueryCount();

        /// <summary>
        /// Gets the average response time in milliseconds.
        /// </summary>
        /// <returns>Average response time</returns>
        double GetAverageResponseTime();

        /// <summary>
        /// Gets the error rate as a percentage.
        /// </summary>
        /// <returns>Error rate percentage (0-100)</returns>
        double GetErrorRate();

        /// <summary>
        /// Exports usage logs to a file for analysis.
        /// </summary>
        /// <param name="filePath">Path to export the logs</param>
        /// <param name="startDate">Start date for export</param>
        /// <param name="endDate">End date for export</param>
        /// <returns>Task representing the export operation</returns>
        Task ExportLogsAsync(string filePath, DateTime startDate, DateTime endDate);
    }
}
