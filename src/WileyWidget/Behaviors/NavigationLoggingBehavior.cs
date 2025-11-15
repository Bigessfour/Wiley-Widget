using System;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// WPF Region behavior that logs navigation events for debugging and monitoring
    /// </summary>
    public class NavigationLoggingBehavior : RegionBehavior
    {
        public const string BehaviorKey = "NavigationLogging";

        public NavigationLoggingBehavior()
        {
            // WPF version doesn't use dependency injection for logger
        }

        protected override void OnAttach()
        {
            Log.Debug("NavigationLoggingBehavior attached to region '{RegionName}'", Region.Name);

            Region.NavigationService.Navigated += Region_NavigationService_Navigated;
            Region.NavigationService.Navigating += Region_NavigationService_Navigating;
            Region.NavigationService.NavigationFailed += Region_NavigationService_NavigationFailed;
        }

        private void Region_NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            Log.Debug("Region '{RegionName}' navigating to '{TargetUri}'", Region.Name, e.Uri);
        }

        private void Region_NavigationService_Navigated(object sender, RegionNavigationEventArgs e)
        {
            Log.Debug("Region '{RegionName}' navigated to '{TargetUri}' successfully", Region.Name, e.Uri);
        }

        private void Region_NavigationService_NavigationFailed(object sender, RegionNavigationFailedEventArgs e)
        {
            Log.Error(e.Error, "Region '{RegionName}' navigation to '{TargetUri}' failed", Region.Name, e.Uri);
        }
    }
}