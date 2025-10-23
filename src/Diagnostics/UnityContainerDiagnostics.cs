using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Unity;
using Unity.Registration;
using Unity.Resolution;
using Prism.Ioc;
using Serilog;

namespace WileyWidget.Diagnostics
{
    /// <summary>
    /// Diagnostic utilities for troubleshooting Unity container issues in Prism WPF applications.
    /// Provides detailed analysis of registration failures, circular dependencies, and resolution issues.
    /// </summary>
    public static class UnityContainerDiagnostics
    {
        /// <summary>
        /// Tests container registration resolution with detailed error reporting.
        /// Useful for debugging TargetInvocationException issues during startup.
        /// </summary>
        /// <param name="containerRegistry">The Prism container registry</param>
        /// <param name="testMode">Whether running in test mode (affects which services to test)</param>
        /// <returns>Diagnostic report with resolution results</returns>
        public static ContainerDiagnosticReport TestContainerResolutions(IContainerRegistry containerRegistry, bool testMode = false)
        {
            var report = new ContainerDiagnosticReport();
            var unityContainer = containerRegistry.GetContainer();

            Log.Information("=== Starting Unity Container Resolution Diagnostics ===");

            // Test critical service resolutions
            var criticalServices = GetCriticalServices(testMode);
            foreach (var (serviceName, serviceType) in criticalServices)
            {
                TestServiceResolution(unityContainer, serviceName, serviceType, report);
            }

            // Test for circular dependencies
            DetectCircularDependencies(unityContainer, report);

            // Test ViewModel resolutions (common failure point)
            TestViewModelResolutions(containerRegistry, report);

            Log.Information("=== Container Resolution Diagnostics Complete ===");
            Log.Information("Passed: {Passed}, Failed: {Failed}, Warnings: {Warnings}",
                report.PassedResolutions.Count,
                report.FailedResolutions.Count,
                report.Warnings.Count);

            return report;
        }

        /// <summary>
        /// Attempts to resolve a service and provides detailed diagnostic information.
        /// </summary>
        /// <param name="container">The Unity container</param>
        /// <param name="serviceName">Display name for the service</param>
        /// <param name="serviceType">The service type to resolve</param>
        /// <param name="report">The diagnostic report to update</param>
        private static void TestServiceResolution(IUnityContainer container, string serviceName, Type serviceType, ContainerDiagnosticReport report)
        {
            try
            {
                Log.Debug("Testing resolution of {ServiceName} ({ServiceType})", serviceName, serviceType.Name);

                var stopwatch = Stopwatch.StartNew();
                var instance = container.Resolve(serviceType);
                stopwatch.Stop();

                if (instance == null)
                {
                    var warning = $"{serviceName} resolved to null";
                    report.Warnings.Add(warning);
                    Log.Warning(warning);
                    return;
                }

                var success = $"{serviceName} resolved successfully in {stopwatch.ElapsedMilliseconds}ms";
                report.PassedResolutions.Add(success);
                Log.Debug(success);

                // Additional validation for common issues
                ValidateResolvedInstance(serviceName, serviceType, instance, report);
            }
            catch (Exception ex)
            {
                var rootException = UnwrapTargetInvocationException(ex);
                var failure = $"{serviceName} failed to resolve: {rootException.Message}";
                report.FailedResolutions.Add(new FailedResolution
                {
                    ServiceName = serviceName,
                    ServiceType = serviceType,
                    Exception = rootException,
                    ResolutionPath = GetResolutionPath(container, serviceType)
                });

                Log.Error(rootException, failure);
                Log.Error("Resolution path: {Path}", GetResolutionPath(container, serviceType));
            }
        }

        /// <summary>
        /// Validates a successfully resolved instance for common issues.
        /// </summary>
        private static void ValidateResolvedInstance(string serviceName, Type serviceType, object instance, ContainerDiagnosticReport report)
        {
            // Check for logger dependencies (common issue)
            if (serviceName.Contains("ViewModel") && instance != null)
            {
                var loggerProperty = instance.GetType().GetProperty("Logger") ??
                                   instance.GetType().GetProperty("_logger", BindingFlags.NonPublic | BindingFlags.Instance);

                if (loggerProperty != null)
                {
                    var loggerValue = loggerProperty.GetValue(instance);
                    if (loggerValue == null)
                    {
                        report.Warnings.Add($"{serviceName} has null logger - check ILogger<> registration");
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to detect circular dependencies in the container.
        /// </summary>
        private static void DetectCircularDependencies(IUnityContainer container, ContainerDiagnosticReport report)
        {
            try
            {
                var registrations = container.Registrations.ToList();

                foreach (var registration in registrations)
                {
                    if (registration.RegisteredType != null && registration.MappedToType != null)
                    {
                        // Try to detect potential circular references by checking constructor parameters
                        DetectCircularReference(registration.MappedToType, new HashSet<Type>(), report);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to analyze circular dependencies");
            }
        }

        /// <summary>
        /// Recursively checks for circular references in constructor parameters.
        /// </summary>
        private static void DetectCircularReference(Type type, HashSet<Type> visited, ContainerDiagnosticReport report)
        {
            if (visited.Contains(type))
            {
                report.Warnings.Add($"Potential circular dependency detected involving {type.Name}");
                return;
            }

            visited.Add(type);

            try
            {
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (var constructor in constructors)
                {
                    foreach (var parameter in constructor.GetParameters())
                    {
                        if (parameter.ParameterType.IsClass && !parameter.ParameterType.IsPrimitive)
                        {
                            DetectCircularReference(parameter.ParameterType, new HashSet<Type>(visited), report);
                        }
                    }
                }
            }
            finally
            {
                visited.Remove(type);
            }
        }

        /// <summary>
        /// Tests ViewModel resolutions which are common failure points.
        /// </summary>
        private static void TestViewModelResolutions(IContainerRegistry containerRegistry, ContainerDiagnosticReport report)
        {
            var viewModelTypes = new[]
            {
                "MainViewModel",
                "AboutViewModel",
                "EnterpriseViewModel",
                "ExcelImportViewModel"
            };

            foreach (var viewModelName in viewModelTypes)
            {
                var viewModelType = Type.GetType($"WileyWidget.ViewModels.{viewModelName}, WileyWidget");
                if (viewModelType != null)
                {
                    TestServiceResolution(containerRegistry.GetContainer(), viewModelName, viewModelType, report);
                }
            }
        }

        /// <summary>
        /// Gets the list of critical services to test based on test mode.
        /// </summary>
        private static (string Name, Type Type)[] GetCriticalServices(bool testMode)
        {
            var services = new List<(string, Type)>
            {
                ("IConfiguration", typeof(Microsoft.Extensions.Configuration.IConfiguration)),
                ("ILoggerFactory", typeof(Microsoft.Extensions.Logging.ILoggerFactory)),
                ("ISettingsService", typeof(WileyWidget.Services.ISettingsService)),
                ("IEnterpriseRepository", typeof(WileyWidget.Business.Interfaces.IEnterpriseRepository)),
                ("IBudgetRepository", typeof(WileyWidget.Business.Interfaces.IBudgetRepository)),
                ("IModuleHealthService", typeof(WileyWidget.Services.IModuleHealthService)),
                ("IEventAggregator", typeof(Prism.Events.IEventAggregator))
                // ("IRegionManager", typeof(IRegionManager)) // Removed with Prism.Regions
            };

            if (!testMode)
            {
                services.AddRange(new[]
                {
                    ("IAIService", typeof(WileyWidget.Services.IAIService)),
                    ("IGrokSupercomputer", typeof(WileyWidget.Services.IGrokSupercomputer)),
                    ("IWileyWidgetContextService", typeof(WileyWidget.Services.IWileyWidgetContextService))
                });
            }

            return services.ToArray();
        }

        /// <summary>
        /// Attempts to build a resolution path for debugging.
        /// </summary>
        private static string GetResolutionPath(IUnityContainer container, Type serviceType)
        {
            try
            {
                var registration = container.Registrations.FirstOrDefault(r => r.RegisteredType == serviceType);
                if (registration != null && registration.MappedToType != null)
                {
                    return $"{serviceType.Name} -> {registration.MappedToType.Name}";
                }
            }
            catch
            {
                // Ignore errors in path building
            }

            return "Unknown resolution path";
        }

        /// <summary>
        /// Unwraps TargetInvocationException to get to the root cause.
        /// </summary>
        private static Exception UnwrapTargetInvocationException(Exception exception)
        {
            var current = exception;
            var seen = new HashSet<Exception>();

            while (current is TargetInvocationException tie && tie.InnerException != null && !seen.Contains(tie))
            {
                seen.Add(tie);
                current = tie.InnerException;
            }

            return current;
        }
    }

    /// <summary>
    /// Diagnostic report for container resolution testing.
    /// </summary>
    public class ContainerDiagnosticReport
    {
        public List<string> PassedResolutions { get; } = new();
        public List<FailedResolution> FailedResolutions { get; } = new();
        public List<string> Warnings { get; } = new();

        public bool HasFailures => FailedResolutions.Any();
        public bool HasWarnings => Warnings.Any();

        public void LogSummary()
        {
            Log.Information("=== Container Diagnostic Summary ===");
            Log.Information("Passed: {Count}", PassedResolutions.Count);
            Log.Information("Failed: {Count}", FailedResolutions.Count);
            Log.Information("Warnings: {Count}", Warnings.Count);

            if (HasFailures)
            {
                Log.Error("Failed Resolutions:");
                foreach (var failure in FailedResolutions)
                {
                    Log.Error("  - {Service}: {Message}", failure.ServiceName, failure.Exception.Message);
                }
            }

            if (HasWarnings)
            {
                Log.Warning("Warnings:");
                foreach (var warning in Warnings)
                {
                    Log.Warning("  - {Warning}", warning);
                }
            }
        }
    }

    /// <summary>
    /// Represents a failed service resolution.
    /// </summary>
    public class FailedResolution
    {
        public string ServiceName { get; set; } = string.Empty;
        public Type ServiceType { get; set; } = typeof(object);
        public Exception Exception { get; set; } = new Exception();
        public string ResolutionPath { get; set; } = string.Empty;
    }
}
