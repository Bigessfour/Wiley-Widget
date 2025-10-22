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
    /// Prism module responsible for tools functionality.
    /// Registers ToolsPanelView with the BottomPanelRegion.
    /// </summary>
    [Module(ModuleName = "ToolsModule")]
    public class ToolsModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing ToolsModule");

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Note: ToolsPanelView is registered by PanelModule with BottomPanelRegion
            // We may need to move this registration here in the future

            Log.Information("ToolsModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register ToolsViewModel
            containerRegistry.Register<ToolsViewModel>();

            Log.Debug("Tools types registered");
        }
    }
}
