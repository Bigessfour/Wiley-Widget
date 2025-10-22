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
                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register BudgetView with BudgetRegion
                regionManager.RegisterViewWithRegion("BudgetRegion", typeof(BudgetView));
                Log.Information("Successfully registered BudgetView with BudgetRegion");

                // Register BudgetAnalysisView with BudgetRegion (can coexist with BudgetView)
                regionManager.RegisterViewWithRegion("BudgetRegion", typeof(BudgetAnalysisView));
                Log.Information("Successfully registered BudgetAnalysisView with BudgetRegion");

                // Register AnalyticsView with AnalyticsRegion
                regionManager.RegisterViewWithRegion("AnalyticsRegion", typeof(AnalyticsView));
                Log.Information("Successfully registered AnalyticsView with AnalyticsRegion");

                Log.Information("BudgetModule initialization completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize BudgetModule - region registration failed");
                throw;
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

            // Register Budget repository
            containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();

            // Register views for navigation
            containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>();
            containerRegistry.RegisterForNavigation<BudgetAnalysisView, BudgetAnalysisViewModel>();
            containerRegistry.RegisterForNavigation<AnalyticsView, AnalyticsViewModel>();

            Log.Debug("Budget types registered");
        }
    }
}
