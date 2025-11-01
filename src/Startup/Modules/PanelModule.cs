using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using WileyWidget.Views.Panels;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module for panels (dashboard panel, settings panel, tools panel).
    /// Registers panel views with their respective regions.
    /// Depends on core modules for region availability.
    /// </summary>
    [Module(ModuleName = "PanelModule")]
    [ModuleDependency("DashboardModule")]
    [ModuleDependency("EnterpriseModule")]
    [ModuleDependency("BudgetModule")]
    [ModuleDependency("MunicipalAccountModule")]
    public class PanelModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing PanelModule");

            try
            {
                // Panel views typically don't have ViewModels to resolve early,
                // but we validate region manager availability
                var regionManager = containerProvider.Resolve<IRegionManager>();
                Log.Debug("Successfully resolved IRegionManager from container");

                // Register panel views with their regions
                regionManager.RegisterViewWithRegion("LeftPanelRegion", typeof(DashboardPanelView));
                regionManager.RegisterViewWithRegion("RightPanelRegion", typeof(SettingsPanelView));
                regionManager.RegisterViewWithRegion("BottomPanelRegion", typeof(ToolsPanelView));

                Log.Information("Successfully registered panel views");
                Log.Information("PanelModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in PanelModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register for navigation
            containerRegistry.RegisterForNavigation<DashboardPanelView>();
            containerRegistry.RegisterForNavigation<SettingsPanelView>();
            containerRegistry.RegisterForNavigation<ToolsPanelView>();

            Log.Debug("Panel types registered");
        }
    }
}
