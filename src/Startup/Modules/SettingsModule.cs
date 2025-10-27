using System;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Consolidated Settings module responsible for settings-related views and tools consolidation.
    /// This merges previous ToolsModule registrations into a single module to simplify the catalog.
    /// </summary>
    [Module(ModuleName = "SettingsModule")]
    public class SettingsModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Settings
            containerRegistry.Register<SettingsViewModel>();
            containerRegistry.Register<SettingsView>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();

            // Tools consolidated here (ToolsViewModel)
            containerRegistry.Register<ToolsViewModel>();

            Log.Debug("SettingsModule types registered (Settings + Tools consolidated)");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing SettingsModule (consolidated settings and tools)");

            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RegisterViewWithRegion("SettingsRegion", typeof(SettingsView));
            Log.Information("SettingsView registered with SettingsRegion");

            Log.Information("SettingsModule initialization completed");
        }
    }
}
