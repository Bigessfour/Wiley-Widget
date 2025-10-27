using System;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using WileyWidget.ViewModels;
using WileyWidget.Views;

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

            var regionManager = containerProvider.Resolve<IRegionManager>();

            // Register AIAssistPanelView with AIAssistRegion (SfAIAssistView-based UI)
            regionManager.RegisterViewWithRegion("AIAssistRegion", typeof(AIAssistPanelView));
            Log.Information("AIAssistPanelView registered with AIAssistRegion");

            Log.Information("AIAssistModule initialization completed");
        }
    }
}
