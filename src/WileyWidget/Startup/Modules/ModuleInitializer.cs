using System;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
// using Unity.Resolution; // Unity-specific diagnostics disabled; avoid hard dependency

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Base class for Prism modules that provides robust error handling and diagnostics
    /// for module initialization. Prevents ModuleInitializeException from crashing the application.
    /// </summary>
    public abstract class ModuleInitializer : IModule
    {
        /// <summary>
        /// Default constructor for ModuleInitializer.
        /// </summary>
        protected ModuleInitializer()
        {
        }
        /// <summary>
        /// Registers types with the container. Default implementation does nothing.
        /// Derived classes can override to register their types.
        /// </summary>
        /// <param name="containerRegistry">The container registry</param>
        public virtual void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Default implementation - no types to register
        }

        /// <summary>
        /// Called when the module is initialized. This method wraps the actual initialization
        /// in comprehensive error handling to prevent application crashes.
        /// </summary>
        /// <param name="containerProvider">The container provider for service resolution</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            if (containerProvider is null)
            {
                throw new ArgumentNullException(nameof(containerProvider));
            }

            var moduleName = GetModuleName();
            Log.Information("Initializing module: {ModuleName}", moduleName);

            try
            {
                // Call the derived class's initialization logic
                InitializeModule(containerProvider);
                Log.Information("Module {ModuleName} initialized successfully", moduleName);
            }
            catch (Exception ex)
            {
                // Get the root exception for better diagnostics
                var rootException = ex.GetRootException();
                Log.Error(rootException, "Critical error during {ModuleName} initialization: {Message}",
                    moduleName, rootException.Message);

                // Log detailed exception chain
                Log.Error("Full exception chain: {DetailedMessage}", ex.GetDetailedMessage());

                // Log dependency resolution context
                LogDependencyResolutionContext(containerProvider, ex);

                // Don't rethrow - allow application to continue with other modules
                // The module will be marked as failed but won't crash the entire application
                Log.Warning("Module {ModuleName} failed to initialize but application will continue", moduleName);
            }
        }

        /// <summary>
        /// Abstract method that derived classes must implement with their initialization logic.
        /// </summary>
        /// <param name="containerProvider">The container provider for service resolution</param>
        protected abstract void InitializeModule(IContainerProvider containerProvider);

        /// <summary>
        /// Gets the module name for logging purposes. Defaults to the class name.
        /// </summary>
        /// <returns>The module name</returns>
        protected virtual string GetModuleName()
        {
            return GetType().Name;
        }

        /// <summary>
        /// Logs context about dependency resolution failures for better diagnostics.
        /// </summary>
        /// <param name="containerProvider">The container provider</param>
        /// <param name="exception">The exception that occurred</param>
        private void LogDependencyResolutionContext(IContainerProvider containerProvider, Exception exception)
        {
            try
            {
                // Check if this is a dependency resolution issue
                // Avoid hard Unity dependency: detect by type name to gather context when available
                if (exception.GetType().Name == "ResolutionFailedException")
                {
                    // Try to get type information from the exception
                    string typeName = "Unknown";
                    try
                    {
                        // ResolutionFailedException should have Type property, but check if it exists
                        var typeProperty = exception.GetType().GetProperty("Type");
                        if (typeProperty != null)
                        {
                            var resolvedType = typeProperty.GetValue(exception) as Type;
                            typeName = resolvedType?.Name ?? "Unknown";
                        }
                    }
                    catch
                    {
                        // Fallback if Type property is not available
                        typeName = "Unknown";
                    }

                    Log.Error("Dependency resolution failed for type: {TypeName}", typeName);
                    // Container registration count not available without container-specific API; skip to avoid hard dependency
                }

                // Check for common service resolution issues
                var commonServices = new[]
                {
                    typeof(WileyWidget.Services.IModuleHealthService),
                    typeof(WileyWidget.Services.ISecretVaultService),
                    typeof(WileyWidget.Services.IQuickBooksService),
                    typeof(Microsoft.Extensions.Logging.ILoggerFactory),
                    typeof(Microsoft.Extensions.Configuration.IConfiguration)
                };

                foreach (var serviceType in commonServices)
                {
                    try
                    {
                        containerProvider.Resolve(serviceType);
                        Log.Debug("Service {ServiceType} is available", serviceType.Name);
                    }
                    catch
                    {
                        Log.Warning("Service {ServiceType} is NOT registered or failed to resolve", serviceType.Name);
                    }
                }
            }
            catch (Exception logEx)
            {
                Log.Error(logEx, "Failed to log dependency resolution context");
            }
        }
    }
}
