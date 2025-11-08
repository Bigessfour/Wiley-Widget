using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Main;

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

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // SettingsViewModel will be resolved automatically when navigating to SettingsView
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();
                regionManager.RegisterViewWithRegion("SettingsRegion", typeof(SettingsView));
                Log.Information("SettingsView registered with SettingsRegion");

                Log.Information("SettingsModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in SettingsModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }
    }
}
