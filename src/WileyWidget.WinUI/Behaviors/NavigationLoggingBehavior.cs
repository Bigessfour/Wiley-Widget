using System;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.Behaviors
{
    /// <summary>
    /// Region behavior that logs navigation events for debugging and monitoring
    /// </summary>
    public class NavigationLoggingBehavior : RegionBehavior
    {
        public const string BehaviorKey = "NavigationLogging";

        private readonly ILogger<NavigationLoggingBehavior> _logger;

        public NavigationLoggingBehavior(ILogger<NavigationLoggingBehavior> logger)
        {
            _logger = logger;
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigating += NavigationService_Navigating;
            Region.NavigationService.Navigated += NavigationService_Navigated;
            Region.NavigationService.NavigationFailed += NavigationService_NavigationFailed;

            _logger.LogDebug("NavigationLoggingBehavior attached to region '{RegionName}'", Region.Name);
        }

        private void NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            _logger.LogInformation("Navigating to '{Target}' in region '{RegionName}' from '{Source}'",
                e.Uri, Region.Name, e.NavigationContext.NavigationService?.Journal?.CurrentEntry?.Uri);
        }

        private void NavigationService_Navigated(object sender, RegionNavigationEventArgs e)
        {
            _logger.LogInformation("Successfully navigated to '{Target}' in region '{RegionName}'",
                e.Uri, Region.Name);
        }

        private void NavigationService_NavigationFailed(object sender, RegionNavigationFailedEventArgs e)
        {
            _logger.LogError(e.Error, "Navigation failed for '{Target}' in region '{RegionName}': {ErrorMessage}",
                e.Uri, Region.Name, e.Error?.Message);
        }
    }
}