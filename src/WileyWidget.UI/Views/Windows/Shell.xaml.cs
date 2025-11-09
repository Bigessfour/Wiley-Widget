using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Prism.Navigation.Regions;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Windows {
    public partial class Shell : Window
    {
    private readonly IRegionManager _regionManager;
    private MainViewModel? _viewModel;
        private readonly System.Windows.Threading.DispatcherTimer _saveStateTimer;
        private bool _wicComponentsAvailable = true;

        // Parameterless constructor for test scenarios
        // Chains to main constructor to ensure proper initialization
        public Shell() : this(null)
        {
            Log.Debug("MainWindow: Parameterless constructor called - chaining to main constructor with null RegionManager");
        }

        public Shell(IRegionManager regionManager)
        {
            Log.Debug("MainWindow: Constructor called");

            _regionManager = regionManager;

            // **PROACTIVE WIC AVAILABILITY CHECK**
            // Verify Windows Imaging Component availability before XAML initialization
            CheckWicAvailability();

            // Initialize debouncing timer for state saving
            _saveStateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 500ms debounce
            };
            _saveStateTimer.Tick += SaveStateTimer_Tick;

            try
            {
                InitializeComponent();
            }
            catch (System.Windows.Markup.XamlParseException xamlEx)
            {
                Log.Error(xamlEx, "XAML Parse Exception during InitializeComponent");

                // **COMPREHENSIVE INNER EXCEPTION LOGGING**
                // Check for COMException and NotSupportedException in the inner exception chain
                var innerEx = xamlEx.InnerException;
                int exceptionDepth = 0;
                while (innerEx != null)
                {
                    exceptionDepth++;
                    Log.Error("Inner Exception [{Depth}]: {ExceptionType} - {Message}",
                        exceptionDepth, innerEx.GetType().Name, innerEx.Message);

                    if (innerEx is System.Runtime.InteropServices.COMException comEx)
                    {
                        Log.Error(comEx, "COMException details - HRESULT: 0x{HResult:X8}, Message: {Message}",
                            comEx.HResult, comEx.Message);
                        Log.Error("COMException Source: {Source}, ErrorCode: {ErrorCode}",
                            comEx.Source, comEx.ErrorCode);

                        // Common HRESULT codes for imaging issues
                        if (comEx.HResult == unchecked((int)0x88982F50)) // WINCODEC_ERR_COMPONENTNOTFOUND
                        {
                            Log.Error("WIC Component Not Found - Missing codec or imaging component");
                        }
                        else if (comEx.HResult == unchecked((int)0x80004005)) // E_FAIL
                        {
                            Log.Error("General COM failure - possibly missing WIC components");
                        }
                    }
                    else if (innerEx is NotSupportedException notSupportedEx)
                    {
                        Log.Error(notSupportedEx, "NotSupportedException - Feature or codec not supported");
                        Log.Error("NotSupportedException Source: {Source}", notSupportedEx.Source);
                    }

                    innerEx = innerEx.InnerException;
                }

                // **FALLBACK UI WITH PROGRAMMATIC ICON LOADING**
                try
                {
                    Log.Warning("Attempting to create fallback UI with programmatic image loading");

                    var fallbackPanel = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(20),
                        Orientation = System.Windows.Controls.Orientation.Vertical
                    };

                    // Use MahApps IconPack for warning icon (no PNG file needed)
                    try
                    {
                        var warningIcon = new MahApps.Metro.IconPacks.PackIconMaterial
                        {
                            Kind = MahApps.Metro.IconPacks.PackIconMaterialKind.AlertCircle,
                            Width = 64,
                            Height = 64,
                            Foreground = System.Windows.Media.Brushes.Orange,
                            Margin = new Thickness(0, 0, 0, 10)
                        };

                        fallbackPanel.Children.Add(warningIcon);
                        Log.Information("Successfully loaded fallback warning icon");
                    }
                    catch (Exception iconEx)
                    {
                        Log.Warning(iconEx, "Failed to load fallback icon - continuing without image");
                        // Continue without image - non-critical
                    }

                    var errorTextBlock = new System.Windows.Controls.TextBlock
                    {
                        Text = "Application initialization encountered an issue during startup.\n\n" +
                               "This may be caused by:\n" +
                               "• Missing image resources or codecs\n" +
                               "• XAML resource loading conflicts\n" +
                               "• System component dependencies\n\n" +
                               "The application is using fallback rendering. Check log files for details.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        LineHeight = 20
                    };
                    fallbackPanel.Children.Add(errorTextBlock);

                    if (!_wicComponentsAvailable)
                    {
                        var wicWarning = new System.Windows.Controls.TextBlock
                        {
                            Text = "\n⚠️ Windows Imaging Component (WIC) availability check failed.\n" +
                                   "Some imaging features may be unavailable.",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 12,
                            Foreground = System.Windows.Media.Brushes.Orange,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        fallbackPanel.Children.Add(wicWarning);
                    }

                    // Add Copy Logs button
                    var copyLogsButton = new System.Windows.Controls.Button
                    {
                        Content = "📋 Copy Startup Logs to Clipboard",
                        Margin = new Thickness(0, 20, 0, 0),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                    };
                    copyLogsButton.Click += CopyLogsButton_Click;
                    fallbackPanel.Children.Add(copyLogsButton);

                    // Add Export Logs button
                    var exportLogsButton = new System.Windows.Controls.Button
                    {
                        Content = "💾 Export Logs to File",
                        Margin = new Thickness(0, 10, 0, 0),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                    };
                    exportLogsButton.Click += ExportLogsButton_Click;
                    fallbackPanel.Children.Add(exportLogsButton);

                    // Add log file location info
                    var logLocationText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"\nLog files are located at:\n{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")}",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Margin = new Thickness(0, 20, 0, 0),
                        TextAlignment = TextAlignment.Center
                    };
                    fallbackPanel.Children.Add(logLocationText);

                    Content = fallbackPanel;
                    Width = 600;
                    Height = 520;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                catch (Exception fallbackEx)
                {
                    Log.Fatal(fallbackEx, "Failed to create fallback UI");
                    throw; // Re-throw if we can't even create a basic UI
                }

                // Re-throw the original exception after logging and fallback attempt
                throw;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unexpected exception during InitializeComponent");
                throw;
            }

            // Prism regions are automatically managed - no manual registration needed

            // Add event handlers for diagnostics
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            SizeChanged += MainWindow_SizeChanged;
            Activated += MainWindow_Activated;
            ContentRendered += MainWindow_ContentRendered;
            DataContextChanged += Shell_DataContextChanged;

            Log.Debug("MainWindow: Constructor completed, event handlers attached");
        }

        private void Shell_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = e.NewValue as MainViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                // Apply current theme on initial hookup
                ApplyTheme(_viewModel.CurrentTheme);
            }
        }

        /// <summary>
        /// Proactively checks Windows Imaging Component (WIC) availability.
        /// This diagnostic runs before XAML initialization to detect missing codecs or imaging components.
        /// Prevents silent failures during image resource loading.
        /// </summary>
        private void CheckWicAvailability()
        {
            try
            {
                Log.Information("[WIC_CHECK] Performing WIC availability diagnostic check...");
                bool hasWarnings = false;

                // Test 1: Try to create a simple bitmap to verify WIC functionality
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Log.Verbose("[WIC_CHECK] Test 1: Creating test bitmap to verify WIC functionality");

                    // Create a minimal BitmapImage to test WIC availability
                    var testBitmap = BitmapSource.Create(1, 1, 96, 96,
                        System.Windows.Media.PixelFormats.Bgr32, null,
                        new byte[4], 4);

                    sw.Stop();
                    Log.Verbose("[WIC_CHECK] Test bitmap created in {ElapsedMs}ms", sw.ElapsedMilliseconds);

                    if (testBitmap.CanFreeze)
                    {
                        testBitmap.Freeze();
                        Log.Information("[WIC_CHECK] ✓ Test 1 PASSED - WIC basic test successful (bitmap created and frozen in {ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        Log.Warning("[WIC_CHECK] ✗ Test 1 FAILED - WIC test bitmap creation succeeded but bitmap is invalid (cannot freeze)");
                        _wicComponentsAvailable = false;
                        ShowWicWarningDialog("WIC components not properly initialized");
                        return;
                    }
                }
                catch (NotSupportedException nsEx)
                {
                    Log.Error(nsEx, "[WIC_CHECK] ✗ Test 1 FAILED - WIC functional test failed - NotSupportedException during bitmap creation");
                    _wicComponentsAvailable = false;
                    ShowWicWarningDialog("WIC components not properly initialized");
                    return;
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    Log.Error(comEx, "[WIC_CHECK] ✗ Test 1 FAILED - WIC functional test failed - COMException during bitmap creation (HRESULT: 0x{HResult:X8})", comEx.HResult);
                    _wicComponentsAvailable = false;
                    ShowWicWarningDialog($"COM error accessing WIC (HRESULT: 0x{comEx.HResult:X8})");
                    return;
                }

                // Test 2: Try to access pack URI resolver (non-critical test)
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Log.Verbose("[WIC_CHECK] Test 2: Testing pack:// URI resource access (non-critical)");

                    // Try to access a known resource or use a proper pack URI format
                    // Using siteoforigin instead of application since we may not have embedded resources
                    var testUri = new Uri("pack://siteoforigin:,,,/", UriKind.Absolute);

                    // Test if pack URI resolver is available (may throw if resource doesn't exist)
                    try
                    {
                        var resourceStreamInfo = Application.GetResourceStream(testUri);
                    }
                    catch (System.IO.IOException)
                    {
                        // Expected if resource doesn't exist - this is fine
                    }

                    sw.Stop();
                    Log.Information("[WIC_CHECK] ✓ Test 2 PASSED - Pack URI resolver is available (tested in {ElapsedMs}ms)", sw.ElapsedMilliseconds);
                }
                catch (Exception packEx)
                {
                    Log.Warning("[WIC_CHECK] ⚠ Test 2 WARNING - Pack URI resource access test had issues (non-critical - may be normal if no resources exist): {Message}", packEx.Message);
                    hasWarnings = true;
                    // This is not critical - continue without failing WIC check
                }

                // Test 3: Verify PNG codec availability with proper PNG data
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Log.Verbose("[WIC_CHECK] Test 3: Verifying PNG codec availability with complete PNG data");

                    // Create a complete minimal 1x1 PNG file (base64 encoded)
                    var minimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
                    var pngBytes = Convert.FromBase64String(minimalPngBase64);

                    var pngDecoder = new PngBitmapDecoder(
                        new MemoryStream(pngBytes),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.None);

                    // Try to access the first frame to ensure decoder works
                    if (pngDecoder.Frames.Count > 0 && pngDecoder.Frames[0] != null)
                    {
                        sw.Stop();
                        Log.Information("[WIC_CHECK] ✓ Test 3 PASSED - PNG codec test passed - PNG decoder available and functional in {ElapsedMs}ms", sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        Log.Warning("[WIC_CHECK] ⚠ Test 3 WARNING - PNG decoder created but no frames available");
                        hasWarnings = true;
                    }
                }
                catch (Exception pngEx)
                {
                    Log.Warning(pngEx, "[WIC_CHECK] ⚠ Test 3 WARNING - PNG codec test failed - PNG support may be unavailable (non-critical)");
                    hasWarnings = true;
                    // This is a warning, not a fatal error
                }

                // Final status determination
                if (hasWarnings)
                {
                    Log.Warning("[WIC_CHECK] ✓ WIC availability check completed with warnings - core WIC components operational but some features may be limited");
                }
                else
                {
                    Log.Information("[WIC_CHECK] ✓ WIC availability check completed successfully - all components operational");
                }
                _wicComponentsAvailable = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WIC_CHECK] ✗ WIC availability check failed with unexpected exception");
                _wicComponentsAvailable = false;
                ShowWicWarningDialog($"Unexpected error during WIC check: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays a warning dialog to the user if WIC components are unavailable.
        /// </summary>
        private void ShowWicWarningDialog(string details)
        {
            try
            {
                var result = MessageBox.Show(
                    $"⚠️ Windows Imaging Component (WIC) Warning\n\n" +
                    $"Issue detected: {details}\n\n" +
                    $"This may cause issues with:\n" +
                    $"• PNG/JPEG image loading and display\n" +
                    $"• XAML resource initialization\n" +
                    $"• Application icon rendering\n\n" +
                    $"Most functionality will work normally. Consider installing:\n" +
                    $"• Microsoft Visual C++ Redistributables\n" +
                    $"• Windows Updates\n" +
                    $"• .NET Desktop Runtime\n\n" +
                    $"Continue loading application?",
                    "WIC Component Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes);

                if (result == MessageBoxResult.No)
                {
                    Log.Warning("User chose to abort application startup due to WIC issues");
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to display WIC warning dialog - continuing startup");
                // Continue without showing dialog - already logged the issue
            }
        }

        private void SaveStateTimer_Tick(object? sender, EventArgs e)
        {
            _saveStateTimer.Stop();
            // Obsolete: SaveDockingState removed - using Prism regions for layout management
            Log.Debug("SaveStateTimer tick (no-op - using Prism regions)");
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
                // Use SfSkinManager.ApplicationTheme (global theme) per Syncfusion v31.1.17 best practice
                // Setting ApplicationTheme applies to ALL windows and controls automatically
                // No need to call SetVisualStyle per-window
                SfSkinManager.ApplicationTheme = new Theme(themeName);
                Log.Information("Global application theme changed to: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply theme: {Theme}", themeName);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Start distributed tracing span for UI initialization
            using var uiLoadSpan = System.Diagnostics.Activity.Current?.Source.StartActivity("ui.mainwindow.loaded");
            uiLoadSpan?.SetTag("window.width", ActualWidth);
            uiLoadSpan?.SetTag("window.height", ActualHeight);
            uiLoadSpan?.SetTag("window.state", WindowState.ToString());

            // Memory tracking - before
            var gcMemoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetBefore = Environment.WorkingSet;

            Log.Information("MainWindow: Loaded event - Size: {Width}x{Height}, Position: ({Left}, {Top}), State: {State}, Visible: {IsVisible}",
                ActualWidth, ActualHeight, Left, Top, WindowState, IsVisible);
            Log.Information("Memory Before Load - GC: {GCMemory:N0} bytes, WorkingSet: {WorkingSet:N0} bytes",
                gcMemoryBefore, workingSetBefore);

            // Add memory metrics to trace
            uiLoadSpan?.SetTag("memory.gc_bytes", gcMemoryBefore);
            uiLoadSpan?.SetTag("memory.working_set_bytes", workingSetBefore);

            // **NULL-SAFE CONTROL INITIALIZATION**
            // Verify critical controls loaded successfully before applying styles or states
            try
            {
                // Check for DockingManager
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager == null)
                {
                    Log.Warning("MainDockingManager is null in Loaded event - control may not have initialized properly");
                    // Attempt to find in visual tree as fallback
                    dockingManager = FindVisualChild<DockingManager>(this);
                    if (dockingManager != null)
                    {
                        Log.Information("Found DockingManager in visual tree during Loaded event");
                        RegisterName("MainDockingManager", dockingManager);
                    }
                    else
                    {
                        Log.Error("DockingManager not found in visual tree - layout management may be impaired");
                    }
                }
                else
                {
                    Log.Information("MainDockingManager verified - control loaded successfully");
                }

                // Check for Ribbon controls if present
                var ribbon = FindVisualChild<Syncfusion.Windows.Tools.Controls.Ribbon>(this);
                if (ribbon != null)
                {
                    Log.Information("Ribbon control found - verifying button states");

                    // Verify RibbonButton controls are initialized
                    var ribbonButtons = FindVisualChildren<Syncfusion.Windows.Tools.Controls.RibbonButton>(ribbon);
                    var buttonCount = 0;
                    foreach (var button in ribbonButtons)
                    {
                        if (button == null)
                        {
                            Log.Warning("Found null RibbonButton in collection - skipping");
                            continue;
                        }

                        buttonCount++;

                        // Check if LargeIcon is set and valid
                        if (button.LargeIcon == null)
                        {
                            Log.Warning("RibbonButton '{ButtonLabel}' has null LargeIcon - no fallback available (use MahApps IconPacks IconTemplate instead)",
                                button.Label ?? "Unknown");
                            // Note: Removed PNG fallback system. All icons should use IconTemplate with MahApps.Metro.IconPacks
                        }

                        // Verify button is enabled and visible
                        if (!button.IsEnabled)
                        {
                            Log.Debug("RibbonButton '{ButtonLabel}' is disabled", button.Label ?? "Unknown");
                        }
                        if (button.Visibility != Visibility.Visible)
                        {
                            Log.Debug("RibbonButton '{ButtonLabel}' is not visible (Visibility: {Visibility})",
                                button.Label ?? "Unknown", button.Visibility);
                        }
                    }

                    Log.Information("Verified {ButtonCount} RibbonButton controls", buttonCount);
                }
                else
                {
                    Log.Information("No Ribbon control found in visual tree - may not be using ribbon UI");
                }

                // Check ContentControl
                var contentControl = Content as System.Windows.Controls.ContentControl;
                if (contentControl != null)
                {
                    Log.Information("MainWindow: ContentControl - ActualSize: {Width}x{Height}, Content: {ContentType}",
                        contentControl.ActualWidth, contentControl.ActualHeight,
                        contentControl.Content?.GetType().Name ?? "null");

                    if (contentControl.Content == null)
                    {
                        Log.Warning("ContentControl.Content is null - may indicate initialization issue");
                    }
                }
                else
                {
                    Log.Information("Content is not a ContentControl (Type: {ContentType})",
                        Content?.GetType().Name ?? "null");
                }
            }
            catch (Exception controlCheckEx)
            {
                Log.Error(controlCheckEx, "Error during control initialization verification");
                // Continue with initialization - this is diagnostic only
            }

            // ViewModel is auto-wired by Prism's ViewModelLocator; ensure we have a reference and log it
            if (DataContext is MainViewModel vm)
            {
                _viewModel = vm;
                Log.Information("MainViewModel resolved via ViewModelLocator");
                Log.Debug("DataContext type: {DataContextType}", DataContext.GetType().Name);
            }
            else
            {
                Log.Warning("DataContext is not MainViewModel yet; ViewModelLocator may set it later");
            }

            // Verify region status and log for diagnostics
            LogRegionStatus();

            // Load DockingManager state per Syncfusion docs
            LoadDockingManagerState();

            // Memory tracking - after
            var gcMemoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetAfter = Environment.WorkingSet;
            var gcMemoryDelta = gcMemoryAfter - gcMemoryBefore;
            var workingSetDelta = workingSetAfter - workingSetBefore;

            Log.Information("Memory After Load - GC: {GCMemory:N0} bytes (+{Delta:N0}), WorkingSet: {WorkingSet:N0} bytes (+{WSDelta:N0})",
                gcMemoryAfter, gcMemoryDelta, workingSetAfter, workingSetDelta);
            Log.Information("Total Memory Impact - GC Delta: {GCDeltaMB:F2} MB, WorkingSet Delta: {WSDeltaMB:F2} MB",
                gcMemoryDelta / 1024.0 / 1024.0, workingSetDelta / 1024.0 / 1024.0);

            // Show the window now that all initialization is complete
            Log.Information("MainWindow initialization complete - showing window");
            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
                Log.Information("MainWindow is now visible to user");
            }
            else
            {
                Log.Information("MainWindow is already visible");
            }

            // Display warning if WIC components were unavailable
            if (!_wicComponentsAvailable)
            {
                Log.Warning("WIC components have reduced functionality - user may experience limited image features");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        "⚠️ Windows Imaging Component Information\n\n" +
                        "Some imaging components have limited functionality.\n\n" +
                        "You may notice:\n" +
                        "• Some icons using fallback designs\n" +
                        "• PNG images replaced with vector graphics\n" +
                        "• Reduced image processing features\n\n" +
                        "The application will function normally with alternative rendering methods.",
                        "Imaging Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
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
            Log.Debug("MainWindow: Closed event");

            // Save DockingManager state per Syncfusion docs
            // PersistState=True enables auto-save, but explicit save ensures state is persisted
            try
            {
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager == null)
                {
                    Log.Warning("MainDockingManager not found in Shell.xaml - attempting to find in visual tree");
                    dockingManager = FindVisualChild<DockingManager>(this);
                }

                if (dockingManager != null)
                {
                    Log.Information("Saving DockingManager state to IsolatedStorage");
                    dockingManager.SaveDockState();
                    Log.Information("DockingManager state saved successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - state persistence unavailable");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save DockingManager state");
            }
        }

        /// <summary>
        /// Loads the DockingManager state from IsolatedStorage.
        /// Per Syncfusion docs: Call LoadDockState() in the Loaded event to restore previous layout.
        /// </summary>
        private void LoadDockingManagerState()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Log.Verbose("[DOCKING_MANAGER] Starting DockingManager state load operation");

                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager == null)
                {
                    Log.Warning("[DOCKING_MANAGER] MainDockingManager not found via FindName - cannot load state directly");
                    Log.Verbose("[DOCKING_MANAGER] Attempting to find DockingManager in visual tree as fallback");

                    // Try to find DockingManager in the visual tree as a fallback
                    dockingManager = FindVisualChild<DockingManager>(this);

                    if (dockingManager == null)
                    {
                        Log.Error("[DOCKING_MANAGER] ✗ MainDockingManager not found in visual tree - state persistence unavailable");
                        Log.Error("[DOCKING_MANAGER] DockingManager may need to be initialized programmatically or XAML may have errors");

                        // Log visual tree structure for diagnostics
                        LogVisualTreeStructure(this, 0, 3);
                        return;
                    }
                    else
                    {
                        Log.Information("[DOCKING_MANAGER] ✓ Found DockingManager in visual tree after {ElapsedMs}ms - registering as MainDockingManager",
                            sw.ElapsedMilliseconds);
                        Log.Verbose("[DOCKING_MANAGER] DockingManager details: Type={Type}, Name={Name}, IsLoaded={IsLoaded}",
                            dockingManager.GetType().FullName, dockingManager.Name ?? "(unnamed)", dockingManager.IsLoaded);
                        RegisterName("MainDockingManager", dockingManager);
                    }
                }
                else
                {
                    Log.Verbose("[DOCKING_MANAGER] ✓ MainDockingManager found via FindName in {ElapsedMs}ms", sw.ElapsedMilliseconds);
                }

                Log.Information("[DOCKING_MANAGER] Attempting to load DockingManager state from IsolatedStorage");
                sw.Restart();

                // Per Syncfusion docs: LoadDockState returns bool indicating success
                bool stateLoaded = dockingManager.LoadDockState();
                sw.Stop();

                if (stateLoaded)
                {
                    Log.Information("[DOCKING_MANAGER] ✓ DockingManager state loaded successfully from IsolatedStorage in {ElapsedMs}ms",
                        sw.ElapsedMilliseconds);

                    // Log child count for diagnostics
                    var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(dockingManager);
                    Log.Verbose("[DOCKING_MANAGER] DockingManager has {ChildCount} children in visual tree", childCount);
                }
                else
                {
                    Log.Information("[DOCKING_MANAGER] ⚠ No saved DockingManager state found or state mismatch - using default layout in {ElapsedMs}ms",
                        sw.ElapsedMilliseconds);
                    // Default layout is already defined in XAML with State and SideInDockedMode properties
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DOCKING_MANAGER] ✗ Failed to load DockingManager state - using default layout");
                // Continue with default XAML layout
            }
        }

        /// <summary>
        /// Helper method to find a child of a specific type in the visual tree
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Logs the visual tree structure for diagnostics (limited depth to avoid spam)
        /// </summary>
        private static void LogVisualTreeStructure(DependencyObject parent, int currentDepth, int maxDepth)
        {
            if (parent == null || currentDepth > maxDepth)
                return;

            var indent = new string(' ', currentDepth * 2);
            var typeName = parent.GetType().Name;
            var name = parent is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name) ? fe.Name : "(unnamed)";

            Log.Verbose("[VISUAL_TREE] {Indent}{TypeName} - Name: {Name}", indent, typeName, name);

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                LogVisualTreeStructure(child, currentDepth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Helper method to find all children of a specific type in the visual tree
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        /// <summary>
        /// Resets the DockingManager to default layout.
        /// Per Syncfusion docs: ResetState() clears saved state and returns to XAML-defined layout.
        /// </summary>
        public void ResetDockingManagerLayout()
        {
            try
            {
                var dockingManager = this.FindName("MainDockingManager") as DockingManager;
                if (dockingManager == null)
                {
                    Log.Warning("MainDockingManager not found - attempting to find in visual tree");
                    dockingManager = FindVisualChild<DockingManager>(this);
                }

                if (dockingManager != null)
                {
                    Log.Information("Resetting DockingManager to default layout");
                    dockingManager.ResetState();
                    Log.Information("DockingManager reset to default layout successfully");
                }
                else
                {
                    Log.Warning("MainDockingManager not found - cannot reset layout");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reset DockingManager layout");
            }
        }

        /// <summary>
        /// Copies startup logs to clipboard
        /// </summary>
        private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                var startupLogFile = Path.Combine(logsDir, "startup-debug.log");

                // If startup-debug.log doesn't exist, try the main log file
                if (!File.Exists(startupLogFile))
                {
                    var logFiles = Directory.GetFiles(logsDir, "*.log", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .ToList();

                    if (logFiles.Any())
                    {
                        startupLogFile = logFiles.First();
                        Log.Information("startup-debug.log not found, using most recent log: {LogFile}", startupLogFile);
                    }
                    else
                    {
                        MessageBox.Show("No log files found in the logs directory.", "No Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                var logContent = File.ReadAllText(startupLogFile);
                Clipboard.SetText(logContent);

                MessageBox.Show($"Log content copied to clipboard!\n\nFile: {Path.GetFileName(startupLogFile)}\nSize: {logContent.Length:N0} characters",
                    "Logs Copied", MessageBoxButton.OK, MessageBoxImage.Information);

                Log.Information("User copied logs to clipboard from: {LogFile}", startupLogFile);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to copy logs to clipboard");
                MessageBox.Show($"Failed to copy logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Exports startup logs to a user-selected file
        /// </summary>
        private void ExportLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                var startupLogFile = Path.Combine(logsDir, "startup-debug.log");

                // If startup-debug.log doesn't exist, try the main log file
                if (!File.Exists(startupLogFile))
                {
                    var logFiles = Directory.GetFiles(logsDir, "*.log", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .ToList();

                    if (logFiles.Any())
                    {
                        startupLogFile = logFiles.First();
                        Log.Information("startup-debug.log not found, using most recent log: {LogFile}", startupLogFile);
                    }
                    else
                    {
                        MessageBox.Show("No log files found in the logs directory.", "No Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"startup-debug-{DateTime.Now:yyyy-MM-dd-HHmmss}.log",
                    DefaultExt = ".log",
                    Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "Export Startup Logs"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.Copy(startupLogFile, saveFileDialog.FileName, overwrite: true);
                    MessageBox.Show($"Logs exported successfully to:\n{saveFileDialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    Log.Information("User exported logs to: {ExportPath}", saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export logs");
                MessageBox.Show($"Failed to export logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Get RegionManager from the window if not injected via constructor
            var regionManager = _regionManager ?? RegionManager.GetRegionManager(this);

            if (regionManager == null)
            {
                Log.Warning("RegionManager is null - no region status available");
                return;
            }

            try
            {
                var totalRegions = regionManager.Regions.Count();
                Log.Information("Total regions registered: {RegionCount}", totalRegions);

                foreach (var region in regionManager.Regions)
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

                // Check specifically for MainRegion since it's critical for initial view
                if (regionManager.Regions.ContainsRegionWithName("MainRegion"))
                {
                    var mainRegion = regionManager.Regions["MainRegion"];
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
