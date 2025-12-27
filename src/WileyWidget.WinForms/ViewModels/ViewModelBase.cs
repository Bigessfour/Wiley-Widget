using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// Base class for ViewModels providing common null-safety infrastructure.
/// Ensures consistent logger fallback and dependency validation across all ViewModels.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Logger with guaranteed non-null instance (uses NullLogger fallback if DI fails)
    /// </summary>
    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Initializes a new instance of ViewModelBase with logger fallback.
    /// </summary>
    /// <param name="logger">Logger from DI (will use NullLogger if null)</param>
    protected ViewModelBase(ILogger? logger)
    {
        Logger = logger ?? CreateNullLogger();

        // Warn if fallback was used (indicates DI misconfiguration)
        if (logger == null)
        {
            Console.WriteLine($"[WARNING] {GetType().Name}: ILogger is null - using NullLogger fallback");
        }
    }

    /// <summary>
    /// Validates that a required dependency is not null.
    /// Throws InvalidOperationException with detailed context if null.
    /// </summary>
    /// <typeparam name="T">Type of dependency to validate</typeparam>
    /// <param name="value">Dependency value to check</param>
    /// <param name="parameterName">Name of the parameter (auto-captured)</param>
    /// <exception cref="InvalidOperationException">Thrown when dependency is null</exception>
    protected void ValidateRequired<T>(
        T? value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : class
    {
        if (value == null)
        {
            var message = $"CRITICAL: Required dependency '{parameterName}' is null in {GetType().Name}. Check DI registration.";
            Logger.LogCritical(message);
            Console.WriteLine($"[CRITICAL NULL] {message}");
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Validates multiple required dependencies in one call.
    /// </summary>
    /// <param name="dependencies">Dictionary of dependency name -> value pairs</param>
    /// <exception cref="InvalidOperationException">Thrown when any dependency is null</exception>
    /// <summary>
    /// Performs validaterequireddependencies. Parameters: Dictionary<string, dependencies.
    /// </summary>
    /// <param name="Dictionary<string">The Dictionary<string.</param>
    /// <param name="dependencies">The dependencies.</param>
    protected void ValidateRequiredDependencies(Dictionary<string, object?> dependencies)
    {
        var nullDependencies = dependencies
            .Where(kvp => kvp.Value == null)
            .Select(kvp => kvp.Key)
            .ToList();

        if (nullDependencies.Any())
        {
            var message = $"CRITICAL: Null dependencies in {GetType().Name}: {string.Join(", ", nullDependencies)}";
            Logger.LogCritical(message);
            Console.WriteLine($"[CRITICAL NULL] {message}");
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Creates a null logger instance using reflection to match the derived ViewModel type.
    /// This ensures NullLogger<DerivedViewModel> is used instead of NullLogger<ViewModelBase>.
    /// </summary>
    /// <summary>
    /// Performs createnulllogger.
    /// </summary>
    private ILogger CreateNullLogger()
    {
        var derivedType = GetType();
        var nullLoggerType = typeof(NullLogger<>).MakeGenericType(derivedType);
        var instanceProperty = nullLoggerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        return (ILogger)(instanceProperty?.GetValue(null) ?? throw new InvalidOperationException($"Failed to create NullLogger for {derivedType.Name}"));
    }

    /// <summary>
    /// Safely raise PropertyChanged on the UI thread when possible.
    /// Call this from derived ViewModels when performing property updates from background threads.
    /// </summary>
    /// <summary>
    /// Performs raisepropertychangedonuithread. Parameters: null.
    /// </summary>
    /// <param name="null">The null.</param>
    protected void RaisePropertyChangedOnUiThread([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        try
        {
            var dispatcher = Program.Services != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services)
                : null;

            if (dispatcher == null || dispatcher.CheckAccess())
            {
                base.OnPropertyChanged(propertyName);
                return;
            }

            // Post to UI thread to avoid deadlocks in synchronous callers and ensure PropertyChanged fires on UI thread
            _ = dispatcher.InvokeAsync(() => base.OnPropertyChanged(propertyName))
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Logger.LogError(t.Exception, "Failed to dispatch PropertyChanged for {Property}", propertyName);
                    }
                }, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            // As a safe fallback, raise property on calling thread
            Logger.LogWarning(ex, "RaisePropertyChangedOnUiThread dispatch failed - raising on calling thread for {Property}", propertyName);
            base.OnPropertyChanged(propertyName);
        }
    }
}
