using System;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;
using WileyWidget.Views;
using WileyWidget.Views.Panels;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for AI assistance functionality.
    /// Registers AIAssistView with the AIAssistRegion.
    /// </summary>
    [Module(ModuleName = "AIAssistModule")]
    public class AIAssistModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register AIAssistViewModel
            containerRegistry.Register<AIAssistViewModel>();

            // Register AIResponseViewModel
            containerRegistry.Register<AIResponseViewModel>();

            // Register views for navigation (panel view driven by AIAssistViewModel)
            containerRegistry.RegisterForNavigation<AIAssistPanelView, AIAssistViewModel>();

            Log.Debug("AI assist types registered");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Log.Information("Initializing AIAssistModule");

            try
            {
                // REMOVED: Eager ViewModel resolution causes DryIoc timeout due to heavy dependencies
                // AIAssistViewModel will be resolved automatically when navigating to AIAssistPanelView
                // Per Prism best practices, ViewModels should only be resolved during navigation

                var regionManager = containerProvider.Resolve<IRegionManager>();

                // Register AIAssistPanelView with AIAssistRegion (SfAIAssistView-based UI)
                regionManager.RegisterViewWithRegion("AIAssistRegion", typeof(AIAssistPanelView));
                Log.Information("AIAssistPanelView registered with AIAssistRegion");

                Log.Information("AIAssistModule initialization completed");
            }
            catch (Exception ex)
            {
                // Log & fallback (per Prism samples) - handles ContainerResolutionException and other DI failures
                Log.Error(ex, "DI container resolution or region registration failed in AIAssistModule.OnInitialized");
                // Don't rethrow - allow application to continue with degraded functionality
            }
        }
    }
}
