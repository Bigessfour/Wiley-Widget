using System;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Views;

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
            var moduleHealthService = containerProvider.Resolve<IModuleHealthService>();
            moduleHealthService.RegisterModule("CoreModule");

            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RegisterViewWithRegion("SettingsRegion", typeof(SettingsView));

            moduleHealthService.MarkModuleInitialized("CoreModule", success: true);
            Log.Information("CoreModule initialization completed");
        }
    }
}
