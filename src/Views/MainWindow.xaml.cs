using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using Prism.Navigation.Regions;
using WileyWidget.ViewModels;
using WileyWidget.Services;

namespace WileyWidget.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRegionManager _regionManager;
        private readonly MainViewModel _viewModel;
        private readonly System.Windows.Threading.DispatcherTimer _saveStateTimer;

        // Parameterless constructor for test scenarios
        public MainWindow() : this(null, null)
        {
        }

        public MainWindow(IRegionManager regionManager) : this(regionManager, null)
        {
        }

        public MainWindow(IRegionManager regionManager, MainViewModel viewModel)
        {
            Log.Debug("MainWindow: Constructor called");
            
            _regionManager = regionManager;
            _viewModel = viewModel;

            // Initialize debouncing timer for state saving
            _saveStateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 500ms debounce
            };
            _saveStateTimer.Tick += SaveStateTimer_Tick;
            
            InitializeComponent();
            
            // Add event handlers for diagnostics
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            SizeChanged += MainWindow_SizeChanged;
            Activated += MainWindow_Activated;
            ContentRendered += MainWindow_ContentRendered;

            // Subscribe to ViewModel property changes for theme switching
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            
            Log.Debug("MainWindow: Constructor completed, event handlers attached");
        }

        private void SaveStateTimer_Tick(object? sender, EventArgs e)
        {
            _saveStateTimer.Stop();
            SaveDockingState();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentTheme))
            {
                ApplyTheme(_viewModel.CurrentTheme);
            }
        }

        private void ApplyTheme(string themeName)
        {
            try
            {
                // Convert string to VisualStyles enum
                if (Enum.TryParse<Syncfusion.SfSkinManager.VisualStyles>(themeName, out var visualStyle))
                {
                    // Apply theme to the entire window and its Syncfusion controls
                    Syncfusion.SfSkinManager.SfSkinManager.SetVisualStyle(this, visualStyle);
                    Log.Information("Theme changed to: {Theme}", themeName);
                }
                else
                {
                    Log.Warning("Invalid theme name: {Theme}", themeName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply theme: {Theme}", themeName);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Memory tracking - before
            var gcMemoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetBefore = Environment.WorkingSet;
            
            Log.Information("MainWindow: Loaded event - Size: {Width}x{Height}, Position: ({Left}, {Top}), State: {State}, Visible: {IsVisible}",
                ActualWidth, ActualHeight, Left, Top, WindowState, IsVisible);
            Log.Information("Memory Before Load - GC: {GCMemory:N0} bytes, WorkingSet: {WorkingSet:N0} bytes", 
                gcMemoryBefore, workingSetBefore);
            
            // Step 1: Set DataContext first to ensure ViewModel is available
            if (_viewModel != null)
            {
                DataContext = _viewModel;
                Log.Information("MainViewModel set as DataContext");
                Log.Debug("DataContext type: {DataContextType}", DataContext.GetType().Name);
            }
            else
            {
                Log.Warning("MainViewModel was not injected via constructor");
            }
            
            // Step 2: Initialize Prism regions after DataContext is set
            InitializePrismRegions();
            
            // Step 3: Verify region status and log for diagnostics
            LogRegionStatus();
            
            // Step 4: Load docking state with enhanced error handling
            LoadDockStateWithFallback();
                
            // Log content control state
            var contentControl = Content as System.Windows.Controls.ContentControl;
            if (contentControl != null)
            {
                Log.Information("MainWindow: ContentControl - ActualSize: {Width}x{Height}, Content: {ContentType}",
                    contentControl.ActualWidth, contentControl.ActualHeight, 
                    contentControl.Content?.GetType().Name ?? "null");
            }
            
            // Memory tracking - after
            var gcMemoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetAfter = Environment.WorkingSet;
            var gcMemoryDelta = gcMemoryAfter - gcMemoryBefore;
            var workingSetDelta = workingSetAfter - workingSetBefore;
            
            Log.Information("Memory After Load - GC: {GCMemory:N0} bytes (+{Delta:N0}), WorkingSet: {WorkingSet:N0} bytes (+{WSDelta:N0})", 
                gcMemoryAfter, gcMemoryDelta, workingSetAfter, workingSetDelta);
            Log.Information("Total Memory Impact - GC Delta: {GCDeltaMB:F2} MB, WorkingSet Delta: {WSDeltaMB:F2} MB",
                gcMemoryDelta / 1024.0 / 1024.0, workingSetDelta / 1024.0 / 1024.0);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Log.Debug("MainWindow: SizeChanged - NewSize: {Width}x{Height}, PreviousSize: {PrevWidth}x{PrevHeight}",
                e.NewSize.Width, e.NewSize.Height, e.PreviousSize.Width, e.PreviousSize.Height);
        }

        private void MainWindow_Activated(object sender, System.EventArgs e)
        {
            Log.Debug("MainWindow: Activated event - Window brought to foreground");
        }

        private void MainWindow_ContentRendered(object sender, System.EventArgs e)
        {
            Log.Information("MainWindow: ContentRendered event - All content has been rendered");
        }

        private void MainWindow_Closed(object sender, System.EventArgs e)
        {
            Log.Information("MainWindow: Closed event - Saving docking state");
            
            // Save docking state
            try
            {
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    dockingManager.SaveDockState();
                    Log.Information("Docking state saved successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot save docking state");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save docking state");
            }
        }

        private void DockingManager_DockStateChanged(object sender, System.EventArgs e)
        {
            Log.Debug("DockingManager: DockStateChanged event");
            
            // Use Dispatcher to ensure UI updates happen on UI thread
            Dispatcher.Invoke(() =>
            {
                // Debounce the state saving to avoid too frequent saves
                _saveStateTimer.Stop();
                _saveStateTimer.Start();
                UpdateViewModel();
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Saves the current docking state to IsolatedStorage
        /// </summary>
        private void SaveDockingState()
        {
            try
            {
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    dockingManager.SaveDockState();
                    Log.Debug("Docking state saved successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot save docking state");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save docking state");
            }
        }

        private void DockingManager_ActiveWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Log.Debug("DockingManager: ActiveWindowChanged event");
            
            // Use Dispatcher to ensure UI updates happen on UI thread
            Dispatcher.Invoke(() =>
            {
                var dockingManager = d as DockingManager;
                if (dockingManager != null)
                {
                    // Update ViewModel's ActiveWindow property
                    if (_viewModel != null)
                    {
                        _viewModel.ActiveWindow = dockingManager.ActiveWindow;
                    }

                    if (dockingManager.ActiveWindow != null)
                    {
                        Log.Information("Active window changed to: {WindowName}", 
                            dockingManager.ActiveWindow.Name ?? dockingManager.ActiveWindow.GetType().Name);
                    }
                }
                UpdateViewModel();
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void DockingManager_WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Log.Debug("DockingManager: WindowClosing event for window");
            
            // Add any cleanup logic here if needed
            // e.Cancel = true; // Uncomment to prevent closing
        }

        private void DockingManager_WindowClosed(object sender, System.EventArgs e)
        {
            Log.Information("DockingManager: Window closed");
            
            // Handle window closed event - update region states
            Dispatcher.Invoke(() =>
            {
                UpdateViewModel();
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void UpdateViewModel()
        {
            // Update the MainViewModel with current docking state
            var viewModel = DataContext as ViewModels.MainViewModel;
            if (viewModel != null)
            {
                // Notify property changes or update docking-related properties
                // For now, just log the change
                Log.Debug("ViewModel updated with docking state change");
            }
        }

        private void LoadDockStateWithFallback()
        {
            try
            {
                // Load from IsolatedStorage
                using (IsolatedStorageFile isoStorage = IsolatedStorageFile.GetUserStoreForAssembly())
                {
                    using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream("DockingLayout.xml", FileMode.Open, isoStorage))
                    {
                        XDocument doc = XDocument.Load(isoStream);
                        // Validate and filter invalid states (per Syncfusion docs)
                        var invalidStates = doc.Descendants("DockState").Where(e => e.Value == "Hidden");
                        foreach (var state in invalidStates)
                        {
                            state.Value = "Dock"; // Fallback to valid state
                        }
                        using (var reader = doc.CreateReader())
                        {
                            var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                            if (dockingManager != null)
                            {
                                dockingManager.LoadDockState(reader);
                            }
                        }
                    }
                }
                Log.Information("Docking state loaded successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load docking state - using default layout");
                // Apply default layout
                LoadDefaultDockingLayout();
            }
        }

        /// <summary>
        /// Loads a default docking layout when state loading fails
        /// </summary>
        private void LoadDefaultDockingLayout()
        {
            Log.Information("Loading default docking layout");
            
            try
            {
                // Reset to default docking state
                // This ensures all dock windows are in a valid, visible state
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager != null)
                {
                    foreach (var dockingChild in dockingManager.Children)
                    {
                        if (dockingChild is FrameworkElement element)
                        {
                            // Set default docking properties
                            DockingManager.SetState(element, DockState.Dock);
                            DockingManager.SetDesiredWidthInDockedMode(element, 200);
                            DockingManager.SetDesiredHeightInDockedMode(element, 200);
                            
                            Log.Debug("Set default dock state for: {ElementName}", element.Name ?? element.GetType().Name);
                        }
                    }
                    
                    Log.Information("Default docking layout applied successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot set default layout");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply default docking layout");
            }
        }

        /// <summary>
        /// Initializes Prism regions after DataContext is set and DockingManager is ready
        /// </summary>
        private void InitializePrismRegions()
        {
            Log.Information("Initializing Prism regions using ViewRegistrationService");
            
            if (_regionManager == null)
            {
                Log.Warning("RegionManager is null - cannot initialize regions");
                return;
            }

            try
            {
                // Log current region count before initialization
                Log.Information("Current regions available: {RegionCount}", _regionManager.Regions.Count());
                
                // Check if regions are already available from XAML
                var availableRegions = _regionManager.Regions.Select(r => r.Name).ToArray();
                Log.Information("Available regions from XAML: [{Regions}]", string.Join(", ", availableRegions));
                
                // Use ViewRegistrationService for comprehensive registration
                var viewRegistrationService = new ViewRegistrationService(_regionManager);
                viewRegistrationService.RegisterAllViews();

                // Validate regions after registration
                var validationResult = viewRegistrationService.ValidateRegions();
                Log.Information("Region validation result: {Result}", validationResult);

                if (!validationResult.IsValid)
                {
                    Log.Warning("Some regions are missing: [{MissingRegions}]", 
                        string.Join(", ", validationResult.MissingRegions));
                }
                
                Log.Information("Prism regions initialization completed using ViewRegistrationService");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Prism regions");
            }
        }

        /// <summary>
        /// Safely registers a view with a region, checking for region existence first
        /// </summary>
        private void RegisterViewWithRegionSafely(string regionName, Type viewType)
        {
            try
            {
                if (_regionManager.Regions.ContainsRegionWithName(regionName))
                {
                    _regionManager.RegisterViewWithRegion(regionName, viewType);
                    Log.Debug("Successfully registered {ViewType} with region {RegionName}", viewType.Name, regionName);
                }
                else
                {
                    Log.Debug("Region {RegionName} not found - view {ViewType} not registered", regionName, viewType.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register {ViewType} with region {RegionName}", viewType.Name, regionName);
            }
        }

        /// <summary>
        /// Logs comprehensive region status for diagnostics
        /// </summary>
        private void LogRegionStatus()
        {
            Log.Information("=== Region Status Report ===");
            
            if (_regionManager == null)
            {
                Log.Warning("RegionManager is null - no region status available");
                return;
            }

            try
            {
                var totalRegions = _regionManager.Regions.Count();
                Log.Information("Total regions registered: {RegionCount}", totalRegions);
                
                foreach (var region in _regionManager.Regions)
                {
                    var viewCount = region.Views?.Count() ?? 0;
                    var activeView = region.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None";
                    
                    Log.Information("Region '{RegionName}': {ViewCount} views, Active: {ActiveView}", 
                        region.Name, viewCount, activeView);
                        
                    // Log each view in the region
                    if (region.Views != null)
                    {
                        foreach (var view in region.Views)
                        {
                            Log.Debug("  - View: {ViewType}", view.GetType().Name);
                        }
                    }
                }
                
                // Check specifically for MainRegion since it's critical
                if (_regionManager.Regions.ContainsRegionWithName("MainRegion"))
                {
                    var mainRegion = _regionManager.Regions["MainRegion"];
                    Log.Information("MainRegion status: {ViewCount} views, Active: {ActiveView}",
                        mainRegion.Views?.Count() ?? 0,
                        mainRegion.ActiveViews?.FirstOrDefault()?.GetType().Name ?? "None");
                }
                else
                {
                    Log.Warning("MainRegion not found in region manager!");
                }
                
                Log.Information("=== End Region Status Report ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate region status report");
            }
        }
    }
}