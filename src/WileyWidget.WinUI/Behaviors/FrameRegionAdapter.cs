using Microsoft.UI.Xaml.Controls;
using Prism.Regions;
using System;

namespace WileyWidget.WinUI.Behaviors
{
    public class FrameRegionAdapter : RegionAdapterBase<Frame>
    {
        public FrameRegionAdapter(IRegionBehaviorFactory factory) : base(factory) { }

        protected override void Adapt(IRegion region, Frame regionTarget)
        {
            region.Views.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var view in e.NewItems)
                    {
                        if (view is Microsoft.UI.Xaml.UIElement ui)
                            regionTarget.Content = ui;
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    regionTarget.Content = null;
                }
            };
        }

        protected override IRegion CreateRegion() => new AllActiveRegion();
    }
}
