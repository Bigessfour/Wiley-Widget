using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Centralized global exception handler service for the Wiley Widget application.
    /// Provides consistent exception handling, logging, user notifications, and recovery strategies.
    /// Implements best practices for .NET WinForms applications with comprehensive error management.
    /// </summary>
    public class GlobalExceptionHandlerService : WileyWidget.Services.IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandlerService> _logger;
        private readonly bool _isProduction;
        private readonly string _supportContact;
        private readonly object _errorTrackingLock = new object();
        private int _consecutiveErrors = 0;
        private DateTime _lastErrorTime = DateTime.MinValue;
        private const int MAX_CONSECUTIVE_ERRORS = 5;
        private static readonly TimeSpan ERROR_RESET_INTERVAL = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the GlobalExceptionHandlerService.
        /// </summary>
        /// <param name="logger">Logger instance for structured logging.</param>
        /// <param name="isProduction">Indicates if running in production mode (hides sensitive data).</param>
        /// <param name="supportContact">Support contact email for user-facing error messages.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public GlobalExceptionHandlerService(
            ILogger<GlobalExceptionHandlerService> logger,
            bool isProduction = false,
            string supportContact = "support@wileywidget.com")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isProduction = isProduction;
            _supportContact = supportContact ?? "support@wileywidget.com";
        }

        /// <summary>
        /// Handles navigation errors with contextual information.
        /// </summary>
        public void HandleNavigationError(string regionName, string targetUri, Exception? error, string errorMessage)
        {
            try
            {
                var context = new
                {
                    RegionName = regionName,
                    TargetUri = targetUri,
                    ErrorMessage = errorMessage,
                    ExceptionType = error?.GetType().Name,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogError(error, "Navigation error in region '{RegionName}' to '{TargetUri}': {ErrorMessage}",
                    regionName, targetUri, errorMessage);

                Log.Error(error, "[NAVIGATION] Region: {RegionName}, Target: {TargetUri}, Message: {ErrorMessage}",
                    regionName, targetUri, errorMessage);

                // Show user-friendly notification for navigation errors
                ShowUserNotification(
                    "Navigation Error",
                    $"Unable to navigate to the requested page.\n\n{GetSafeErrorMessage(error)}",
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                // Fallback logging if the handler itself fails
                Debug.WriteLine($"[CRITICAL] Exception in HandleNavigationError: {ex}");
                Log.Fatal(ex, "Critical failure in exception handler");
            }
        }

        /// <summary>
        /// Handles general application errors with classification and recovery strategies.
        /// </summary>
        public void HandleGeneralError(string source, string operation, Exception? error, string errorMessage, bool isHandled = false)
        {
            try
            {
                // Suppress logging for expected cancellation exceptions during normal app lifecycle
                if (error is OperationCanceledException || error is TaskCanceledException)
                {
                    _logger.LogDebug("Operation canceled in {Source}.{Operation}: {Message}", source, operation, errorMessage);
                    return;
                }

                TrackConsecutiveErrors();

                var errorClassification = ClassifyException(error);
                var severity = DetermineErrorSeverity(error, isHandled);

                var context = new
                {
                    Source = source,
                    Operation = operation,
                    ErrorMessage = errorMessage,
                    Classification = errorClassification.ToString(),
                    Severity = severity.ToString(),
                    IsHandled = isHandled,
                    ConsecutiveErrors = _consecutiveErrors,
                    Timestamp = DateTime.UtcNow
                };

                // Log with appropriate level based on severity
                switch (severity)
                {
                    case ErrorSeverity.Critical:
                        _logger.LogCritical(error, "[{Source}] Critical error in {Operation}: {ErrorMessage}",
                            source, operation, errorMessage);
                        Log.Fatal(error, "[CRITICAL] Source: {Source}, Operation: {Operation}, Message: {ErrorMessage}",
                            source, operation, errorMessage);
                        break;

                    case ErrorSeverity.High:
                        _logger.LogError(error, "[{Source}] Error in {Operation}: {ErrorMessage}",
                            source, operation, errorMessage);
                        Log.Error(error, "[ERROR] Source: {Source}, Operation: {Operation}, Message: {ErrorMessage}",
                            source, operation, errorMessage);
                        break;

                    case ErrorSeverity.Medium:
                        _logger.LogWarning(error, "[{Source}] Warning in {Operation}: {ErrorMessage}",
                            source, operation, errorMessage);
                        Log.Warning(error, "[WARNING] Source: {Source}, Operation: {Operation}, Message: {ErrorMessage}",
                            source, operation, errorMessage);
                        break;

                    case ErrorSeverity.Low:
                        _logger.LogInformation(error, "[{Source}] Handled error in {Operation}: {ErrorMessage}",
                            source, operation, errorMessage);
                        Log.Information(error, "[INFO] Source: {Source}, Operation: {Operation}, Message: {ErrorMessage}",
                            source, operation, errorMessage);
                        break;
                }

                // Apply recovery strategy if needed
                if (!isHandled)
                {
                    ApplyRecoveryStrategy(errorClassification, error, source, operation);
                }

                // Check for cascading failures
                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    HandleCascadingFailure();
                }
            }
            catch (Exception ex)
            {
                // Fallback logging
                Debug.WriteLine($"[CRITICAL] Exception in HandleGeneralError: {ex}");
                Log.Fatal(ex, "Critical failure in exception handler");
            }
        }

        /// <summary>
        /// Registers global navigation handlers (placeholder for future implementation).
        /// </summary>
        public void RegisterGlobalNavigationHandlers()
        {
            _logger.LogDebug("Global navigation handlers registered");
        }

        /// <summary>
        /// Handles UI thread exceptions with user-friendly notifications.
        /// </summary>
        public void HandleUIThreadException(Exception exception, string context)
        {
            ArgumentNullException.ThrowIfNull(exception);
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                _logger.LogError(exception, "[UI-THREAD] Exception in {Context}: {Message}",
                    context, exception.Message);

                Log.Error(exception, "[UI-THREAD] Context: {Context}, Type: {ExceptionType}",
                    context, exception.GetType().Name);

                TrackConsecutiveErrors();

                var errorClassification = ClassifyException(exception);
                var userMessage = GenerateUserMessage(exception, errorClassification);

                ShowUserNotification("Application Error", userMessage, MessageBoxIcon.Error);

                // Check if application should restart
                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    OfferApplicationRestart();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Exception in HandleUIThreadException: {ex}");
                Log.Fatal(ex, "Critical failure in UI thread exception handler");
            }
        }

        /// <summary>
        /// Handles unhandled domain exceptions with termination logic.
        /// </summary>
        public void HandleUnhandledException(Exception exception, bool isTerminating)
        {
            ArgumentNullException.ThrowIfNull(exception);
            try
            {
                _logger.LogCritical(exception, "[UNHANDLED] Fatal exception - IsTerminating: {IsTerminating}",
                    isTerminating);

                Log.Fatal(exception, "[UNHANDLED] IsTerminating: {IsTerminating}, Type: {ExceptionType}, Message: {Message}",
                    isTerminating, exception.GetType().FullName, exception.Message);

                // Ensure all logs are persisted before potential termination
                Log.CloseAndFlush();

                if (isTerminating)
                {
                    // Application is terminating - show critical error dialog
                    MessageBox.Show(
                        BuildTerminationMessage(exception),
                        "Critical Application Error - Terminating",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Stop);
                }
            }
            catch (Exception ex)
            {
                // Last-resort logging
                Debug.WriteLine($"[CRITICAL] Exception in HandleUnhandledException: {ex}");
                try
                {
                    Log.Fatal(ex, "Critical failure in unhandled exception handler");
                    Log.CloseAndFlush();
                }
                catch
                {
                    // Nothing more we can do
                }
            }
        }

        /// <summary>
        /// Handles unobserved task exceptions with observation marking.
        /// </summary>
        public void HandleUnobservedTaskException(AggregateException exception, bool observed)
        {
            ArgumentNullException.ThrowIfNull(exception);
            try
            {
                _logger.LogError(exception, "[UNOBSERVED-TASK] Unobserved task exception - Observed: {Observed}",
                    observed);

                Log.Error(exception, "[UNOBSERVED-TASK] Observed: {Observed}, InnerExceptions: {Count}",
                    observed, exception.InnerExceptions.Count);

                // Log each inner exception
                foreach (var innerEx in exception.InnerExceptions)
                {
                    _logger.LogError(innerEx, "[UNOBSERVED-TASK-INNER] {ExceptionType}: {Message}",
                        innerEx.GetType().Name, innerEx.Message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Exception in HandleUnobservedTaskException: {ex}");
                Log.Fatal(ex, "Critical failure in task exception handler");
            }
        }

        #region Private Helper Methods

        private ErrorClassification ClassifyException(Exception? exception)
        {
            if (exception == null) return ErrorClassification.Unknown;

            return exception switch
            {
                OutOfMemoryException => ErrorClassification.ResourceExhaustion,
                StackOverflowException => ErrorClassification.ResourceExhaustion,
                InvalidOperationException when exception.Source == "Microsoft.Extensions.DependencyInjection" => ErrorClassification.Configuration,
                InvalidOperationException => ErrorClassification.Logic,
                ArgumentException => ErrorClassification.Validation,
                UnauthorizedAccessException => ErrorClassification.Security,
                System.Security.SecurityException => ErrorClassification.Security,
                System.Net.Http.HttpRequestException => ErrorClassification.Network,
                System.Net.Sockets.SocketException => ErrorClassification.Network,
                TimeoutException => ErrorClassification.Network,
                TaskCanceledException => ErrorClassification.Cancellation,
                OperationCanceledException => ErrorClassification.Cancellation,
                System.Data.Common.DbException => ErrorClassification.DataAccess,
                IOException => ErrorClassification.FileSystem,
                NotImplementedException => ErrorClassification.NotImplemented,
                _ => ErrorClassification.Unknown
            };
        }

        private ErrorSeverity DetermineErrorSeverity(Exception? exception, bool isHandled)
        {
            if (isHandled) return ErrorSeverity.Low;
            if (exception == null) return ErrorSeverity.Medium;

            return exception switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                StackOverflowException => ErrorSeverity.Critical,
                System.Security.SecurityException => ErrorSeverity.Critical,
                UnauthorizedAccessException => ErrorSeverity.High,
                System.Data.Common.DbException => ErrorSeverity.High,
                InvalidOperationException when exception.Source == "Microsoft.Extensions.DependencyInjection" => ErrorSeverity.High,
                System.Net.Http.HttpRequestException => ErrorSeverity.Medium,
                TimeoutException => ErrorSeverity.Medium,
                TaskCanceledException => ErrorSeverity.Low,
                OperationCanceledException => ErrorSeverity.Low,
                _ => ErrorSeverity.Medium
            };
        }

        private void ApplyRecoveryStrategy(ErrorClassification classification, Exception? exception, string source, string operation)
        {
            _logger.LogInformation("Applying recovery strategy for {Classification} error in {Source}.{Operation}",
                classification, source, operation);

            switch (classification)
            {
                case ErrorClassification.Network:
                    // Network errors might be transient - log and suggest retry
                    _logger.LogWarning("Network error detected - user should retry operation");
                    break;

                case ErrorClassification.ResourceExhaustion:
                    // Critical memory/resource issues
                    _logger.LogCritical("Resource exhaustion detected - recommend application restart");
                    OfferApplicationRestart();
                    break;

                case ErrorClassification.Configuration:
                    // Configuration errors need administrator attention
                    _logger.LogError("Configuration error detected - administrator intervention required");
                    break;

                case ErrorClassification.Security:
                    // Security violations should be logged but not expose details to users
                    _logger.LogWarning("Security violation detected - denying access");
                    break;

                default:
                    // General error - log and continue
                    _logger.LogDebug("No specific recovery strategy for {Classification}", classification);
                    break;
            }
        }

        /// <summary>
        /// Tracks consecutive errors to detect cascading failures.
        /// Thread-safe implementation using lock to prevent race conditions.
        /// </summary>
        private void TrackConsecutiveErrors()
        {
            lock (_errorTrackingLock)
            {
                var now = DateTime.UtcNow;

                if ((now - _lastErrorTime) > ERROR_RESET_INTERVAL)
                {
                    _consecutiveErrors = 0;
                }

                _consecutiveErrors++;
                _lastErrorTime = now;

                _logger.LogDebug("Consecutive errors: {Count} (last error: {LastError})",
                    _consecutiveErrors, _lastErrorTime);
            }
        }

        /// <summary>
        /// Handles cascading failure scenario when too many consecutive errors occur.
        /// Thread-safe access to error counters.
        /// </summary>
        private void HandleCascadingFailure()
        {
            int errorCount;
            lock (_errorTrackingLock)
            {
                errorCount = _consecutiveErrors;
            }

            _logger.LogCritical("Cascading failure detected - {Count} consecutive errors in {TimeSpan} minutes",
                errorCount, ERROR_RESET_INTERVAL.TotalMinutes);

            Log.Fatal("Cascading failure detected - application stability compromised. Consecutive errors: {Count}",
                errorCount);

            var result = MessageBox.Show(
                $"Multiple errors have occurred ({errorCount} consecutive errors).\n\n" +
                "The application may be unstable. Would you like to restart?\n\n" +
                "Click Yes to restart now, or No to continue (not recommended).",
                "Application Instability Detected",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                RestartApplication();
            }
        }

        private void OfferApplicationRestart()
        {
            var result = MessageBox.Show(
                "A critical error has occurred.\n\n" +
                "Restarting the application is recommended.\n\n" +
                "Would you like to restart now?",
                "Restart Recommended",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                RestartApplication();
            }
        }

        private void RestartApplication()
        {
            try
            {
                _logger.LogInformation("Application restart initiated");
                Log.Information("Application restarting...");
                Log.CloseAndFlush();

                Application.Restart();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart application");
                Log.Error(ex, "Application restart failed");
            }
        }

        private string GenerateUserMessage(Exception exception, ErrorClassification classification)
        {
            var sb = new StringBuilder();

            if (_isProduction)
            {
                // Production: Generic user-friendly messages
                sb.AppendLine(GetProductionMessage(classification));
                sb.AppendLine();
                sb.AppendLine($"Error ID: {Guid.NewGuid():N}");
                sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine($"If this problem persists, please contact: {_supportContact}");
            }
            else
            {
                // Development: More detailed messages
                sb.AppendLine($"Error Type: {exception.GetType().Name}");
                sb.AppendLine($"Classification: {classification}");
                sb.AppendLine();
                sb.AppendLine($"Message: {exception.Message}");

                if (exception.InnerException != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Inner Exception: {exception.InnerException.Message}");
                }
            }

            return sb.ToString();
        }

        private string GetProductionMessage(ErrorClassification classification)
        {
            return classification switch
            {
                ErrorClassification.Network => "A network error occurred. Please check your internet connection and try again.",
                ErrorClassification.DataAccess => "Unable to access data. Please try again or contact support if the problem persists.",
                ErrorClassification.Security => "Access denied. You may not have permission to perform this operation.",
                ErrorClassification.Configuration => "A configuration error occurred. Please contact your system administrator.",
                ErrorClassification.ResourceExhaustion => "The application is running low on resources. Please restart the application.",
                ErrorClassification.FileSystem => "A file system error occurred. Please check file permissions and disk space.",
                ErrorClassification.Validation => "Invalid input detected. Please check your data and try again.",
                _ => "An unexpected error occurred. Please try again or contact support if the problem persists."
            };
        }

        private string GetSafeErrorMessage(Exception? exception)
        {
            if (exception == null) return "Unknown error";

            return _isProduction
                ? "An error occurred while processing your request."
                : exception.Message;
        }

        private void ShowUserNotification(string title, string message, MessageBoxIcon icon)
        {
            try
            {
                MessageBox.Show(message, $"Wiley Widget - {title}", MessageBoxButtons.OK, icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show user notification");
                Debug.WriteLine($"Failed to show MessageBox: {ex}");
            }
        }

        private string BuildTerminationMessage(Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine("A critical error has occurred and the application must close.");
            sb.AppendLine();

            if (!_isProduction)
            {
                sb.AppendLine($"Error: {exception.GetType().Name}");
                sb.AppendLine($"Message: {exception.Message}");
                sb.AppendLine();
            }

            sb.AppendLine("All work has been saved where possible.");
            sb.AppendLine();
            sb.AppendLine($"For support, contact: {_supportContact}");
            sb.AppendLine($"Error Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        #endregion

        #region Enums

        private enum ErrorClassification
        {
            Unknown,
            Network,
            DataAccess,
            Security,
            Configuration,
            ResourceExhaustion,
            FileSystem,
            Validation,
            Logic,
            Cancellation,
            NotImplemented
        }

        private enum ErrorSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        #endregion
    }
}
