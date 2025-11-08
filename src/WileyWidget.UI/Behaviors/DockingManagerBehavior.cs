using System;
using System.Windows;
using Microsoft.Xaml.Behaviors;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.UI.Behaviors
{
    /// <summary>
    /// Behavior that manages DockingManager state persistence and event handling
    /// </summary>
    public class DockingManagerBehavior : Behavior<DockingManager>
    {
        // Holds the last observed active window while suppression is enabled
        private DependencyObject? _pendingActiveWindow;
        private readonly System.Windows.Threading.DispatcherTimer _saveStateTimer;
        private const int MaxFloatingWindows = 3; // enforce a soft cap to reduce clutter

        public DockingManagerBehavior()
        {
            _saveStateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 500ms debounce
            };
            _saveStateTimer.Tick += SaveStateTimer_Tick;
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.DockStateChanged += OnDockStateChanged;
                AssociatedObject.ActiveWindowChanged += OnActiveWindowChanged;

                Log.Debug("DockingManagerBehavior: Attached to DockingManager, event handlers registered");
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.DockStateChanged -= OnDockStateChanged;
                AssociatedObject.ActiveWindowChanged -= OnActiveWindowChanged;
            }

            _saveStateTimer.Stop();
            _saveStateTimer.Tick -= SaveStateTimer_Tick;

            base.OnDetaching();

            Log.Debug("DockingManagerBehavior: Detached from DockingManager");
        }

        private void SaveStateTimer_Tick(object? sender, EventArgs e)
        {
            _saveStateTimer.Stop();
            SaveDockingState();
        }

        private void OnDockStateChanged(object sender, EventArgs e)
        {
            Log.Debug("DockingManagerBehavior: DockStateChanged event");

            // Debounce the state saving to avoid too frequent saves
            _saveStateTimer.Stop();
            _saveStateTimer.Start();

            // Docking state change logged - ViewModel notification removed for simplicity

            try
            {
                // Soft policy: if too many floating windows exist, move latest active to Document to declutter
                if (AssociatedObject != null)
                {
                    // ActiveWindow is documented; ChangeState is a documented static method
                    var active = AssociatedObject.ActiveWindow as FrameworkElement;
                    // Count floating windows by checking state on DockingManager children
                    int floatCount = 0;
                    foreach (var child in AssociatedObject.Children)
                    {
                        if (child is FrameworkElement fe)
                        {
                            var state = DockingManager.GetState(fe);
                            if (state == DockState.Float)
                            {
                                floatCount++;
                            }
                        }
                    }

                    if (floatCount > MaxFloatingWindows && active != null)
                    {
                        Log.Information("DockingManagerBehavior: Float windows={FloatCount} exceeds cap={Cap}; moving active to Document", floatCount, MaxFloatingWindows);
                        DockingManager.ChangeState(active, DockState.Document);
                        Log.Information("DockingManagerBehavior: Reduced float clutter by converting active to Document; float windows: {Count}", floatCount);
                    }
                    else
                    {
                        Log.Debug("DockingManagerBehavior: Float windows within cap (count={FloatCount}, cap={Cap})", floatCount, MaxFloatingWindows);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DockingManagerBehavior: Failed to enforce float window cap policy");
            }
        }

        private void OnActiveWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Log.Debug("DockingManagerBehavior: ActiveWindowChanged event");

            // If suppression flag is set on the DockingManager, don't process each event immediately.
            // Instead, remember the last active window and emit a single log when suppression is removed.
            if (AssociatedObject == null)
            {
                return;
            }

            var isSuppressed = Prism.Behaviors.DockingManagerSuppress.GetSuppressActiveWindowEvents(AssociatedObject);
            if (isSuppressed)
            {
                // record pending active window; don't update viewmodel or log repeatedly
                _pendingActiveWindow = AssociatedObject.ActiveWindow;
                // store a friendly name for diagnostics
                Prism.Behaviors.DockingManagerSuppress.SetLastActiveWindowName(AssociatedObject, _pendingActiveWindow?.GetType().Name ?? string.Empty);
                Log.Debug("DockingManagerBehavior: ActiveWindowChanged suppressed; queued: {WindowName}", _pendingActiveWindow?.GetType().Name ?? "(null)");
                return;
            }

            // Normal processing when not suppressed
            if (AssociatedObject.DataContext is MainViewModel viewModel)
            {
                viewModel.ActiveWindow = AssociatedObject.ActiveWindow;

                if (AssociatedObject.ActiveWindow != null)
                {
                    Log.Information("DockingManagerBehavior: Active window changed to: {WindowName}",
                        AssociatedObject.ActiveWindow.Name ?? AssociatedObject.ActiveWindow.GetType().Name);
                }
            }

            // If there was a pending active window from prior suppression, clear it (we already applied current)
            _pendingActiveWindow = null;
        }

        /// <summary>
        /// Saves the current docking state to IsolatedStorage
        /// </summary>
        private void SaveDockingState()
        {
            try
            {
                if (AssociatedObject != null)
                {
                    Log.Debug("DockingManagerBehavior: Calling SaveDockState()");
                    AssociatedObject.SaveDockState();
                    Log.Debug("DockingManagerBehavior: Docking state saved successfully");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DockingManagerBehavior: Failed to save docking state");
            }
        }
    }
}
