using System;
using Prism.Ioc;
using Prism.Modularity;
// using Prism.Regions; // Removed to eliminate dependency
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for reports functionality.
    /// Registers ReportsView with the ReportsRegion.
    /// </summary>
    [Module(ModuleName = "ReportsModule")]
    public class ReportsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing ReportsModule");

            // var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register ReportsView with ReportsRegion
            // regionManager.RegisterViewWithRegion("ReportsRegion", typeof(ReportsView)); // Disabled region-based navigation
            // Log.Information("Successfully registered ReportsView with ReportsRegion");

            Log.Information("ReportsModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register ReportsViewModel
            containerRegistry.Register<ReportsViewModel>();

            // Register ReportsView for navigation
            containerRegistry.RegisterForNavigation<ReportsView, ReportsViewModel>();

            Log.Debug("Reports types registered");
        }
    }
}
