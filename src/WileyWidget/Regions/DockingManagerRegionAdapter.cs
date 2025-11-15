using System;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Regions
{
    /// <summary>
    /// WPF Region adapter for Syncfusion DockingManager control
    /// </summary>
    public class DockingManagerRegionAdapter : RegionAdapterBase<Syncfusion.Windows.Tools.Controls.DockingManager>
    {
        public DockingManagerRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
            : base(regionBehaviorFactory)
        {
        }

        protected override void Adapt(IRegion region, Syncfusion.Windows.Tools.Controls.DockingManager regionTarget)
        {
            Log.Debug("Adapting DockingManager for region '{RegionName}'", region.Name);

            // Configure the DockingManager for region usage
            regionTarget.UseDocumentContainer = true;

            // Handle view activation/deactivation
            region.Views.CollectionChanged += (sender, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var view in e.NewItems)
                    {
                        if (view is System.Windows.UIElement uiElement)
                        {
                            // Add view to docking manager
                            regionTarget.Children.Add(uiElement);
                            Log.Debug("Added view to DockingManager in region '{RegionName}'", region.Name);
                        }
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    foreach (var view in e.OldItems)
                    {
                        if (view is System.Windows.UIElement uiElement && regionTarget.Children.Contains(uiElement))
                        {
                            regionTarget.Children.Remove(uiElement);
                            Log.Debug("Removed view from DockingManager in region '{RegionName}'", region.Name);
                        }
                    }
                }
            };

            region.ActiveViews.CollectionChanged += (sender, e) =>
            {
                // Handle active view changes
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var view in e.NewItems)
                    {
                        Log.Debug("Activated view in DockingManager region '{RegionName}'", region.Name);
                    }
                }
            };
        }

        protected override IRegion CreateRegion()
        {
            return new SingleActiveRegion();
        }
    }
}