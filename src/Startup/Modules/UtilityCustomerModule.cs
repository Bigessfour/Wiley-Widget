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
    /// Prism module responsible for utility customer management functionality.
    /// Registers UtilityCustomerView with the UtilityCustomerRegion.
    /// </summary>
    [Module(ModuleName = "UtilityCustomerModule")]
    public class UtilityCustomerModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing UtilityCustomerModule");

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register UtilityCustomerView with UtilityCustomerRegion
            regionManager.RegisterViewWithRegion("UtilityCustomerRegion", typeof(UtilityCustomerView));
            Log.Information("Successfully registered UtilityCustomerView with UtilityCustomerRegion");

            Log.Information("UtilityCustomerModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register UtilityCustomerViewModel
            containerRegistry.Register<UtilityCustomerViewModel>();

            // Register views for navigation
            containerRegistry.RegisterForNavigation<UtilityCustomerView, UtilityCustomerViewModel>();

            // Register dialog for editing/creating customers (Prism Dialog)
            containerRegistry.RegisterDialog<WileyWidget.Views.CustomerEditDialogView, WileyWidget.ViewModels.CustomerEditDialogViewModel>();

            Log.Debug("Utility customer types registered");
        }
    }
}