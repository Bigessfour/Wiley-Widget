using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Prism.Navigation.Regions;
using Syncfusion.Windows.Tools.Controls;

namespace WileyWidget.Regions;

/// <summary>
/// Region adapter for Syncfusion DockingManager to enable Prism region functionality
/// Allows views to be added/removed from DockingManager regions dynamically
/// </summary>
public class DockingManagerRegionAdapter : RegionAdapterBase<DockingManager>
{
    /// <summary>
    /// Initializes a new instance of the DockingManagerRegionAdapter
    /// </summary>
    /// <param name="regionBehaviorFactory">Factory for creating region behaviors</param>
    public DockingManagerRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
        : base(regionBehaviorFactory)
    {
    }

    /// <summary>
    /// Creates a region for the DockingManager
    /// </summary>
    /// <returns>A new SingleActiveRegion for the DockingManager</returns>
    protected override IRegion CreateRegion()
    {
        return new SingleActiveRegion();
    }

    /// <summary>
    /// Adapts the DockingManager to work with Prism regions
    /// </summary>
    /// <param name="region">The region to adapt</param>
    /// <param name="regionTarget">The DockingManager control</param>
    protected override void Adapt(IRegion region, DockingManager regionTarget)
    {
        region.Views.CollectionChanged += (sender, e) =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (FrameworkElement view in e.NewItems)
                    {
                        // Find the ContentControl in the DockingManager that matches this region's name
                        var contentControl = FindContentControlByRegionName(regionTarget, region.Name);
                        if (contentControl is System.Windows.Controls.ContentControl cc)
                        {
                            cc.Content = view;
                            // Make the newly added view the active one
                            if (!region.ActiveViews.Contains(view))
                            {
                                region.Activate(view);
                            }

                            // If the host is a DockingManager document, bring it to front
                            try
                            {
                                if (cc is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                                {
                                    regionTarget.ActivateWindow(fe.Name);
                                }
                            }
                            catch
                            {
                                // Best-effort activation; ignore failures
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (FrameworkElement view in e.OldItems)
                    {
                        // Find the ContentControl and clear its content
                        var contentControl = FindContentControlByRegionName(regionTarget, region.Name);
                        if (contentControl is System.Windows.Controls.ContentControl cc && cc.Content == view)
                        {
                            cc.Content = null;
                        }
                    }
                    break;
            }
        };
    }

    /// <summary>
    /// Attaches behaviors to the region
    /// </summary>
    /// <param name="region">The region</param>
    /// <param name="regionTarget">The DockingManager</param>
    protected override void AttachBehaviors(IRegion region, DockingManager regionTarget)
    {
        base.AttachBehaviors(region, regionTarget);

        // Add custom behaviors if needed
        // region.Behaviors.Add(AutoPopulateRegionBehavior.BehaviorKey, new AutoPopulateRegionBehavior());
    }

    /// <summary>
    /// Finds a ContentControl in the DockingManager by its region name
    /// </summary>
    /// <param name="dockingManager">The DockingManager to search</param>
    /// <param name="regionName">The region name to find</param>
    /// <returns>The ContentControl with the matching region name, or null if not found</returns>
    private System.Windows.Controls.ContentControl? FindContentControlByRegionName(DockingManager dockingManager, string regionName)
    {
        // Search through the DockingManager's children for a ContentControl with the matching region name
        foreach (var child in LogicalTreeHelper.GetChildren(dockingManager))
        {
            if (child is System.Windows.Controls.ContentControl contentControl &&
                contentControl.GetValue(Prism.Navigation.Regions.RegionManager.RegionNameProperty) as string == regionName)
            {
                return contentControl;
            }
        }

        return null;
    }
}
