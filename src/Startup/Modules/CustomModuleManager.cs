using System;
using System.Linq;
using Serilog;
using Prism.Ioc;
using WileyWidget.Services;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Diagnostic helper to log module initialization exceptions and dependency resolution context.
    /// This is not a ModuleManager override (Prism's InitializeModule may not be virtual across versions).
    /// Use this helper from tests or from a custom initialization wrapper if you choose to add one later.
    /// </summary>
    public static class ModuleInitializationDiagnostics
    {
        public static void LogModuleInitializationException(Exception ex, IContainerProvider? provider = null, string? moduleName = null)
        {
            try
            {
                var root = ex.GetRootException();
                Log.Error(root, "Module initialization failed for {Module}: {Message}", moduleName ?? "(unknown)", root.Message);
                Log.Error("Full exception chain: {Detailed}", ex.GetDetailedMessage());

                if (provider != null)
                {
                    try
                    {
                        var health = provider.Resolve<IModuleHealthService>();
                        health.MarkModuleInitialized(moduleName ?? "(unknown)", false, root.Message + " (see logs)");
                    }
                    catch (Exception iex)
                    {
                        Log.Warning(iex, "Failed to mark module health in diagnostics helper");
                    }

                    // DryIoc container diagnostics removed - Registrations property not available in current version
                    // If needed, use container.Resolve<IEnumerable<Type>>() or other diagnostic methods
                }
            }
            catch (Exception logEx)
            {
                Log.Warning(logEx, "ModuleInitializationDiagnostics failed to log exception details");
            }
        }
    }
}
