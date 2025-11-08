using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Main;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for budget management and analytics functionality.
    /// Registers BudgetView, BudgetAnalysisView, and AnalyticsView with their respective regions.
    /// </summary>
    [Module(ModuleName = "BudgetModule")]
    [ModuleDependency("CoreModule")]
    public class BudgetModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing BudgetModule");

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // BudgetViewModel and AnalyticsViewModel will be resolved automatically during navigation
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register BudgetView with BudgetRegion
                regionManager.RegisterViewWithRegion("BudgetRegion", typeof(BudgetView));
                Log.Information("BudgetView registered with BudgetRegion");

                // Register AnalyticsView with AnalyticsRegion
                regionManager.RegisterViewWithRegion("AnalyticsRegion", typeof(AnalyticsView));
                Log.Information("AnalyticsView registered with AnalyticsRegion");

                Log.Information("BudgetModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in BudgetModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register BudgetViewModel
            containerRegistry.Register<BudgetViewModel>();

            // Register BudgetAnalysisViewModel
            containerRegistry.Register<BudgetAnalysisViewModel>();

            // Register AnalyticsViewModel
            containerRegistry.Register<AnalyticsViewModel>();

            // Budget repository is registered centrally in App.RegisterTypes; avoid duplicate registration here

            // Register views for navigation
            containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>();
            containerRegistry.RegisterForNavigation<BudgetAnalysisView, BudgetAnalysisViewModel>();
            containerRegistry.RegisterForNavigation<AnalyticsView, AnalyticsViewModel>();

            Log.Debug("Budget types registered");
        }
    }
}
