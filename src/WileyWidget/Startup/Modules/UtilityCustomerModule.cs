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
    /// Prism module responsible for utility customer management functionality.
    /// Registers UtilityCustomerView with the UtilityCustomerRegion.
    /// </summary>
    [Module(ModuleName = "UtilityCustomerModule")]
    public class UtilityCustomerModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing UtilityCustomerModule");

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // UtilityCustomerViewModel will be resolved automatically during navigation
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register UtilityCustomerView with UtilityCustomerRegion
                regionManager.RegisterViewWithRegion("UtilityCustomerRegion", typeof(UtilityCustomerView));
                Log.Information("Successfully registered UtilityCustomerView with UtilityCustomerRegion");

                Log.Information("UtilityCustomerModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in UtilityCustomerModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register UtilityCustomerViewModel
            containerRegistry.Register<UtilityCustomerViewModel>();

            // Register views for navigation
            containerRegistry.RegisterForNavigation<UtilityCustomerView, UtilityCustomerViewModel>();

            // Note: CustomerEditDialogView registration moved to DialogsModule
            // to avoid assembly reference issues

            Log.Debug("Utility customer types registered");
        }
    }
}
