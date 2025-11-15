using Prism.Navigation.Regions;
using Serilog;
using System.Linq;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// WPF Region behavior that automatically activates views when they are added to the region
    /// </summary>
    public class AutoActivateBehavior : RegionBehavior
    {
        public const string BehaviorKey = "AutoActivate";

        public AutoActivateBehavior()
        {
        }

        protected override void OnAttach()
        {
            Log.Debug("AutoActivateBehavior attached to region '{RegionName}'", Region.Name);

            Region.Views.CollectionChanged += Views_CollectionChanged;
        }

        private void Views_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var newView in e.NewItems)
                {
                    if (newView != null && !Region.Views.Any())
                    {
                        // Auto-activate the first view added if no views are currently active
                        Region.Activate(newView);
                        Log.Debug("Auto-activated view in region '{RegionName}'", Region.Name);
                        break;
                    }
                }
            }
        }
    }
}