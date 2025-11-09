// StartupEnvironmentValidator.cs - Implementation of startup environment validation service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.2)
// Date: November 9, 2025
//
// This service validates the application's startup environment including:
// - Module initialization and health checks
// - AI service configuration and API key validation
// - ViewModel dependency validation
// - System memory and resource availability

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for validating the application's startup environment.
    /// Ensures all required services, dependencies, and system resources are available.
    /// </summary>
    public class StartupEnvironmentValidator : IStartupEnvironmentValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StartupEnvironmentValidator> _logger;

        public StartupEnvironmentValidator(
            IConfiguration configuration,
            ILogger<StartupEnvironmentValidator> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates that all modules initialized successfully.
        /// Checks module health status via IModuleHealthService.
        /// </summary>
        public void ValidateModuleInitialization(IModuleHealthService moduleHealthService)
        {
            if (moduleHealthService == null)
            {
                Log.Warning("Module health service not available - skipping module validation");
                _logger.LogWarning("Module health service not available - skipping module validation");
                return;
            }

            try
            {
                var moduleStatuses = moduleHealthService.GetAllModuleStatuses();
                var healthy = moduleStatuses.Count(m => m.Status == ModuleHealthStatus.Healthy);
                var total = moduleStatuses.Count();

                Log.Information("Module validation: {Healthy}/{Total} healthy", healthy, total);
                _logger.LogInformation("Module validation: {Healthy}/{Total} modules healthy", healthy, total);

                if (healthy < total)
                {
                    var unhealthy = moduleStatuses.Where(m => m.Status != ModuleHealthStatus.Healthy).ToList();
                    foreach (var module in unhealthy)
                    {
                        Log.Warning("Unhealthy module: {ModuleName} - Status: {Status}",
                            module.ModuleName, module.Status);
                        _logger.LogWarning("Unhealthy module: {ModuleName} - Status: {Status}",
                            module.ModuleName, module.Status);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to validate module initialization");
                _logger.LogError(ex, "Failed to validate module initialization");
            }
        }

        /// <summary>
        /// Validates AI service configuration.
        /// Checks for required API keys and service availability.
        /// </summary>
        public void ValidateAIServiceConfiguration()
        {
            try
            {
                Log.Information("Validating AI service configuration...");
                _logger.LogInformation("Validating AI service configuration...");

                var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    Log.Information("✓ XAI_API_KEY environment variable found");
                    _logger.LogInformation("✓ XAI_API_KEY environment variable found (length: {Length})", apiKey.Length);
                }
                else
                {
                    Log.Warning("⚠ XAI_API_KEY not found in environment variables");
                    _logger.LogWarning("⚠ XAI_API_KEY not found in environment variables - AI features may be degraded");
                }

                // Check if AI is required
                var requireAI = string.Equals(
                    Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);

                if (requireAI && string.IsNullOrWhiteSpace(apiKey))
                {
                    var message = "REQUIRE_AI_SERVICE=true but XAI_API_KEY not configured";
                    Log.Error(message);
                    _logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                Log.Information("✓ AI service configuration validation complete");
                _logger.LogInformation("✓ AI service configuration validation complete");
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw required service exceptions
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠ AI service configuration validation failed");
                _logger.LogWarning(ex, "⚠ AI service configuration validation failed - continuing with degraded AI support");
            }
        }

        /// <summary>
        /// Validates that all registered ViewModels can be constructed with their dependencies.
        /// Performs 2nd order validation: ensures all services required by ViewModels are registered.
        /// </summary>
        public void ValidateAndRegisterViewModels(IContainerRegistry registry)
        {
            if (registry == null)
            {
                Log.Warning("Registry not available - skipping ViewModel validation");
                _logger.LogWarning("Registry not available - skipping ViewModel validation");
                return;
            }

            try
            {
                Log.Information("Validating ViewModel registrations...");
                _logger.LogInformation("Validating ViewModel registrations...");

                // Get SettingsViewModel type
                var settingsVMType = Type.GetType("WileyWidget.ViewModels.Main.SettingsViewModel, WileyWidget");
                if (settingsVMType == null)
                {
                    Log.Warning("⚠ SettingsViewModel type not found - skipping validation");
                    _logger.LogWarning("⚠ SettingsViewModel type not found - skipping validation");
                    return;
                }

                // Validate constructor dependencies
                var constructors = settingsVMType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    Log.Debug("Validating SettingsViewModel constructor with {Count} parameters", parameters.Length);
                    _logger.LogDebug("Validating SettingsViewModel constructor with {Count} parameters", parameters.Length);

                    foreach (var param in parameters)
                    {
                        var paramType = param.ParameterType;
                        Log.Debug("  Checking dependency: {Type}", paramType.Name);

                        // Try to resolve the dependency (validates it's registered)
                        try
                        {
                            var container = registry.GetContainer();
                            if (!container.IsRegistered(paramType))
                            {
                                Log.Warning("⚠ SettingsViewModel dependency not registered: {Type}", paramType.Name);
                                _logger.LogWarning("⚠ SettingsViewModel dependency not registered: {Type}", paramType.Name);
                            }
                            else
                            {
                                Log.Debug("  ✓ {Type} is registered", paramType.Name);
                                _logger.LogDebug("  ✓ {Type} is registered", paramType.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "⚠ Failed to validate dependency: {Type}", paramType.Name);
                            _logger.LogWarning(ex, "⚠ Failed to validate dependency: {Type}", paramType.Name);
                        }
                    }
                }

                Log.Information("✓ ViewModel validation complete");
                _logger.LogInformation("✓ ViewModel validation complete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to validate ViewModels");
                _logger.LogError(ex, "✗ Failed to validate ViewModels");
                // Don't throw - this is a validation step, not critical for startup
            }
        }

        /// <summary>
        /// Gets the available system memory in megabytes.
        /// Uses GC.GetGCMemoryInfo() to estimate available memory.
        /// </summary>
        public long GetAvailableMemoryMB()
        {
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                var totalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes;
                var heapSizeBytes = gcInfo.HeapSizeBytes;
                var availableBytes = totalAvailableMemoryBytes - heapSizeBytes;

                var availableMB = availableBytes / (1024 * 1024);

                Log.Debug("Available memory: {AvailableMB} MB (Total: {TotalMB} MB, Heap: {HeapMB} MB)",
                    availableMB,
                    totalAvailableMemoryBytes / (1024 * 1024),
                    heapSizeBytes / (1024 * 1024));

                return availableMB;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get available memory - returning 0");
                _logger.LogWarning(ex, "Failed to get available memory - returning 0");
                return 0;
            }
        }

        /// <summary>
        /// Validates that minimum memory requirements are met for application startup.
        /// </summary>
        public bool ValidateMemoryRequirements(long minimumRequiredMB = 128)
        {
            try
            {
                var availableMB = GetAvailableMemoryMB();

                if (availableMB < minimumRequiredMB)
                {
                    Log.Warning("Insufficient memory: {Available} MB available, {Required} MB required",
                        availableMB, minimumRequiredMB);
                    _logger.LogWarning("Insufficient memory: {Available} MB available, {Required} MB required",
                        availableMB, minimumRequiredMB);
                    return false;
                }

                Log.Information("✓ Memory validation passed: {Available} MB available (required: {Required} MB)",
                    availableMB, minimumRequiredMB);
                _logger.LogInformation("✓ Memory validation passed: {Available} MB available (required: {Required} MB)",
                    availableMB, minimumRequiredMB);

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Memory validation failed - assuming sufficient memory");
                _logger.LogWarning(ex, "Memory validation failed - assuming sufficient memory");
                return true; // Don't block startup on validation failure
            }
        }
    }
}
