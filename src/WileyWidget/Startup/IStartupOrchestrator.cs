using System;
using System.Threading.Tasks;
using Prism.Ioc;

namespace WileyWidget.Startup;

/// <summary>
/// Orchestrates application startup phases with validation, telemetry, and error handling.
/// Works alongside Prism's lifecycle rather than replacing it.
/// </summary>
public interface IStartupOrchestrator
{
    /// <summary>
    /// Validates environment and configuration before Prism bootstrap begins.
    /// Called from App.OnStartup() before base.OnStartup().
    /// </summary>
    /// <returns>Startup result with validation details</returns>
    Task<StartupResult> ValidatePreStartupAsync();

    /// <summary>
    /// Validates services and performs post-bootstrap checks after Prism container is ready.
    /// Called from App.OnInitialized() with access to the configured container.
    /// </summary>
    /// <param name="container">Configured Prism container for service resolution</param>
    /// <returns>Startup result with validation details</returns>
    Task<StartupResult> ValidatePostBootstrapAsync(IContainerProvider container);

    /// <summary>
    /// Handles startup failure with appropriate rollback and telemetry.
    /// Called when any startup phase fails.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    /// <param name="phase">The startup phase where failure occurred</param>
    /// <returns>Task for async error handling</returns>
    Task HandleStartupFailureAsync(Exception exception, StartupPhase phase);

    /// <summary>
    /// Notifies that startup completed successfully and UI is ready.
    /// Called after shell creation and initial navigation.
    /// </summary>
    /// <returns>Task for async completion notification</returns>
    Task NotifyStartupCompleteAsync();
}

/// <summary>
/// Represents the result of a startup phase validation.
/// </summary>
public class StartupResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public Dictionary<string, object> ValidationDetails { get; init; } = new();
    public TimeSpan Duration { get; init; }

    public static StartupResult Success(string message, TimeSpan duration) => new()
    {
        IsSuccess = true,
        Message = message,
        Duration = duration
    };

    public static StartupResult Failure(string message, Exception? exception = null, TimeSpan duration = default) => new()
    {
        IsSuccess = false,
        Message = message,
        Exception = exception,
        Duration = duration
    };
}

/// <summary>
/// Identifies the startup phase for error handling and telemetry.
/// </summary>
public enum StartupPhase
{
    Unknown = 0,
    ConfigurationLoad = 1,
    ContainerSetup = 2,
    ModulesInit = 3,
    UILoad = 4
}
