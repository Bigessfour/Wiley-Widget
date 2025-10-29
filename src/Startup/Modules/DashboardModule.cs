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
using WileyWidget.Views;

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
                Log.Error(ex, "Critical error during DashboardModule initialization");
                moduleHealthService.MarkModuleInitialized("DashboardModule", false, ex.Message);
                throw;
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
