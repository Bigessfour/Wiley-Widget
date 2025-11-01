using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Main;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Core Prism module responsible for shell-level infrastructure registrations.
    /// Implements the module pattern described in Prism's module initialization guidance.
    /// </summary>
    [Module(ModuleName = "CoreModule")]
    public class CoreModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Settings registrations are consolidated into SettingsModule
            Log.Debug("CoreModule types registered (no settings; consolidated in SettingsModule)");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            try
            {
                var moduleHealthService = containerProvider.Resolve<IModuleHealthService>();
                moduleHealthService.RegisterModule("CoreModule");

                // Safely resolve ViewModel to validate DI container registration
                var vm = containerProvider.Resolve<SettingsViewModel>();
                Log.Debug("Successfully resolved SettingsViewModel from container");

                var regionManager = containerProvider.Resolve<IRegionManager>();
                regionManager.RegisterViewWithRegion("SettingsRegion", typeof(SettingsView));

                moduleHealthService.MarkModuleInitialized("CoreModule", success: true);
                Log.Information("CoreModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in CoreModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }
    }
}
