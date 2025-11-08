using System;
using System.Linq;
using System.Windows;
using Prism.Navigation.Regions;

namespace WileyWidget.Regions
{
    /// <summary>
    /// Keeps a region's Context synchronized with the active view's DataContext.
    /// Useful when commands or behaviors rely on Region.Context for parameter binding.
    /// </summary>
    public class SyncContextWithHostBehavior : RegionBehavior
    {
        public const string BehaviorKey = "SyncContextWithHost";

        protected override void OnAttach()
        {
            Region.ActiveViews.CollectionChanged += ActiveViews_CollectionChanged;
            SyncFromActiveView();
        }

        private void ActiveViews_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SyncFromActiveView();
        }

        private void SyncFromActiveView()
        {
            var active = Region.ActiveViews.FirstOrDefault();
            if (active is FrameworkElement fe)
            {
                Region.Context = fe.DataContext;
            }
        }
    }
}
