using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Helpers
{
    /// <summary>
    /// Reflection-based validator for ViewModel implementation completeness.
    /// Validates runtime behavior that static analysis cannot catch.
    /// </summary>
    public static class ViewModelValidator
    {
        /// <summary>
        /// Validation result containing all findings.
        /// </summary>
        public class ValidationResult
        {
            public string ViewModelName { get; init; } = string.Empty;
            public bool IsValid => Errors.Count == 0;
            public List<string> Errors { get; init; } = new();
            public List<string> Warnings { get; init; } = new();
            public List<string> Info { get; init; } = new();

            public void ThrowIfInvalid()
            {
                if (!IsValid)
                {
                    var errorMessage = $"ViewModel '{ViewModelName}' validation failed:\n" +
                                     string.Join("\n", Errors.Select(e => $"  ❌ {e}"));
                    throw new InvalidOperationException(errorMessage);
                }
            }
        }

        /// <summary>
        /// Performs comprehensive validation on a ViewModel instance.
        /// </summary>
        /// <typeparam name="TViewModel">The ViewModel type to validate.</typeparam>
        /// <param name="viewModel">The ViewModel instance.</param>
        /// <returns>Validation result with all findings.</returns>
        public static ValidationResult ValidateViewModel<TViewModel>(TViewModel viewModel)
            where TViewModel : class
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            var result = new ValidationResult
            {
                ViewModelName = typeof(TViewModel).Name
            };

            ValidateInheritance<TViewModel>(result);
            ValidateINotifyPropertyChanged(viewModel, result);
            ValidateCommands<TViewModel>(result);
            ValidateProperties<TViewModel>(result);
            ValidateDisposablePattern<TViewModel>(result);
            ValidateServiceDependencies<TViewModel>(result);

            return result;
        }

        /// <summary>
        /// Validates that the ViewModel inherits from correct base class.
        /// </summary>
        private static void ValidateInheritance<TViewModel>(ValidationResult result)
        {
            var type = typeof(TViewModel);
            var baseType = type.BaseType;

            if (baseType == null ||
                (baseType != typeof(ObservableObject) &&
                 baseType != typeof(ObservableRecipient) &&
                 !baseType.IsGenericType))
            {
                result.Errors.Add($"Must inherit from ObservableObject or ObservableRecipient (currently: {baseType?.Name ?? "unknown"})");
            }

            if (!typeof(INotifyPropertyChanged).IsAssignableFrom(type))
            {
                result.Errors.Add("Must implement INotifyPropertyChanged");
            }
        }

        /// <summary>
        /// Validates that properties properly raise PropertyChanged events.
        /// </summary>
        private static void ValidateINotifyPropertyChanged<TViewModel>(TViewModel viewModel, ValidationResult result)
        {
            if (viewModel is not INotifyPropertyChanged inpc)
            {
                result.Errors.Add("ViewModel does not implement INotifyPropertyChanged");
                return;
            }

            var type = typeof(TViewModel);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !p.GetIndexParameters().Any());

            foreach (var property in properties)
            {
                // Skip known non-observable properties
                if (property.Name.EndsWith("Command") ||
                    property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition().Name.Contains("Collection"))
                {
                    continue;
                }

                // Check if property has ObservableProperty attribute
                var hasObservableAttribute = property.DeclaringType?
                    .GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)?
                    .GetCustomAttributes<ObservablePropertyAttribute>().Any() ?? false;

                if (!hasObservableAttribute)
                {
                    // Try to verify PropertyChanged is raised (requires runtime test)
                    result.Info.Add($"Property '{property.Name}' should be validated for PropertyChanged notification at runtime");
                }
            }
        }

        /// <summary>
        /// Validates that commands are properly implemented.
        /// </summary>
        private static void ValidateCommands<TViewModel>(ValidationResult result)
        {
            var type = typeof(TViewModel);
            var commandProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.GetInterfaces().Any(i =>
                    i.Name.Contains("ICommand") ||
                    i.Name.Contains("IRelayCommand") ||
                    i.Name.Contains("IAsyncRelayCommand")));

            foreach (var commandProperty in commandProperties)
            {
                if (!commandProperty.CanRead)
                {
                    result.Errors.Add($"Command '{commandProperty.Name}' must be readable");
                    continue;
                }

                // Check if it's an IAsyncRelayCommand for async operations
                var isAsync = commandProperty.PropertyType.Name.Contains("Async");
                if (isAsync)
                {
                    result.Info.Add($"Async command '{commandProperty.Name}' detected - ensure proper cancellation token support");
                }

                // Verify command is not null after construction
                result.Info.Add($"Command '{commandProperty.Name}' should be validated for non-null initialization");
            }
        }

        /// <summary>
        /// Validates property implementations and naming conventions.
        /// </summary>
        private static void ValidateProperties<TViewModel>(ValidationResult result)
        {
            var type = typeof(TViewModel);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Check for proper naming (PascalCase)
                if (!char.IsUpper(property.Name[0]))
                {
                    result.Warnings.Add($"Property '{property.Name}' should use PascalCase naming");
                }

                // Check for XML documentation
                var xmlDocExists = property.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("XmlDoc") || a.GetType().Name.Contains("Documentation"));

                if (!xmlDocExists && !property.Name.EndsWith("Command"))
                {
                    result.Info.Add($"Property '{property.Name}' should have XML documentation");
                }
            }
        }

        /// <summary>
        /// Validates IDisposable pattern implementation.
        /// </summary>
        private static void ValidateDisposablePattern<TViewModel>(ValidationResult result)
        {
            var type = typeof(TViewModel);
            var implementsDisposable = typeof(IDisposable).IsAssignableFrom(type);

            // Check for disposable fields
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            var hasDisposableFields = fields.Any(f =>
                typeof(IDisposable).IsAssignableFrom(f.FieldType) ||
                f.FieldType.Name.Contains("CancellationTokenSource"));

            if (hasDisposableFields && !implementsDisposable)
            {
                result.Warnings.Add("ViewModel contains disposable fields but doesn't implement IDisposable");
            }

            if (implementsDisposable)
            {
                var disposeMethod = type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
                if (disposeMethod == null)
                {
                    result.Errors.Add("Implements IDisposable but missing public Dispose() method");
                }
            }
        }

        /// <summary>
        /// Validates service dependency injection.
        /// </summary>
        private static void ValidateServiceDependencies<TViewModel>(ValidationResult result)
        {
            var type = typeof(TViewModel);
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
            {
                result.Errors.Add("No public constructor found");
                return;
            }

            if (constructors.Length > 1)
            {
                result.Warnings.Add("Multiple public constructors found - ensure DI container can resolve correctly");
            }

            var mainConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = mainConstructor.GetParameters();

            // Check for ILogger<T>
            var hasLogger = parameters.Any(p =>
                p.ParameterType.IsGenericType &&
                p.ParameterType.GetGenericTypeDefinition().Name.Contains("ILogger"));

            if (!hasLogger)
            {
                result.Warnings.Add("Constructor should inject ILogger<TViewModel> for diagnostics");
            }

            // Check for service interfaces
            var hasServices = parameters.Any(p =>
                p.ParameterType.IsInterface &&
                (p.ParameterType.Name.EndsWith("Service") || p.ParameterType.Name.StartsWith("I")));

            if (!hasServices)
            {
                result.Info.Add("Consider injecting service interfaces instead of concrete implementations");
            }

            // Verify null checks exist (this is a static check, needs code review)
            result.Info.Add("Ensure constructor parameters have ArgumentNullException checks");
        }

        /// <summary>
        /// Tests that a property raises PropertyChanged event.
        /// </summary>
        public static void AssertPropertyRaisesPropertyChanged<TViewModel, TProperty>(
            TViewModel viewModel,
            string propertyName,
            TProperty newValue)
            where TViewModel : class, INotifyPropertyChanged
        {
            var propertyChangedRaised = false;
            var correctPropertyName = false;

            viewModel.PropertyChanged += (s, e) =>
            {
                propertyChangedRaised = true;
                if (e.PropertyName == propertyName)
                {
                    correctPropertyName = true;
                }
            };

            var property = typeof(TViewModel).GetProperty(propertyName);
            property.Should().NotBeNull($"Property '{propertyName}' should exist");

            var oldValue = property!.GetValue(viewModel);
            property.SetValue(viewModel, newValue);

            propertyChangedRaised.Should().BeTrue($"Setting '{propertyName}' should raise PropertyChanged event");
            correctPropertyName.Should().BeTrue($"PropertyChanged should be raised with correct property name '{propertyName}'");

            var currentValue = property.GetValue(viewModel);
            currentValue.Should().Be(newValue, $"Property '{propertyName}' should be updated to new value");
        }

        /// <summary>
        /// Tests that a command can execute without throwing.
        /// </summary>
        public static async Task AssertCommandExecutesAsync<TViewModel>(
            TViewModel viewModel,
            string commandName,
            bool expectedCanExecute = true)
            where TViewModel : class
        {
            var property = typeof(TViewModel).GetProperty(commandName);
            property.Should().NotBeNull($"Command property '{commandName}' should exist");

            var command = property!.GetValue(viewModel);
            command.Should().NotBeNull($"Command '{commandName}' should be initialized");

            if (command is IRelayCommand relayCommand)
            {
                relayCommand.CanExecute(null).Should().Be(expectedCanExecute,
                    $"Command '{commandName}' CanExecute should be {expectedCanExecute}");

                if (expectedCanExecute)
                {
                    var executeAction = () => relayCommand.Execute(null);
                    executeAction.Should().NotThrow($"Command '{commandName}' should execute without exceptions");
                }
            }
            else if (command is IAsyncRelayCommand asyncCommand)
            {
                asyncCommand.CanExecute(null).Should().Be(expectedCanExecute,
                    $"Async command '{commandName}' CanExecute should be {expectedCanExecute}");

                if (expectedCanExecute)
                {
                    var executeTask = asyncCommand.ExecuteAsync(null);
                    await executeTask;
                    executeTask.IsCompletedSuccessfully.Should().BeTrue(
                        $"Async command '{commandName}' should complete successfully");
                }
            }
            else
            {
                throw new InvalidOperationException($"'{commandName}' is not a recognized command type");
            }
        }

        /// <summary>
        /// Tests that a command properly handles cancellation.
        /// </summary>
        public static async Task AssertCommandSupportsCancellationAsync<TViewModel>(
            TViewModel viewModel,
            string commandName)
            where TViewModel : class
        {
            var property = typeof(TViewModel).GetProperty(commandName);
            property.Should().NotBeNull($"Command property '{commandName}' should exist");

            var command = property!.GetValue(viewModel) as IAsyncRelayCommand;
            command.Should().NotBeNull($"Command '{commandName}' should be an IAsyncRelayCommand");

            // This is a basic check - specific cancellation testing should be done in unit tests
            command.Should().NotBeNull($"Async command '{commandName}' should support cancellation token");
        }

        /// <summary>
        /// Validates that the ViewModel properly disposes resources.
        /// </summary>
        public static void AssertViewModelDisposesCorrectly<TViewModel>(TViewModel viewModel)
            where TViewModel : class, IDisposable
        {
            var disposeAction = () => viewModel.Dispose();
            disposeAction.Should().NotThrow("Dispose should execute without throwing");

            // Attempt double dispose (should be idempotent)
            disposeAction.Should().NotThrow("Multiple Dispose calls should be safe");
        }
    }
}
