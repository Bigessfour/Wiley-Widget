using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of DI validation service that scans assemblies and validates
    /// that all service interfaces can be resolved from the DI container.
    ///
    /// Uses reflection to discover service interfaces and IServiceProvider to test resolution.
    /// Handles scoped services by creating temporary scopes, and provides detailed error reporting.
    ///
    /// Reference: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines
    /// </summary>
    public class DiValidationService : IDiValidationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DiValidationService> _logger;

        // Core services that MUST be present for app to function
        // Using Type.GetType with assembly-qualified names to avoid hard references
        private static Type[] GetCoreServiceTypes()
        {
            var types = new List<Type>
            {
                typeof(ISettingsService),
                typeof(ISecretVaultService),
                typeof(IQuickBooksService),
                typeof(IAIService),
                typeof(ITelemetryService),
                typeof(IAuditService),
                typeof(ICacheService)
            };

            return types.ToArray();
        }

        private static void TryAddType(List<Type> types, string assemblyQualifiedName)
        {
            try
            {
                var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
                if (type != null)
                {
                    types.Add(type);
                }
            }
            catch { /* Type not available - skip */ }
        }

        public DiValidationService(IServiceProvider serviceProvider, ILogger<DiValidationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DiValidationReport ValidateRegistrations(
            IEnumerable<Assembly>? assembliesToScan = null,
            bool includeGenerics = false)
        {
            _logger.LogInformation("[DI_VALIDATION] Starting DI validation scan");
            var report = new DiValidationReport();

            try
            {
                // If no assemblies specified, scan default service assemblies
                var assemblies = (assembliesToScan ?? GetDefaultServiceAssemblies()).ToList();
                _logger.LogDebug("[DI_VALIDATION] Scanning {AssemblyCount} assemblies", assemblies.Count);

                // Discover all service interfaces
                var serviceInterfaces = GetServiceInterfaces(assemblies, includeGenerics).ToList();
                _logger.LogInformation("[DI_VALIDATION] Discovered {InterfaceCount} service interfaces", serviceInterfaces.Count);

                // Attempt to resolve each service
                foreach (var serviceType in serviceInterfaces)
                {
                    try
                    {
                        bool resolved = TryResolveService(serviceType, out var error);

                        if (resolved)
                        {
                            report.ResolvedServices.Add(serviceType.FullName ?? serviceType.Name);
                            _logger.LogTrace("[DI_VALIDATION] ✓ {ServiceType}", serviceType.Name);
                        }
                        else
                        {
                            report.MissingServices.Add(serviceType.FullName ?? serviceType.Name);
                            _logger.LogWarning("[DI_VALIDATION] ✗ {ServiceType} - Missing registration", serviceType.Name);

                            if (error != null)
                            {
                                report.Errors.Add(error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = new DiValidationError
                        {
                            ServiceType = serviceType.FullName ?? serviceType.Name,
                            ErrorMessage = ex.Message,
                            StackTrace = ex.StackTrace,
                            SuggestedFix = GenerateSuggestedFix(serviceType)
                        };
                        report.Errors.Add(error);
                        _logger.LogError(ex, "[DI_VALIDATION] Exception resolving {ServiceType}", serviceType.Name);
                    }
                }

                _logger.LogInformation("[DI_VALIDATION] Validation complete: {Summary}", report.GetSummary());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DI_VALIDATION] Fatal error during validation");
                report.Errors.Add(new DiValidationError
                {
                    ServiceType = "VALIDATION_PROCESS",
                    ErrorMessage = $"Validation process failed: {ex.Message}",
                    StackTrace = ex.StackTrace
                });
            }

            return report;
        }

        public bool ValidateCoreServices()
        {
            _logger.LogInformation("[DI_VALIDATION] Validating core services");

            var coreServiceTypes = GetCoreServiceTypes();

            foreach (var serviceType in coreServiceTypes)
            {
                try
                {
                    // Try to resolve using both GetService (optional) and GetRequiredService patterns
                    bool resolved = TryResolveService(serviceType, out _);

                    if (!resolved)
                    {
                        _logger.LogError("[DI_VALIDATION] Core service missing: {ServiceType}", serviceType.Name);
                        return false;
                    }

                    _logger.LogTrace("[DI_VALIDATION] Core service OK: {ServiceType}", serviceType.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DI_VALIDATION] Failed to resolve core service: {ServiceType}", serviceType.Name);
                    return false;
                }
            }

            _logger.LogInformation("[DI_VALIDATION] All core services validated successfully");
            return true;
        }

        public IEnumerable<string> GetDiscoveredServiceInterfaces(IEnumerable<Assembly>? assembliesToScan = null)
        {
            var assemblies = (assembliesToScan ?? GetDefaultServiceAssemblies()).ToList();
            return GetServiceInterfaces(assemblies, includeGenerics: false)
                .Select(t => t.FullName ?? t.Name)
                .OrderBy(name => name);
        }

        /// <summary>
        /// Attempts to resolve a service from the DI container, handling scoped services appropriately.
        /// </summary>
        private bool TryResolveService(Type serviceType, out DiValidationError? error)
        {
            error = null;

            try
            {
                // For scoped services, create a temporary scope
                using var scope = _serviceProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;

                // Try GetService first (returns null if not registered)
                var instance = scopedProvider.GetService(serviceType);

                if (instance != null)
                {
                    return true;
                }

                // Service returned null - not registered
                error = new DiValidationError
                {
                    ServiceType = serviceType.FullName ?? serviceType.Name,
                    ErrorMessage = "Service not registered in DI container",
                    SuggestedFix = GenerateSuggestedFix(serviceType)
                };

                return false;
            }
            catch (InvalidOperationException ex)
            {
                // Service registration issue (e.g., missing dependency)
                error = new DiValidationError
                {
                    ServiceType = serviceType.FullName ?? serviceType.Name,
                    ErrorMessage = $"Registration error: {ex.Message}",
                    StackTrace = ex.StackTrace,
                    SuggestedFix = GenerateSuggestedFix(serviceType)
                };
                return false;
            }
        }

        /// <summary>
        /// Gets default assemblies to scan for service interfaces.
        /// </summary>
        private static IEnumerable<Assembly> GetDefaultServiceAssemblies()
        {
            var assemblies = new List<Assembly>();

            // Add Services.Abstractions assembly
            var abstractionsAssembly = typeof(ISettingsService).Assembly;
            assemblies.Add(abstractionsAssembly);

            // Add Business.Interfaces assembly (repositories)
            try
            {
                var businessAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "WileyWidget.Business");
                if (businessAssembly != null)
                {
                    assemblies.Add(businessAssembly);
                }
            }
            catch { /* Assembly not loaded */ }

            return assemblies;
        }

        /// <summary>
        /// Discovers service interfaces in the specified assemblies using reflection.
        /// </summary>
        private static IEnumerable<Type> GetServiceInterfaces(
            IEnumerable<Assembly> assemblies,
            bool includeGenerics)
        {
            return assemblies
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Handle partially loaded assemblies
                        return ex.Types.Where(t => t != null).Cast<Type>();
                    }
                })
                .Where(type => type.IsInterface)
                .Where(type => type.IsPublic)
                .Where(type => type.Name.StartsWith("I") && type.Name.Length > 1 && char.IsUpper(type.Name[1]))
                .Where(type => !IsExcludedInterface(type))
                .Where(type => !type.IsGenericType || includeGenerics)
                .Where(type => type.Namespace?.StartsWith("WileyWidget") == true)
                .Distinct();
        }

        /// <summary>
        /// Filters out framework interfaces that aren't DI services.
        /// </summary>
        private static bool IsExcludedInterface(Type type)
        {
            var excludedPrefixes = new[]
            {
                "IEnumerable",
                "ICollection",
                "IList",
                "IDictionary",
                "IReadOnly",
                "IDisposable",
                "IAsyncDisposable",
                "IComparable",
                "IEquatable",
                "IQueryable",
                "INotifyPropertyChanged",
                "ICommand"
            };

            return excludedPrefixes.Any(prefix => type.Name.StartsWith(prefix));
        }

        /// <summary>
        /// Generates a suggested fix for a missing service registration.
        /// </summary>
        private static string GenerateSuggestedFix(Type serviceType)
        {
            var serviceName = serviceType.Name;

            // Try to find implementation by convention (IFooService -> FooService)
            string implName = serviceName.StartsWith("I") && serviceName.Length > 1
                ? serviceName.Substring(1)
                : $"{serviceName}Impl";

            // Determine likely lifetime
            string lifetime = serviceName.Contains("Repository") || serviceName.Contains("DbContext")
                ? "Scoped"
                : serviceName.Contains("Service") || serviceName.Contains("Client")
                    ? "Singleton"
                    : "Transient";

            return $"Add services.Add{lifetime}<{serviceName}, {implName}>() to DI configuration";
        }
    }
}
