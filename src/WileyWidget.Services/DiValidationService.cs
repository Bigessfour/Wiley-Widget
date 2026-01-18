using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IDiValidationService that provides runtime DI container validation.
    /// Redirects to the implementation in Abstractions if needed, or provides core logic.
    /// </summary>
    public class DiValidationService : WileyWidget.Services.Abstractions.IDiValidationService
    {
        private readonly ILogger<DiValidationService> _logger;

        public DiValidationService(ILogger<DiValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DiValidationReport ValidateRegistrations(IEnumerable<Assembly>? assembliesToScan = null, bool includeGenerics = false)
        {
            // Placeholder for full implementation
            return new DiValidationReport();
        }

        public bool ValidateCoreServices() => true;

        public IEnumerable<string> GetDiscoveredServiceInterfaces(IEnumerable<Assembly>? assembliesToScan = null) => Enumerable.Empty<string>();

        public DiValidationResult ValidateServiceCategory(IServiceProvider serviceProvider, IEnumerable<Type> serviceTypes, string categoryName)
        {
            return ValidateServiceCategory(serviceProvider, serviceTypes, categoryName, false);
        }

        public DiValidationResult ValidateServiceCategory(IServiceProvider serviceProvider, IEnumerable<Type> serviceTypes, string categoryName, bool fastValidation = false)
        {
            var result = new DiValidationResult();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("=== Starting {Category} Validation ({Mode}) ===", categoryName, fastValidation ? "Fast" : "Full");

            try
            {
                var isService = serviceProvider.GetService<IServiceProviderIsService>();

                foreach (var type in serviceTypes)
                {
                    try
                    {
                        if (fastValidation && isService != null)
                        {
                            if (isService.IsService(type))
                            {
                                result.SuccessMessages.Add($"✓ {type.Name} registered successfully");
                                _logger.LogInformation("OK {Type}", type.Name);
                            }
                            else
                            {
                                result.Errors.Add($"✗ {type.Name} is NOT registered");
                                _logger.LogError("FAIL {Type}", type.Name);
                            }
                            continue;
                        }

                        using var scope = serviceProvider.CreateScope();
                        var instance = scope.ServiceProvider.GetService(type);
                        if (instance != null)
                        {
                            result.SuccessMessages.Add($"✓ {type.Name} resolved successfully");
                            _logger.LogInformation("OK {Type}", type.Name);
                        }
                        else
                        {
                            result.Errors.Add($"✗ {type.Name} failed to resolve");
                            _logger.LogError("FAIL {Type}", type.Name);
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        // Timeout during service resolution (e.g., telemetry service)
                        var message = $"✗ {type.Name} resolution timed out (OperationCanceledException): {oce.Message}";
                        result.Errors.Add(message);
                        result.Warnings.Add($"{type.Name} initialization may have timed out");
                        _logger.LogWarning(oce, "TIMEOUT {Type}", type.Name);
                    }
                    catch (TimeoutException tex)
                    {
                        // Explicit timeout during service resolution
                        var message = $"✗ {type.Name} resolution timed out: {tex.Message}";
                        result.Errors.Add(message);
                        result.Warnings.Add($"{type.Name} took too long to initialize");
                        _logger.LogWarning(tex, "TIMEOUT {Type}", type.Name);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"✗ {type.Name} resolution error: {ex.GetType().Name}: {ex.Message}");
                        _logger.LogError(ex, "ERROR {Type}: {ExceptionType}", type.Name, ex.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                // Timeout during entire category validation
                var message = $"Category '{categoryName}' validation was canceled (timeout): {oce.Message}";
                result.Warnings.Add(message);
                result.Errors.Add(message);
                _logger.LogWarning(oce, "CATEGORY TIMEOUT {Category}", categoryName);
            }
            catch (TimeoutException tex)
            {
                // Explicit timeout during category validation
                var message = $"Category '{categoryName}' validation timed out: {tex.Message}";
                result.Warnings.Add(message);
                result.Errors.Add(message);
                _logger.LogWarning(tex, "CATEGORY TIMEOUT {Category}", categoryName);
            }
            catch (Exception ex)
            {
                // Unexpected error during category validation
                var message = $"Unexpected error during '{categoryName}' validation ({ex.GetType().Name}): {ex.Message}";
                result.Errors.Add(message);
                _logger.LogError(ex, "CATEGORY ERROR {Category}: {ExceptionType}", categoryName, ex.GetType().Name);
            }
            finally
            {
                stopwatch.Stop();
                result.ValidationDuration = stopwatch.Elapsed;
                result.IsValid = result.Errors.Count == 0;

                _logger.LogInformation("=== Completed {Category} Validation in {Duration}ms (Valid: {IsValid}) ===", 
                    categoryName, result.ValidationDuration.TotalMilliseconds, result.IsValid);
            }

            return result;
        }
    }
}
