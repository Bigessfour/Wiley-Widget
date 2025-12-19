using System.Collections.Generic;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service for validating dependency injection container registrations at runtime.
    /// Scans assemblies for service interfaces and attempts to resolve each one,
    /// identifying missing or misconfigured registrations before they cause runtime failures.
    ///
    /// Reference: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection
    /// </summary>
    public interface IDiValidationService
    {
        /// <summary>
        /// Validates all DI registrations by attempting to resolve each service interface
        /// found in the specified assemblies.
        /// </summary>
        /// <param name="assembliesToScan">
        /// Assemblies to scan for service interfaces (e.g., Services.Abstractions, Business.Interfaces).
        /// If null or empty, scans common service assemblies.
        /// </param>
        /// <param name="includeGenerics">
        /// If true, attempts to validate open generic types (e.g., IRepository&lt;T&gt;).
        /// Default is false as open generics require concrete type arguments.
        /// </param>
        /// <returns>
        /// Comprehensive validation report with resolved/missing services and any resolution errors.
        /// </returns>
        DiValidationReport ValidateRegistrations(
            IEnumerable<System.Reflection.Assembly>? assembliesToScan = null,
            bool includeGenerics = false);

        /// <summary>
        /// Performs quick validation of only core/critical services that must be present
        /// for the application to function (e.g., ISettingsService, IQuickBooksService, ITelemetryService).
        /// </summary>
        /// <returns>
        /// True if all core services can be resolved successfully, false if any are missing.
        /// </returns>
        bool ValidateCoreServices();

        /// <summary>
        /// Gets a list of all service interfaces discovered in the specified assemblies.
        /// Useful for inventory/documentation purposes.
        /// </summary>
        /// <param name="assembliesToScan">Assemblies to scan for interfaces.</param>
        /// <returns>List of full type names for all service interfaces found.</returns>
        IEnumerable<string> GetDiscoveredServiceInterfaces(
            IEnumerable<System.Reflection.Assembly>? assembliesToScan = null);

        /// <summary>
        /// Validates specific service categories (repositories, business services, etc.)
        /// with detailed timing and categorized results.
        /// </summary>
        /// <param name="serviceProvider">Service provider to validate against.</param>
        /// <param name="serviceTypes">Specific service types to validate.</param>
        /// <param name="categoryName">Name of the category being validated (for logging).</param>
        /// <returns>Detailed validation result with timing and categorized errors.</returns>
        DiValidationResult ValidateServiceCategory(
            System.IServiceProvider serviceProvider,
            System.Collections.Generic.IEnumerable<System.Type> serviceTypes,
            string categoryName);
    }

    /// <summary>
    /// Detailed validation result for categorized service validation.
    /// Includes timing, success/error messages, and summary formatting.
    /// </summary>
    public class DiValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> SuccessMessages { get; set; } = new();
        public System.TimeSpan ValidationDuration { get; set; }

        public string GetSummary()
        {
            return IsValid
                ? $"✓ All validations passed ({SuccessMessages.Count} services verified in {ValidationDuration.TotalMilliseconds:F0}ms)"
                : $"✗ Validation failed with {Errors.Count} errors and {Warnings.Count} warnings";
        }
    }

    /// <summary>
    /// Report containing results of DI validation scan.
    /// </summary>
    public class DiValidationReport
    {
        /// <summary>
        /// Services that were successfully resolved from the container.
        /// </summary>
        public List<string> ResolvedServices { get; set; } = new();

        /// <summary>
        /// Services that could not be resolved (missing registrations).
        /// </summary>
        public List<string> MissingServices { get; set; } = new();

        /// <summary>
        /// Errors encountered during resolution attempts (e.g., circular dependencies,
        /// constructor parameter issues, invalid scopes).
        /// </summary>
        public List<DiValidationError> Errors { get; set; } = new();

        /// <summary>
        /// True if all scanned services resolved successfully with no errors.
        /// </summary>
        public bool IsFullyValid => MissingServices.Count == 0 && Errors.Count == 0;

        /// <summary>
        /// Total number of services scanned.
        /// </summary>
        public int TotalServices => ResolvedServices.Count + MissingServices.Count;

        /// <summary>
        /// Percentage of services successfully resolved (0-100).
        /// </summary>
        public double ValidationSuccessRate => TotalServices > 0
            ? (ResolvedServices.Count / (double)TotalServices) * 100.0
            : 100.0;

        /// <summary>
        /// Gets a formatted summary of the validation results.
        /// </summary>
        public string GetSummary()
        {
            return $"DI Validation: {ResolvedServices.Count}/{TotalServices} resolved " +
                   $"({ValidationSuccessRate:F1}%), {MissingServices.Count} missing, {Errors.Count} errors";
        }
    }

    /// <summary>
    /// Detailed error information for a service that failed to resolve.
    /// </summary>
    public class DiValidationError
    {
        /// <summary>
        /// The service type that failed to resolve.
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;

        /// <summary>
        /// The exception message from the resolution attempt.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Optional stack trace for debugging complex resolution failures.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Suggested fix for the registration issue (e.g., "Add services.AddScoped<IFooService, FooService>()").
        /// </summary>
        public string? SuggestedFix { get; set; }

        public override string ToString()
        {
            return $"{ServiceType}: {ErrorMessage}" +
                   (SuggestedFix != null ? $" - {SuggestedFix}" : "");
        }
    }
}
