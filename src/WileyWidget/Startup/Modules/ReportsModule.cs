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
    /// Prism module responsible for reports functionality.
    /// Registers ReportsView with the ReportsRegion.
    /// </summary>
    [Module(ModuleName = "ReportsModule")]
    public class ReportsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing ReportsModule");

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // ReportsViewModel will be resolved automatically when navigating to ReportsView
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register ReportsView with ReportsRegion
                regionManager.RegisterViewWithRegion("ReportsRegion", typeof(ReportsView));
                Log.Information("Successfully registered ReportsView with ReportsRegion");

                Log.Information("ReportsModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in ReportsModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
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
