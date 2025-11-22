using System;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.Behaviors
{
    /// <summary>
    /// Region behavior that automatically activates views when they are added to a region
    /// </summary>
    public class AutoActivateBehavior : RegionBehavior
    {
        public const string BehaviorKey = "AutoActivate";

        private readonly ILogger<AutoActivateBehavior> _logger;

        public AutoActivateBehavior(ILogger<AutoActivateBehavior> logger)
        {
            _logger = logger;
        }

        protected override void OnAttach()
        {
            Region.Views.CollectionChanged += Views_CollectionChanged;
            Region.ActiveViews.CollectionChanged += ActiveViews_CollectionChanged;

            _logger.LogDebug("AutoActivateBehavior attached to region '{RegionName}'", Region.Name);
        }

        private void Views_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var view in e.NewItems)
                {
                    if (view != null && !Region.ActiveViews.Contains(view))
                    {
                        try
                        {
                            Region.Activate(view);
                            _logger.LogDebug("Auto-activated view in region '{RegionName}'", Region.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to auto-activate view in region '{RegionName}'", Region.Name);
                        }
                    }
                }
            }
        }

        private void ActiveViews_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Optional: Handle deactivation if needed
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                _logger.LogDebug("View deactivated in region '{RegionName}'", Region.Name);
            }
        }
    }
}