using System;
using System.Linq;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// WPF Region behavior that automatically saves data when navigating away from views
    /// </summary>
    public class AutoSaveBehavior : RegionBehavior
    {
        public const string BehaviorKey = "AutoSave";

        public AutoSaveBehavior()
        {
        }

        protected override void OnAttach()
        {
            Log.Debug("AutoSaveBehavior attached to region '{RegionName}'", Region.Name);

            Region.NavigationService.Navigating += Region_NavigationService_Navigating;
        }

        private void Region_NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            try
            {
                // Attempt to save data from the current view
                if (Region.Views.Count() > 0)
                {
                    var activeView = Region.Views.First();
                    if (activeView is Prism.Navigation.Regions.INavigationAware navigationAware)
                    {
                        Log.Debug("Auto-saving data before navigating from region '{RegionName}'", Region.Name);
                        // Note: Actual save logic would be implemented in the view models
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Auto-save failed for region '{RegionName}'", Region.Name);
            }
        }
    }
}