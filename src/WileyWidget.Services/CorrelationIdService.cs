using System.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Service that manages correlation IDs for tracking AI requests across the application
/// Enables distributed tracing and log correlation for debugging and monitoring
/// </summary>
public class CorrelationIdService
{
    private readonly ILogger<CorrelationIdService> _logger;
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Header name for correlation ID in HTTP requests
    /// </summary>
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    /// <summary>
    /// Activity tag name for correlation ID in OpenTelemetry
    /// </summary>
    public const string CorrelationIdTagName = "correlation_id";

    /// <summary>
    /// Initializes the correlation ID service
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public CorrelationIdService(ILogger<CorrelationIdService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current correlation ID for this async context
    /// </summary>
    public string? CurrentCorrelationId => _correlationId.Value;

    /// <summary>
    /// Generates a new correlation ID and sets it for the current context
    /// </summary>
    /// <returns>The generated correlation ID</returns>
    public string GenerateCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        SetCorrelationId(correlationId);
        return correlationId;
    }

    /// <summary>
    /// Sets the correlation ID for the current async context
    /// </summary>
    /// <param name="correlationId">Correlation ID to set</param>
    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty", nameof(correlationId));
        }

        _correlationId.Value = correlationId;

        // Add to current Activity for OpenTelemetry
        Activity.Current?.SetTag(CorrelationIdTagName, correlationId);

        _logger.LogDebug("Correlation ID set: {CorrelationId}", correlationId);
    }

    /// <summary>
    /// Clears the correlation ID from the current context
    /// </summary>
    public void ClearCorrelationId()
    {
        _correlationId.Value = null;
        _logger.LogDebug("Correlation ID cleared");
    }

    /// <summary>
    /// Executes an action within a correlation ID context
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided)</param>
    public void ExecuteInContext(Action action, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var id = correlationId ?? GenerateCorrelationId();

        using (LogContext.PushProperty("CorrelationId", id))
        {
            try
            {
                SetCorrelationId(id);
                action();
            }
            finally
            {
                ClearCorrelationId();
            }
        }
    }

    /// <summary>
    /// Executes an async function within a correlation ID context
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="func">Async function to execute</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided)</param>
    /// <returns>Result of the function</returns>
    public async Task<T> ExecuteInContextAsync<T>(Func<Task<T>> func, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(func);

        var id = correlationId ?? GenerateCorrelationId();

        using (LogContext.PushProperty("CorrelationId", id))
        {
            try
            {
                SetCorrelationId(id);
                return await func();
            }
            finally
            {
                ClearCorrelationId();
            }
        }
    }

    /// <summary>
    /// Executes an async action within a correlation ID context
    /// </summary>
    /// <param name="func">Async action to execute</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided)</param>
    public async Task ExecuteInContextAsync(Func<Task> func, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        var id = correlationId ?? GenerateCorrelationId();

        using (LogContext.PushProperty("CorrelationId", id))
        {
            try
            {
                SetCorrelationId(id);
                await func();
            }
            finally
            {
                ClearCorrelationId();
            }
        }
    }
}

/// <summary>
/// Extension methods for correlation ID management in AI services
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Creates a logging scope with correlation ID
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns>Disposable scope</returns>
    public static IDisposable BeginCorrelationScope(this ILogger logger, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
    }

    /// <summary>
    /// Logs an AI service call with correlation context
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="serviceName">Name of AI service</param>
    /// <param name="operation">Operation being performed</param>
    /// <param name="additionalData">Additional context data</param>
    public static void LogAIServiceCall(
        this ILogger logger,
        string correlationId,
        string serviceName,
        string operation,
        object? additionalData = null)
    {
        using (logger.BeginCorrelationScope(correlationId))
        {
            logger.LogInformation(
                "AI Service Call: {ServiceName}.{Operation} [CorrelationId: {CorrelationId}] {AdditionalData}",
                serviceName, operation, correlationId, additionalData);
        }
    }

    /// <summary>
    /// Logs an AI service error with correlation context
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="serviceName">Name of AI service</param>
    /// <param name="operation">Operation that failed</param>
    /// <param name="exception">Exception that occurred</param>
    public static void LogAIServiceError(
        this ILogger logger,
        string correlationId,
        string serviceName,
        string operation,
        Exception exception)
    {
        using (logger.BeginCorrelationScope(correlationId))
        {
            logger.LogError(
                exception,
                "AI Service Error: {ServiceName}.{Operation} [CorrelationId: {CorrelationId}]",
                serviceName, operation, correlationId);
        }
    }
}

/// <summary>
/// Example usage wrapper for AI services with correlation tracking
/// </summary>
public class CorrelatedAIServiceWrapper
{
    private readonly IAIService _aiService;
    private readonly CorrelationIdService _correlationIdService;
    private readonly ILogger<CorrelatedAIServiceWrapper> _logger;

    public CorrelatedAIServiceWrapper(
        IAIService aiService,
        CorrelationIdService correlationIdService,
        ILogger<CorrelatedAIServiceWrapper> logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _correlationIdService = correlationIdService ?? throw new ArgumentNullException(nameof(correlationIdService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets AI insights with correlation tracking
    /// </summary>
    /// <param name="context">Context for the query</param>
    /// <param name="question">Question to ask</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI insights</returns>
    public async Task<string> GetInsightsWithCorrelationAsync(
        string context,
        string question,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        return await _correlationIdService.ExecuteInContextAsync(async () =>
        {
            var id = _correlationIdService.CurrentCorrelationId ?? Guid.NewGuid().ToString("N");

            _logger.LogAIServiceCall(id, "XAIService", "GetInsights", new
            {
                Context = context.Length,
                QuestionLength = question.Length
            });

            try
            {
                var result = await _aiService.GetInsightsAsync(context, question, cancellationToken);

                _logger.LogInformation(
                    "AI insights retrieved successfully [CorrelationId: {CorrelationId}]",
                    id);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogAIServiceError(id, "XAIService", "GetInsights", ex);
                throw;
            }
        }, correlationId);
    }
}
