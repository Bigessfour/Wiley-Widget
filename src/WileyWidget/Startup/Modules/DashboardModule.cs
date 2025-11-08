using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Main;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for dashboard functionality and view registration.
    /// Registers DashboardView with the MainRegion using RegisterViewWithRegion.
    /// Depends on CoreModule for foundational infrastructure services.
    /// </summary>
    [Module(ModuleName = "DashboardModule")]
    [ModuleDependency("CoreModule")]
    public class DashboardModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing DashboardModule");

            var moduleHealthService = containerProvider.Resolve<IModuleHealthService>();
            moduleHealthService.RegisterModule("DashboardModule");

            try
            {
                if (containerProvider == null)
                {
                    throw new ArgumentNullException(nameof(containerProvider));
                }

                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // DashboardViewModel will be resolved automatically when navigating to DashboardView
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();
                Log.Information("Successfully resolved IRegionManager from container");

                // Register DashboardView with MainRegion
                regionManager.RegisterViewWithRegion("MainRegion", typeof(DashboardView));
                Log.Information("DashboardView registered with MainRegion");

                Log.Information("DashboardModule initialization completed");
                moduleHealthService.MarkModuleInitialized("DashboardModule", true);
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in DashboardModule.OnInitialized");
                moduleHealthService.MarkModuleInitialized("DashboardModule", false, ex.Message);
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register DashboardViewModel as transient to avoid unintended shared state
            containerRegistry.Register<DashboardViewModel>();

            // Register DashboardView for navigation
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();

            Log.Debug("Dashboard types registered");
        }
    }
}
