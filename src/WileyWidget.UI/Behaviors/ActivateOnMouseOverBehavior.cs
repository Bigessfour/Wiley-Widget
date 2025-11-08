using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using Serilog;
using Syncfusion.Windows.Tools.Controls;

namespace Prism.Behaviors
{
    /// <summary>
    /// Activates/selects the DockingManager tab that contains the associated element when the mouse hovers it.
    /// Uses documented Syncfusion APIs:
    /// - DockingManager.ResolveManager(UIElement)
    /// - DockingManager.SelectTab(FrameworkElement)
    /// - DockingManager.GetState(DependencyObject)
    /// </summary>
    public class ActivateOnMouseOverBehavior : Behavior<FrameworkElement>
    {
        private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;

        public ActivateOnMouseOverBehavior()
        {
            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _debounceTimer.Tick += OnDebounceTick;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.MouseEnter += OnMouseEnter;
                Log.Debug("ActivateOnMouseOverBehavior: Attached to {ElementType}", AssociatedObject.GetType().Name);
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.MouseEnter -= OnMouseEnter;
            }
            _debounceTimer.Stop();
            _debounceTimer.Tick -= OnDebounceTick;
            base.OnDetaching();
            Log.Debug("ActivateOnMouseOverBehavior: Detached from element");
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                Log.Debug("ActivateOnMouseOverBehavior: MouseEnter on {Element} - scheduling activation", fe.Name ?? fe.GetType().Name);
            }
            // Debounce rapid mouse enters when moving across controls
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            Log.Debug("ActivateOnMouseOverBehavior: Debounce tick - attempting activation");
            TryActivateParentDockItem();
        }

        private void TryActivateParentDockItem()
        {
            var fe = AssociatedObject;
            if (fe == null)
            {
                return;
            }

            // Ensure the element is hosted by a DockingManager
            var manager = DockingManager.ResolveManager(fe);
            if (manager == null)
            {
                Log.Debug("ActivateOnMouseOverBehavior: No DockingManager resolved for element");
                return;
            }

            var candidate = FindNearestFrameworkElementAncestor(fe);
            if (candidate == null)
            {
                Log.Debug("ActivateOnMouseOverBehavior: No FrameworkElement ancestor found to activate");
                return;
            }

            try
            {
                DockingManager.SelectTab(candidate);
                Log.Information("ActivateOnMouseOverBehavior: SelectTab requested for {Element}", candidate.Name ?? candidate.GetType().Name);
            }
            catch
            {
                // SelectTab is a safe no-op when not in a tabbed host
                Log.Warning("ActivateOnMouseOverBehavior: SelectTab threw unexpectedly; continuing");
            }
        }

        private static FrameworkElement? FindNearestFrameworkElementAncestor(DependencyObject start)
        {
            var node = start;
            while (node != null)
            {
                if (node is FrameworkElement fe)
                {
                    return fe;
                }
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }
    }
}
