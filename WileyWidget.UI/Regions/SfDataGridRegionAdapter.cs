using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Prism;
using Prism.Navigation.Regions;
using Serilog;
using Syncfusion.UI.Xaml.Grid;

namespace WileyWidget.Regions
{
    /// <summary>
    /// Lightweight RegionAdapter for Syncfusion's SfDataGrid.
    ///
    /// Intent: Allow a region to target an SfDataGrid for data presentation by
    /// binding the region's first added "view" when it's a collection (IEnumerable)
    /// to the grid's ItemsSource. This avoids trying to host UIElement views inside the grid
    /// and plays nicely with virtualization.
    ///
    /// Usage pattern:
    /// - Register the adapter in App.ConfigureRegionAdapterMappings
    /// - RequestNavigate to a view that sets DataContext with an IEnumerable, or
    ///   add the IEnumerable directly to the region.
    /// </summary>
    public class SfDataGridRegionAdapter : RegionAdapterBase<SfDataGrid>
    {
        public SfDataGridRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
            : base(regionBehaviorFactory)
        {
        }

        protected override IRegion CreateRegion()
        {
            // Use AllActiveRegion since the grid is a data host, not a single view container
            return new AllActiveRegion();
        }

        protected override void Adapt(IRegion region, SfDataGrid regionTarget)
        {
            if (region is null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            if (regionTarget is null)
            {
                throw new ArgumentNullException(nameof(regionTarget));
            }

            // Virtualization hints for performance
            regionTarget.EnableDataVirtualization = true;

            region.Views.CollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var item in e.NewItems ?? Array.Empty<object>())
                    {
                        // If the item is a collection, bind it to ItemsSource
                        if (item is IEnumerable enumerable && item is not string)
                        {
                            regionTarget.ItemsSource = enumerable;
                            Log.Information("SfDataGridRegionAdapter bound ItemsSource from region '{RegionName}'", region.Name);
                        }
                        // If the item is a FrameworkElement, try to use its DataContext when it's a collection
                        else if (item is FrameworkElement fe && fe.DataContext is IEnumerable dcEnum && fe.DataContext is not string)
                        {
                            regionTarget.ItemsSource = dcEnum;
                            Log.Information("SfDataGridRegionAdapter bound ItemsSource from view DataContext in region '{RegionName}'", region.Name);
                        }
                        else
                        {
                            Log.Warning("SfDataGridRegionAdapter received unsupported item type: {Type}. Only IEnumerable is supported.", item?.GetType().FullName ?? "null");
                        }
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    // Clear the grid if the source was removed
                    if (region.Views.Count() == 0)
                    {
                        regionTarget.ItemsSource = null;
                    }
                }
            };
        }
    }
}
