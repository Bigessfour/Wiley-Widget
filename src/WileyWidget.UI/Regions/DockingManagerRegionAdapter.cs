using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Prism.Navigation.Regions;
using Syncfusion.Windows.Tools.Controls;
using Serilog;

namespace WileyWidget.Regions;

// Enabled: Prism region adapter for Syncfusion DockingManager
/// <summary>
/// Region adapter for Syncfusion DockingManager to enable Prism region functionality.
/// This adapter enables Prism regions to work seamlessly with DockingManager child windows.
/// Each region corresponds to a named ContentControl child of the DockingManager.
/// </summary>
/// <remarks>
/// Per Syncfusion docs: DockingManager children should have Name properties set for state persistence.
/// Per Prism docs: Region adapters handle view injection into custom container controls.
/// </remarks>
public class DockingManagerRegionAdapter : RegionAdapterBase<DockingManager>
{
    /// <summary>
    /// Initializes a new instance of the DockingManagerRegionAdapter
    /// </summary>
    /// <param name="regionBehaviorFactory">Factory for creating region behaviors</param>
    public DockingManagerRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
        : base(regionBehaviorFactory)
    {
        Log.Debug("DockingManagerRegionAdapter: Constructor called");
    }

    /// <summary>
    /// Creates a region for the DockingManager.
    /// Uses SingleActiveRegion to ensure only one view is active at a time in each region.
    /// </summary>
    /// <returns>A new SingleActiveRegion for the DockingManager</returns>
    protected override IRegion CreateRegion()
    {
        Log.Debug("DockingManagerRegionAdapter: CreateRegion called - returning SingleActiveRegion");
        return new SingleActiveRegion();
    }

    /// <summary>
    /// Adapts the DockingManager to work with Prism regions.
    /// This method does NOT add children to DockingManager.Children directly.
    /// Instead, it finds existing ContentControl children (declared in XAML with RegionName attached property)
    /// and injects views into their Content property.
    /// </summary>
    /// <param name="region">The region to adapt</param>
    /// <param name="regionTarget">The DockingManager control</param>
    /// <remarks>
    /// Per project guidelines: DockingManagerRegionAdapter must find the correct host slot by
    /// comparing the Prism attached RegionName property on ContentControls.
    /// </remarks>
    protected override void Adapt(IRegion region, DockingManager regionTarget)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(regionTarget);

        Log.Information("DockingManagerRegionAdapter: Adapting region '{RegionName}' to DockingManager", region.Name);

        // Find the ContentControl child that has the matching RegionName
        var targetContentControl = FindContentControlByRegionName(regionTarget, region.Name);

        if (targetContentControl == null)
        {
            Log.Warning("DockingManagerRegionAdapter: No ContentControl found with RegionName='{RegionName}' in DockingManager children", region.Name);
            return;
        }

        Log.Debug("DockingManagerRegionAdapter: Found target ContentControl '{ControlName}' for region '{RegionName}'",
            targetContentControl.Name ?? "(unnamed)", region.Name);

        // Handle view collection changes for this region
        region.Views.CollectionChanged += (sender, e) =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (FrameworkElement view in e.NewItems)
                    {
                        Log.Debug("DockingManagerRegionAdapter: Adding view {ViewType} to ContentControl '{ControlName}' in region '{RegionName}'",
                            view.GetType().Name, targetContentControl.Name ?? "(unnamed)", region.Name);

                        // Inject the view into the ContentControl's Content
                        targetContentControl.Content = view;

                        // Activate the view in the region
                        if (!region.ActiveViews.Contains(view))
                        {
                            region.Activate(view);
                            Log.Debug("DockingManagerRegionAdapter: Activated view {ViewType} in region '{RegionName}'",
                                view.GetType().Name, region.Name);
                        }

                        // Make the ContentControl visible if it was collapsed
                        if (targetContentControl.Visibility == Visibility.Collapsed)
                        {
                            targetContentControl.Visibility = Visibility.Visible;
                            Log.Debug("DockingManagerRegionAdapter: Set ContentControl '{ControlName}' visibility to Visible",
                                targetContentControl.Name ?? "(unnamed)");
                        }

                        // Activate the window in DockingManager to bring it to front
                        try
                        {
                            if (!string.IsNullOrEmpty(targetContentControl.Name))
                            {
                                regionTarget.ActivateWindow(targetContentControl.Name);
                                Log.Debug("DockingManagerRegionAdapter: Activated DockingManager window '{WindowName}'",
                                    targetContentControl.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "DockingManagerRegionAdapter: Failed to activate DockingManager window '{WindowName}'",
                                targetContentControl.Name ?? "(unnamed)");
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (FrameworkElement view in e.OldItems)
                    {
                        Log.Debug("DockingManagerRegionAdapter: Removing view {ViewType} from region '{RegionName}'",
                            view.GetType().Name, region.Name);

                        // Clear the content if it matches the removed view
                        if (targetContentControl.Content == view)
                        {
                            targetContentControl.Content = null;
                            Log.Debug("DockingManagerRegionAdapter: Cleared ContentControl '{ControlName}' content",
                                targetContentControl.Name ?? "(unnamed)");
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    Log.Debug("DockingManagerRegionAdapter: Region '{RegionName}' views reset, clearing ContentControl content", region.Name);
                    targetContentControl.Content = null;
                    break;
            }
        };

        Log.Information("DockingManagerRegionAdapter: Successfully adapted region '{RegionName}' to DockingManager", region.Name);
    }

    /// <summary>
    /// Attaches behaviors to the region
    /// </summary>
    /// <param name="region">The region</param>
    /// <param name="regionTarget">The DockingManager</param>
    protected override void AttachBehaviors(IRegion region, DockingManager regionTarget)
    {
        ArgumentNullException.ThrowIfNull(region);
        base.AttachBehaviors(region, regionTarget);
        Log.Debug("DockingManagerRegionAdapter: Attached default behaviors to region '{RegionName}'", region.Name);
    }

    /// <summary>
    /// Finds a ContentControl in the DockingManager by its region name.
    /// Searches through DockingManager.Children looking for a ContentControl with the
    /// matching RegionManager.RegionName attached property.
    /// </summary>
    /// <param name="dockingManager">The DockingManager to search</param>
    /// <param name="regionName">The region name to find</param>
    /// <returns>The ContentControl with the matching region name, or null if not found</returns>
    private System.Windows.Controls.ContentControl? FindContentControlByRegionName(DockingManager dockingManager, string regionName)
    {
        Log.Debug("DockingManagerRegionAdapter: Searching for ContentControl with RegionName='{RegionName}'", regionName);

        // Per Syncfusion docs: DockingManager.Children contains all docked windows
        // Per project guidelines: Compare RegionName attached property to find the correct host
        foreach (UIElement child in dockingManager.Children)
        {
            if (child is System.Windows.Controls.ContentControl contentControl)
            {
                // Get the RegionName attached property from the ContentControl
                var attachedRegionName = contentControl.GetValue(RegionManager.RegionNameProperty) as string;
                
                Log.Debug("DockingManagerRegionAdapter: Checking ContentControl '{ControlName}' with RegionName='{AttachedRegionName}'",
                    contentControl.Name ?? "(unnamed)", attachedRegionName ?? "(none)");

                if (attachedRegionName == regionName)
                {
                    Log.Debug("DockingManagerRegionAdapter: Found matching ContentControl '{ControlName}' for region '{RegionName}'",
                        contentControl.Name ?? "(unnamed)", regionName);
                    return contentControl;
                }
            }
        }

        Log.Warning("DockingManagerRegionAdapter: No ContentControl found with RegionName='{RegionName}' among {ChildCount} children",
            regionName, dockingManager.Children.Count);
        return null;
    }
}
