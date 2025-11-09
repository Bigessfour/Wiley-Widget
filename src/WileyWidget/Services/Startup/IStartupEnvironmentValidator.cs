// IStartupEnvironmentValidator.cs - Interface for startup environment validation service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.2)
// This service is responsible for validating the startup environment including:
// - Module initialization validation
// - AI service configuration validation
// - ViewModel dependency validation
// - Memory and resource availability checks

using System;
using Prism.Ioc;
using WileyWidget.Services;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for validating the application's startup environment.
    /// Performs validation of modules, services, dependencies, and system resources.
    /// </summary>
    public interface IStartupEnvironmentValidator
    {
        /// <summary>
        /// Validates that all modules initialized successfully.
        /// Checks module health status via IModuleHealthService.
        /// </summary>
        /// <param name="moduleHealthService">Module health tracking service</param>
        void ValidateModuleInitialization(IModuleHealthService moduleHealthService);

        /// <summary>
        /// Validates AI service configuration.
        /// Checks for required API keys and service availability.
        /// </summary>
        void ValidateAIServiceConfiguration();

        /// <summary>
        /// Validates that all registered ViewModels can be constructed with their dependencies.
        /// Performs 2nd order validation: ensures all services required by ViewModels are registered.
        /// </summary>
        /// <param name="registry">DI container registry</param>
        void ValidateAndRegisterViewModels(IContainerRegistry registry);

        /// <summary>
        /// Gets the available system memory in megabytes.
        /// Used to determine if sufficient resources are available for theme application and module loading.
        /// </summary>
        /// <returns>Available memory in MB, or 0 if cannot be determined</returns>
        long GetAvailableMemoryMB();

        /// <summary>
        /// Validates that minimum memory requirements are met for application startup.
        /// </summary>
        /// <param name="minimumRequiredMB">Minimum required memory in MB (default: 128MB)</param>
        /// <returns>True if sufficient memory available</returns>
        bool ValidateMemoryRequirements(long minimumRequiredMB = 128);
    }
}
