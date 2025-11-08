using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Dialogs;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Dialogs;
using WileyWidget.Views.Main;

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

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // EnterpriseViewModel will be resolved automatically when navigating to EnterpriseView
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register EnterpriseView with EnterpriseRegion
                regionManager.RegisterViewWithRegion("EnterpriseRegion", typeof(EnterpriseView));
                Log.Information("Successfully registered EnterpriseView with EnterpriseRegion");

                Log.Information("EnterpriseModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in EnterpriseModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register EnterpriseViewModel
            containerRegistry.Register<EnterpriseViewModel>();

            // Register EnterpriseDialogViewModel
            containerRegistry.Register<EnterpriseDialogViewModel>();

            // Note: IEnterpriseRepository is registered centrally in App.xaml.cs after Bootstrapper
            // ensures IDbContextFactory is available. Modules can't register repositories that depend
            // on database infrastructure since module RegisterTypes may run before/during App.RegisterTypes.

            // Register views for navigation
            containerRegistry.RegisterForNavigation<EnterpriseView, EnterpriseViewModel>();
            containerRegistry.RegisterForNavigation<EnterpriseDialogView, EnterpriseDialogViewModel>();

            Log.Debug("Enterprise types registered");
        }
    }
}
