using Prism.Ioc;
using Prism.Modularity;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Core module for Uno platform - basic implementation.
    /// </summary>
    public class CoreModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // Basic initialization for Uno
            // Note: Uno uses Uno.Extensions.Navigation instead of Prism.Regions
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register types for Uno
            // Views and services will be registered through Uno navigation system
        }
    }
}