using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Logging;
using WileyWidget.WinForms.Helpers;

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
            ConsoleOutputHelper.WriteLineSafe($"[WARNING] {GetType().Name}: ILogger is null - using NullLogger fallback");
        }
    }

    /// <summary>
    /// Validates that a required dependency is not null.
    /// Throws InvalidOperationException with detailed context if null.
    /// C# 14: Uses field keyword for cleaner validation logic with custom semantics.
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
            ConsoleOutputHelper.WriteLineSafe($"[CRITICAL NULL] {message}");
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Validates multiple required dependencies in one call.
    /// </summary>
    /// <param name="dependencies">Dictionary of dependency name -> value pairs</param>
    /// <exception cref="InvalidOperationException">Thrown when any dependency is null</exception>
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
            ConsoleOutputHelper.WriteLineSafe($"[CRITICAL NULL] {message}");
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Creates a null logger instance using reflection to match the derived ViewModel type.
    /// This ensures NullLogger<DerivedViewModel> is used instead of NullLogger<ViewModelBase>.
    /// </summary>
    private ILogger CreateNullLogger()
    {
        var derivedType = GetType();
        var nullLoggerType = typeof(NullLogger<>).MakeGenericType(derivedType);

        // Prefer a static property named 'Instance' but fall back to a static field if necessary
        var instanceProperty = nullLoggerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (instanceProperty != null)
        {
            var propValue = instanceProperty.GetValue(null);
            if (propValue is ILogger propLogger)
                return propLogger;
        }

        var instanceField = nullLoggerType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (instanceField != null)
        {
            var fieldValue = instanceField.GetValue(null);
            if (fieldValue is ILogger fieldLogger)
                return fieldLogger;
        }

        throw new InvalidOperationException($"Failed to create NullLogger for {derivedType.Name}");
    }
}
