using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
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
    public class CoreModule : ModuleInitializer
    {
        public override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Ensure SettingsView can be resolved for navigation scenarios
            containerRegistry.Register<SettingsViewModel>();
            containerRegistry.Register<SettingsView>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();

            Log.Debug("CoreModule types registered");
        }

        protected override void InitializeModule(IContainerProvider containerProvider)
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
