using System;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Regions
{
    /// <summary>
    /// WPF Region adapter for Syncfusion SfDataGrid control
    /// </summary>
    public class SfDataGridRegionAdapter : RegionAdapterBase<Syncfusion.UI.Xaml.Grid.SfDataGrid>
    {
        public SfDataGridRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
            : base(regionBehaviorFactory)
        {
        }

        protected override void Adapt(IRegion region, Syncfusion.UI.Xaml.Grid.SfDataGrid regionTarget)
        {
            Log.Debug("Adapting SfDataGrid for region '{RegionName}'", region.Name);

            // SfDataGrid is typically used for data display, not as a region container
            // This adapter allows the grid to be part of a region while maintaining its data binding

            region.Views.CollectionChanged += (sender, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var view in e.NewItems)
                    {
                        if (view is System.Windows.UIElement uiElement)
                        {
                            // SfDataGrid itself is the view, so we don't add children
                            // Instead, we ensure it's properly configured for the region
                            Log.Debug("SfDataGrid view added to region '{RegionName}'", region.Name);
                        }
                    }
                }
            };

            region.ActiveViews.CollectionChanged += (sender, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var view in e.NewItems)
                    {
                        if (view is Syncfusion.UI.Xaml.Grid.SfDataGrid grid)
                        {
                            // Ensure the grid is visible and active
                            grid.Visibility = System.Windows.Visibility.Visible;
                            Log.Debug("Activated SfDataGrid in region '{RegionName}'", region.Name);
                        }
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    foreach (var view in e.OldItems)
                    {
                        if (view is Syncfusion.UI.Xaml.Grid.SfDataGrid grid)
                        {
                            // Optionally hide the grid when deactivated
                            grid.Visibility = System.Windows.Visibility.Collapsed;
                            Log.Debug("Deactivated SfDataGrid in region '{RegionName}'", region.Name);
                        }
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