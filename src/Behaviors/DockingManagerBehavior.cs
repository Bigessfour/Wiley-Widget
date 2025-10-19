using System;
using System.Windows;
using Microsoft.Xaml.Behaviors;
using Syncfusion.Windows.Tools.Controls;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// Behavior that manages DockingManager state persistence and event handling
    /// </summary>
    public class DockingManagerBehavior : Behavior<DockingManager>
    {
        private readonly System.Windows.Threading.DispatcherTimer _saveStateTimer;

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
        }

        private void OnActiveWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Log.Debug("DockingManagerBehavior: ActiveWindowChanged event");

            if (AssociatedObject?.DataContext is ViewModels.MainViewModel viewModel)
            {
                viewModel.ActiveWindow = AssociatedObject.ActiveWindow;

                if (AssociatedObject.ActiveWindow != null)
                {
                    Log.Information("DockingManagerBehavior: Active window changed to: {WindowName}",
                        AssociatedObject.ActiveWindow.Name ?? AssociatedObject.ActiveWindow.GetType().Name);
                }
            }
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