using System;
using System.Linq;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
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

    /// <summary>
    /// Helper to explicitly register application modules into a Prism IModuleCatalog.
    /// Use this from App.ConfigureModuleCatalog(moduleCatalog) to avoid MEF/implicit scanning.
    /// </summary>
    public static class CustomModuleManager
    {
        /// <summary>
        /// Register modules in a strongly-typed, explicit manner. Prefer AddModule<T>() where possible.
        /// Mark heavy/optional modules OnDemand via ModuleInfo + InitializationMode.OnDemand.
        /// </summary>
        public static void RegisterModules(IModuleCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            try
            {
                // Core modules (load at startup for immediate availability)
                catalog.AddModule<CoreModule>();
                catalog.AddModule<DashboardModule>();
                catalog.AddModule<AIAssistModule>();
                catalog.AddModule<PanelModule>();
                catalog.AddModule<SettingsModule>();

                // Test module used to intentionally throw during Initialize for verifying global error handling.
                // It will only throw when the environment variable THROW_MODULE_INIT_EXCEPTION is set to '1'.
                catalog.AddModule<ThrowingModule>();

                // Reports module is medium-weight; register normally but could be switched to OnDemand if desired
                catalog.AddModule<ReportsModule>();

                // Feature modules - load on demand to improve startup performance
                var enterpriseInfo = new ModuleInfo
                {
                    ModuleName = nameof(EnterpriseModule),
                    ModuleType = typeof(EnterpriseModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.OnDemand
                };
                if (!catalog.Modules.Any(m => m.ModuleName == enterpriseInfo.ModuleName))
                {
                    catalog.AddModule(enterpriseInfo);
                }

                var budgetInfo = new ModuleInfo
                {
                    ModuleName = nameof(BudgetModule),
                    ModuleType = typeof(BudgetModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.OnDemand
                };
                if (!catalog.Modules.Any(m => m.ModuleName == budgetInfo.ModuleName))
                {
                    catalog.AddModule(budgetInfo);
                }

                var municipalAccountInfo = new ModuleInfo
                {
                    ModuleName = nameof(MunicipalAccountModule),
                    ModuleType = typeof(MunicipalAccountModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.OnDemand
                };
                if (!catalog.Modules.Any(m => m.ModuleName == municipalAccountInfo.ModuleName))
                {
                    catalog.AddModule(municipalAccountInfo);
                }

                var utilityCustomerInfo = new ModuleInfo
                {
                    ModuleName = nameof(UtilityCustomerModule),
                    ModuleType = typeof(UtilityCustomerModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.OnDemand
                };
                if (!catalog.Modules.Any(m => m.ModuleName == utilityCustomerInfo.ModuleName))
                {
                    catalog.AddModule(utilityCustomerInfo);
                }

                // QuickBooks integration is heavier and optional in some environments - register as OnDemand
                var qbInfo = new ModuleInfo
                {
                    ModuleName = nameof(QuickBooksModule),
                    ModuleType = typeof(QuickBooksModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.OnDemand
                };
                // Ensure we don't double-add if someone used AddModule elsewhere
                if (!catalog.Modules.Any(m => m.ModuleName == qbInfo.ModuleName))
                {
                    catalog.AddModule(qbInfo);
                }

                Log.Information("CustomModuleManager: Registered modules into module catalog (4 OnDemand modules for improved startup performance)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CustomModuleManager: Failed to register modules into catalog");
                throw;
            }
        }
    }
}
