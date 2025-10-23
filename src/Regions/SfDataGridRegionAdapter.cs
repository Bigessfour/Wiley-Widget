using System;
using System.Collections.Specialized;
using System.Windows;
using Serilog;
using Syncfusion.UI.Xaml.Grid;

namespace WileyWidget.Regions
{
#if false
    /// <summary>
    /// Protective region adapter for Syncfusion's SfDataGrid.
    ///
    /// Notes:
    /// - SfDataGrid is not a typical region host for Prism (it's a data grid, not a ContentControl/ItemsControl compatible
    ///   with Prism region semantics). Attempting to use it as a region host often results in obscure InvalidCastException
    ///   originating in Prism internals. To avoid that, this adapter throws a clear descriptive exception when a view is
    ///   added, explaining correct workarounds.
    ///
    /// - If you truly intend to host multiple views inside a grid (for example, each row is a view), implement a custom
    ///   adapter that maps view instances to the grid's ItemsSource (data items) instead of trying to insert UIElement views
    ///   directly into the grid. For typical navigation scenarios, prefer a ContentControl/Region in XAML and keep the
    ///   SfDataGrid inside the navigated view.
    /// </summary>
#if false
    public class SfDataGridRegionAdapter : RegionAdapterBase<SfDataGrid>
    {
        public SfDataGridRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
            : base(regionBehaviorFactory)
        {
        }

        protected override IRegion CreateRegion()
        {
            // Use a single-active region to match common navigation patterns
            return new SingleActiveRegion();
        }

        protected override void Adapt(IRegion region, SfDataGrid regionTarget)
        {
            // Provide a defensive handler: adding a view to an SfDataGrid region is almost certainly a mistake.
            region.Views.CollectionChanged += (sender, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var newItem in e.NewItems ?? Array.Empty<object>())
                    {
                        var message = "Prism region attempted to add a View to an SfDataGrid. " +
                                      "SfDataGrid is not a supported region host for direct view hosting. " +
                                      "Use a ContentControl (with a region) to navigate between views, or implement a custom adapter " +
                                      "that maps views to the grid's data items. See README or XAML_EXCEPTION_HANDLING_GUIDE.md for guidance.";

                        // Log the actionable diagnostic to make the cause obvious instead of an InvalidCastException.
                        try
                        {
                            Log.Error(message + " ViewType={ViewType}, Region={RegionName}", newItem?.GetType().FullName, region.Name);
                        }
                        catch
                        {
                            // Best-effort logging only
                        }

                        // Throw a clear exception to replace obscure casts originating in Prism internals.
                        throw new InvalidOperationException(message);
                    }
                }
            };
        }
    }
#endif
#endif
}
