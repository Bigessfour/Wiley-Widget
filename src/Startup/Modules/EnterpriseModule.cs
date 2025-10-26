using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for enterprise management functionality.
    /// Registers EnterpriseView, EnterprisePanelView, and EnterpriseDialogView with their respective regions.
    /// </summary>
    [Module(ModuleName = "EnterpriseModule")]
    public class EnterpriseModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing EnterpriseModule");

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register EnterpriseView with EnterpriseRegion
            regionManager.RegisterViewWithRegion("EnterpriseRegion", typeof(EnterpriseView));
            Log.Information("Successfully registered EnterpriseView with EnterpriseRegion");

            Log.Information("EnterpriseModule initialization completed");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register EnterpriseViewModel
            containerRegistry.Register<EnterpriseViewModel>();

            // Register EnterpriseDialogViewModel
            containerRegistry.Register<EnterpriseDialogViewModel>();

            // Register Enterprise repository
            containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();

            // Register views for navigation
            containerRegistry.RegisterForNavigation<EnterpriseView, EnterpriseViewModel>();
            containerRegistry.RegisterForNavigation<EnterpriseDialogView, EnterpriseDialogViewModel>();

            Log.Debug("Enterprise types registered");
        }
    }
}
