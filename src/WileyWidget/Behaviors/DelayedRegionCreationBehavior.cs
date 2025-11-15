using System;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// WPF Region behavior that delays region creation until first navigation request
    /// </summary>
    public class DelayedRegionCreationBehavior : RegionBehavior
    {
        public const string BehaviorKey = "DelayedRegionCreation";

        private bool _regionCreated;

        public DelayedRegionCreationBehavior()
        {
        }

        protected override void OnAttach()
        {
            Log.Debug("DelayedRegionCreationBehavior attached to region '{RegionName}'", Region.Name);

            // Initially mark region as not created
            _regionCreated = false;

            // Hook into navigation service to create region on first navigation
            Region.NavigationService.Navigating += Region_NavigationService_Navigating;
        }

        private void Region_NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            if (!_regionCreated)
            {
                // Create the region on first navigation
                _regionCreated = true;
                Log.Debug("Region '{RegionName}' created on first navigation to '{Uri}'", Region.Name, e.Uri);

                // Unsubscribe from the event since region is now created
                Region.NavigationService.Navigating -= Region_NavigationService_Navigating;
            }
        }
    }
}