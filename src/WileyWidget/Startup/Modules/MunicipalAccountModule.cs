using System;
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
    /// Prism module responsible for municipal account functionality.
    /// Registers MunicipalAccountView with MunicipalAccountRegion.
    /// </summary>
    [Module(ModuleName = "MunicipalAccountModule")]
    public class MunicipalAccountModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing MunicipalAccountModule");

            var moduleHealthService = containerProvider.Resolve<IModuleHealthService>();
            moduleHealthService.RegisterModule("MunicipalAccountModule");

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // MunicipalAccountViewModel will be resolved automatically during navigation
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();
                Log.Information("Successfully resolved IRegionManager from container");

                // Register MunicipalAccountView with MunicipalAccountRegion with error handling
                RegisterViewWithRegion(regionManager, "MunicipalAccountRegion", typeof(MunicipalAccountView));

                Log.Information("MunicipalAccountModule initialization completed successfully");
                moduleHealthService.MarkModuleInitialized("MunicipalAccountModule", true);
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in MunicipalAccountModule.OnInitialized");
                moduleHealthService.MarkModuleInitialized("MunicipalAccountModule", false, ex.Message);
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        /// <summary>
        /// Registers a view with a region with comprehensive error handling and recovery.
        /// </summary>
        /// <param name="regionManager">The region manager to use for registration</param>
        /// <param name="regionName">The name of the region to register with</param>
        /// <param name="viewType">The type of the view to register</param>
        private void RegisterViewWithRegion(IRegionManager regionManager, string regionName, Type viewType)
        {
            try
            {
                regionManager.RegisterViewWithRegion(regionName, viewType);
                Log.Information("Successfully registered {ViewType} with {RegionName}", viewType.Name, regionName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register {ViewType} with {RegionName}", viewType.Name, regionName);

                // Attempt recovery strategies
                try
                {
                    // Strategy 1: Check if region exists
                    if (!regionManager.Regions.ContainsRegionWithName(regionName))
                    {
                        Log.Warning("Region '{RegionName}' does not exist. View registration deferred.", regionName);
                        return;
                    }

                    // Strategy 2: Try alternative registration method
                    Log.Information("Attempting alternative registration method for {ViewType}", viewType.Name);
                    var region = regionManager.Regions[regionName];
                    region.Add(viewType);
                    Log.Information("Successfully registered {ViewType} using alternative method", viewType.Name);
                }
                catch (Exception recoveryEx)
                {
                    Log.Error(recoveryEx, "All recovery strategies failed for {ViewType} registration", viewType.Name);
                    // Final fallback: log and continue - don't crash the module
                }
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register MunicipalAccountViewModel
            containerRegistry.Register<MunicipalAccountViewModel>();

            // Register MunicipalAccountView for navigation
            containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();

            Log.Debug("Municipal account types registered");
        }
    }
}
