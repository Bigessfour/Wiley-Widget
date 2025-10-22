using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;

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

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register panel views with their regions
            regionManager.RegisterViewWithRegion("LeftPanelRegion", typeof(DashboardPanelView));
            regionManager.RegisterViewWithRegion("RightPanelRegion", typeof(SettingsPanelView));
            regionManager.RegisterViewWithRegion("BottomPanelRegion", typeof(ToolsPanelView));

            Log.Information("Successfully registered panel views");
            Log.Information("PanelModule initialization completed");
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
