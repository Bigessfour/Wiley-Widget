using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using Serilog;
using Prism.Navigation.Regions;

namespace WileyWidget.Behaviors
{
    /// <summary>
    /// Behavior that manages Prism region initialization and coordination with DockingManager
    /// </summary>
    public class RegionManagerBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty RegionManagerProperty =
            DependencyProperty.Register("RegionManager", typeof(IRegionManager), typeof(RegionManagerBehavior), new PropertyMetadata(null));

        public IRegionManager RegionManager
        {
            get => (IRegionManager)GetValue(RegionManagerProperty);
            set => SetValue(RegionManagerProperty, value);
        }

        public static readonly DependencyProperty MainRegionNameProperty =
            DependencyProperty.Register("MainRegionName", typeof(string), typeof(RegionManagerBehavior), new PropertyMetadata("MainRegion"));

        public string MainRegionName
        {
            get => (string)GetValue(MainRegionNameProperty);
            set => SetValue(MainRegionNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded += OnLoaded;
                Log.Debug("RegionManagerBehavior: Attached to {ElementType}, waiting for Loaded event",
                    AssociatedObject.GetType().Name);
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnLoaded;
            }

            base.OnDetaching();

            Log.Debug("RegionManagerBehavior: Detached from {ElementType}", AssociatedObject?.GetType().Name ?? "null");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializePrismRegions();
        }

        /// <summary>
        /// Initializes Prism regions after DataContext is set and DockingManager is ready
        /// </summary>
        private void InitializePrismRegions()
        {
            if (RegionManager == null)
            {
                Log.Warning("RegionManagerBehavior: RegionManager is null - cannot initialize regions");
                return;
            }

            try
            {
                // Log current region count before initialization
                Log.Information("RegionManagerBehavior: Current regions available: {RegionCount}", RegionManager.Regions.Count());

                // Check if regions are already available from XAML
                var availableRegions = RegionManager.Regions.Select(r => r.Name).ToArray();
                Log.Information("RegionManagerBehavior: Available regions from XAML: [{Regions}]", string.Join(", ", availableRegions));

                // Explicitly create MainRegion if it doesn't exist
                if (!RegionManager.Regions.ContainsRegionWithName(MainRegionName))
                {
                    Log.Information("RegionManagerBehavior: {MainRegionName} not found - creating explicitly", MainRegionName);
                    var mainRegionControl = new ContentControl
                    {
                        Name = $"{MainRegionName}Control",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    // Prism.Regions.RegionManager.SetRegionName(mainRegionControl, MainRegionName);
                    // Prism.Regions.RegionManager.SetRegionManager(mainRegionControl, RegionManager);

                    // Add to the DockingManager as a docked element
                    var dockingManager = FindParentDockingManager();
                    if (dockingManager != null)
                    {
                        Syncfusion.Windows.Tools.Controls.DockingManager.SetState(mainRegionControl, Syncfusion.Windows.Tools.Controls.DockState.Dock);
                        Syncfusion.Windows.Tools.Controls.DockingManager.SetDesiredWidthInDockedMode(mainRegionControl, double.NaN); // Auto width
                        Syncfusion.Windows.Tools.Controls.DockingManager.SetDesiredHeightInDockedMode(mainRegionControl, double.NaN); // Auto height
                        dockingManager.Children.Add(mainRegionControl);
                        Log.Information("RegionManagerBehavior: {MainRegionName} created and added to DockingManager", MainRegionName);
                    }
                    else
                    {
                        Log.Warning("RegionManagerBehavior: DockingManager not found - cannot add {MainRegionName} control", MainRegionName);
                    }
                }
                else
                {
                    Log.Information("RegionManagerBehavior: {MainRegionName} already exists", MainRegionName);
                }

                // Note: View registration is now handled by Prism modules
                // Each module (DashboardModule, etc.) registers its own views
                Log.Information("RegionManagerBehavior: Prism regions initialization completed - modules will register views");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RegionManagerBehavior: Failed to initialize Prism regions");
            }
        }

        /// <summary>
        /// Finds the parent DockingManager in the visual tree
        /// </summary>
        private Syncfusion.Windows.Tools.Controls.DockingManager? FindParentDockingManager()
        {
            var current = AssociatedObject as DependencyObject;
            while (current != null)
            {
                if (current is Syncfusion.Windows.Tools.Controls.DockingManager dockingManager)
                {
                    return dockingManager;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
