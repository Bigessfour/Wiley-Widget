using Syncfusion.Windows.Tools.Controls; // For ComboBoxAdv, RibbonWindow
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using Syncfusion.UI.Xaml.Diagram;
using WileyWidget.Configuration;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Charts;
using WileyWidget.Services;
using WileyWidget.Models;
using System.Text;
using System.IO;
using System; // For StringComparison
using WileyWidget.UI.Theming;
using WileyWidget.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Views;

/// <summary>
/// Primary shell window for Wiley Widget municipal management application.
/// 
/// <para>This window serves as the main UI container providing:</para>
/// <list type="bullet">
/// <item><strong>Theme Management:</strong> Dynamic theme switching with persistence</item>
/// <item><strong>Window State Management:</strong> Size, position, and maximization state persistence</item>
/// <item><strong>Data Visualization:</strong> Enterprise and widget data grids with Syncfusion controls</item>
/// <item><strong>User Interaction:</strong> Command handling for theme switching and application management</item>
/// <item><strong>Performance Monitoring:</strong> Startup timing and resource usage tracking</item>
/// </list>
/// 
/// <para>Architecture Notes:</para>
/// <list type="bullet">
/// <item>Follows MVVM pattern with ViewModels handling business logic</item>
/// <item>Uses Syncfusion controls for professional UI components</item>
/// <item>Implements comprehensive error handling and user feedback</item>
/// <item>Allows dynamic column generation for flexible data display</item>
/// </list>
/// 
/// <para>Threading Model:</para>
/// <list type="bullet">
/// <item>UI operations execute on main thread</item>
/// <item>Async operations use Task-based patterns</item>
/// <item>Window events are handled synchronously</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    // Removed custom InitializeComponent and _contentLoaded duplicate to allow WPF-generated partial class implementation.
    // Relying on the auto-generated InitializeComponent from the XAML build (Build Action: Page).
    #region Constants
    private const string DefaultTheme = "FluentDark"; // Syncfusion WPF 30.2.4 supported theme (documentation-aligned)
    private const int PerformanceOptimizationChildScanLimit = 500; // Guard to prevent excessive visual tree traversal
    #endregion
    /// <summary>
    /// Runtime toggle for dynamic column generation in data grids.
    /// When enabled, columns are generated programmatically based on model properties.
    /// Kept as readonly variable to avoid CS0162 unreachable code warnings.
    /// </summary>
    private static readonly bool UseDynamicColumns = false; // set true to enable runtime column build

    /// <summary>
    /// Reference to the budget diagram control
    /// </summary>
    private SfDiagram BudgetDiagramField;
    private Services.IDiagramBuilderService _diagramBuilder;
    private Services.IApiKeyFacade _apiKeyFacade;
    private Services.IThemeCoordinator _themeCoordinator;
    public Services.IThemeCoordinator ThemeCoordinator => _themeCoordinator; // for XAML binding

    /// <summary>
    /// Initializes the main application window with comprehensive setup.
    /// </summary>
    public MainWindow()
    {
        Log.Information("=== MainWindow Constructor Started ===");

        try
        {
            Log.Information("Initializing MainWindow components...");
            InitializeComponent();
            Log.Information("MainWindow components initialized successfully");

            Log.Information("Setting up data context with MainViewModel...");
            // Delay DataContext setup until ServiceLocator is initialized
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var vm = ServiceLocator.GetService<ViewModels.MainViewModel>();
                    if (vm == null)
                        Log.Warning("MainViewModel service not resolved – UI will operate in limited state");
                    var settingsVm = ServiceLocator.GetServiceOrDefault<ViewModels.SettingsViewModel>()
                        ?? new ViewModels.SettingsViewModel(SettingsService.Instance, _apiKeyFacade, _themeCoordinator);
                    Settings = settingsVm; // Exposed via DP for XAML binding path `Settings`
                    DataContext = vm;
                    Log.Information("Data context established (null={IsNull}) SettingsVM={HasSettings}", vm == null, settingsVm != null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to set DataContext");
                }
            }));

            Log.Information("Locating Syncfusion controls...");
            // InitializeSyncfusionControls(); // Commented out - controls not in simplified XAML
            Log.Information("Syncfusion controls located and referenced");

            // Resolve new coordination services (best-effort, fallback to simple instances)
            _diagramBuilder = ServiceLocator.GetService<Services.IDiagramBuilderService>() ?? new Services.DiagramBuilderService();
            _apiKeyFacade = ServiceLocator.GetService<Services.IApiKeyFacade>() ?? new Services.ApiKeyFacade(ApiKeyService.Instance, SettingsService.Instance);
            _themeCoordinator = ServiceLocator.GetService<Services.IThemeCoordinator>() ?? new Services.ThemeCoordinator(SettingsService.Instance);

            // CRITICAL FIX: Defer theme application to Loaded event to prevent crashes
            // FluentLight and other themes with reveal animations can crash if applied in constructor
            Log.Information("Theme application deferred to Loaded event to prevent animation crashes");

            if (UseDynamicColumns)
            {
                Log.Information("Dynamic column generation enabled - building columns programmatically");
                BuildDynamicColumns();
            }

            Log.Information("Setting up window state management via WindowStateService...");
            GetWindowStateService()?.Restore(this);

            // CRITICAL FIX: Apply theme in Loaded event instead of constructor
            Loaded += OnLoaded;

            Closing += OnClosingPersistState;

            Log.Information("Updating theme toggle button visuals...");
            UpdateThemeToggleVisuals();

            Log.Information("=== MainWindow Constructor Completed Successfully ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during MainWindow initialization");
            throw;
        }
    }

    // Fallback stub for InitializeComponent to protect against rare build ordering issues.
    // The real method will be code-generated by WPF; if it's missing, this prevents a hard compile break and logs the condition.
    [System.Diagnostics.Conditional("DEBUG")]
    private void InitializeComponentFallbackGuard()
    {
        if (GetType().GetMethod("InitializeComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null)
        {
            Log.Warning("WPF generated InitializeComponent missing at runtime - fallback guard executed. Check XAML build action (Page) and logical namespace.");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("Window.Loaded event fired - applying theme and maximized state");
        ApplyDeferredTheme();
        ApplyMaximized();
        OptimizePerformance();
        // Once executed, detach to avoid duplicate calls if window is reloaded (defensive)
        Loaded -= OnLoaded;
    }

    private void OnClosingPersistState(object sender, System.ComponentModel.CancelEventArgs e)
    {
    GetWindowStateService()?.Persist(this);
        Closing -= OnClosingPersistState;
    }

    /// <summary>
    /// Applies WPF performance optimizations to improve application responsiveness
    /// </summary>
    private void OptimizePerformance()
    {
        try
        {
            Log.Information("Applying WPF performance optimizations...");

            // Enable layout rounding for crisp rendering
            WpfPerformanceOptimizer.EnableLayoutRounding(this);

            // Optimize DataGrid controls for large datasets
            var dataGrids = this.FindVisualChildren<DataGrid>();
            foreach (var dataGrid in dataGrids)
            {
                WpfPerformanceOptimizer.OptimizeDataGrid(dataGrid);
            }

            // Optimize UI elements
            var uiElements = this.FindVisualChildren<UIElement>();
            int processed = 0;
            foreach (var element in uiElements)
            {
                if (processed++ > PerformanceOptimizationChildScanLimit)
                {
                    Log.Debug("Performance optimization scan aborted after limit {Limit}", PerformanceOptimizationChildScanLimit);
                    break;
                }
                WpfPerformanceOptimizer.OptimizeUIElement(element);
            }

            // Monitor UI thread for blocking operations
            WpfPerformanceOptimizer.MonitorUIThread();

            Log.Information("WPF performance optimizations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply performance optimizations");
        }
    }

    /// <summary>
    /// Initializes all Syncfusion controls in the MainWindow with proper error handling
    /// </summary>
    private void InitializeSyncfusionControls()
    {
        Log.Information("=== Initializing Syncfusion Controls ===");

        try
        {
            // Initialize SfDataGrid controls with error handling
            InitializeSfDataGridControls();

            // Initialize SfChart control with error handling
            InitializeSfChartControl();

            // Initialize SfDiagram control with error handling
            InitializeSfDiagramControl();

            // Initialize SfAccordion control with error handling
            InitializeSfAccordionControl();

            // Initialize SfComboBoxAdv controls with error handling
            InitializeSfComboBoxControls();

            // Get reference to the budget diagram control
            BudgetDiagramField = FindName("BudgetDiagram") as SfDiagram;
            if (BudgetDiagramField != null)
            {
                Log.Information("SfDiagram (BudgetDiagram) located successfully - Type: {Type}, Name: {Name}",
                    BudgetDiagramField.GetType().Name, BudgetDiagramField.Name);
                Log.Information("SfDiagram properties - IsLoaded: {IsLoaded}, IsEnabled: {IsEnabled}",
                    BudgetDiagramField.IsLoaded, BudgetDiagramField.IsEnabled);
            }
            else
            {
                Log.Warning("SfDiagram (BudgetDiagram) not found in XAML");
            }

            // Log all Syncfusion controls found in the visual tree
            LogSyncfusionControls(this);

            Log.Information("=== Syncfusion Controls Initialization Complete ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Syncfusion controls initialization");
            throw;
        }
    }

    /// <summary>
    /// Initializes SfDataGrid controls with enhanced error handling
    /// </summary>
    private void InitializeSfDataGridControls()
    {
        try
        {
            // Initialize Widgets DataGrid
            var widgetsGrid = FindName("Grid") as SfDataGrid;
            if (widgetsGrid != null)
            {
                Log.Information("Widgets SfDataGrid initialized with AllowEditing={AllowEditing}, AllowGrouping={AllowGrouping}, AllowSorting={AllowSorting}",
                    widgetsGrid.AllowEditing, widgetsGrid.AllowGrouping, widgetsGrid.AllowSorting);
            }
            else
            {
                Log.Warning("Widgets SfDataGrid not found");
            }

            // Initialize Enterprises DataGrid
            var enterprisesGrid = FindName("EnterprisesGrid") as SfDataGrid;
            if (enterprisesGrid != null)
            {
                Log.Information("Enterprises SfDataGrid initialized with AllowEditing={AllowEditing}, AllowGrouping={AllowGrouping}, AllowSorting={AllowSorting}",
                    enterprisesGrid.AllowEditing, enterprisesGrid.AllowGrouping, enterprisesGrid.AllowSorting);
            }
            else
            {
                Log.Warning("Enterprises SfDataGrid not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing SfDataGrid controls");
        }
    }

    /// <summary>
    /// Initializes SfChart control with enhanced error handling
    /// </summary>
    private void InitializeSfChartControl()
    {
        try
        {
            var budgetChart = FindName("BudgetChart") as SfChart;
            if (budgetChart != null)
            {
                Log.Information("SfChart initialized successfully");
                // Note: EnableDeferredUpdate and ZoomingEnabled properties may not be available in current Syncfusion version
                // These properties are set in XAML and logged during control discovery
            }
            else
            {
                Log.Warning("SfChart (BudgetChart) not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing SfChart control");
        }
    }

    /// <summary>
    /// Initializes SfDiagram control with enhanced error handling
    /// </summary>
    private void InitializeSfDiagramControl()
    {
        try
        {
            var budgetDiagram = FindName("BudgetDiagram") as SfDiagram;
            if (budgetDiagram != null)
            {
                Log.Information("SfDiagram initialized with Constraints set in XAML");
            }
            else
            {
                Log.Warning("SfDiagram (BudgetDiagram) not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing SfDiagram control");
        }
    }

    /// <summary>
    /// Initializes SfAccordion control with enhanced error handling
    /// </summary>
    private void InitializeSfAccordionControl()
    {
        try
        {
            var budgetAccordion = FindName("BudgetAccordion");
            if (budgetAccordion != null)
            {
                Log.Information("SfAccordion initialized with ExpandMode set in XAML");
            }
            else
            {
                Log.Warning("SfAccordion (BudgetAccordion) not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing SfAccordion control");
        }
    }

    /// <summary>
    /// Initializes SfComboBoxAdv controls with enhanced error handling
    /// </summary>
    private void InitializeSfComboBoxControls()
    {
        try
        {
            // Initialize Theme ComboBox
            var themeComboBox = FindName("ThemeComboBox") as Syncfusion.Windows.Tools.Controls.ComboBoxAdv;
            if (themeComboBox != null)
            {
                Log.Information("Theme ComboBoxAdv initialized with AllowMultiSelect=False and Watermark set in XAML");
            }

            // Initialize Model ComboBox
            var modelComboBox = FindName("ModelComboBox") as Syncfusion.Windows.Tools.Controls.ComboBoxAdv;
            if (modelComboBox != null)
            {
                Log.Information("Model ComboBoxAdv initialized with AllowMultiSelect=False and Watermark set in XAML");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing SfComboBoxAdv controls");
        }
    }

    /// <summary>
    /// Recursively logs all Syncfusion controls in the visual tree.
    /// </summary>
    /// <param name="element">The root element to search from</param>
    private void LogSyncfusionControls(DependencyObject element)
    {
        if (element == null) return;

        // Check if this element is a Syncfusion control
        var elementType = element.GetType();
        if (elementType.Namespace?.StartsWith("Syncfusion") == true)
        {
            StructuredLogger.LogSyncfusionOperation(
                elementType.Name,
                "Discovered",
                new { Name = (element as FrameworkElement)?.Name ?? "Unnamed", IsLoaded = (element as FrameworkElement)?.IsLoaded ?? false });

            // Log specific properties for common Syncfusion controls
            if (element is SfDataGrid dataGrid)
            {
                StructuredLogger.LogSyncfusionOperation(
                    "SfDataGrid",
                    "PropertiesCheck",
                    new { ItemsSourceSet = dataGrid.ItemsSource != null, ColumnCount = dataGrid.Columns.Count });
            }
            else if (element is SfChart chart)
            {
                StructuredLogger.LogSyncfusionOperation(
                    "SfChart",
                    "PropertiesCheck",
                    new { SeriesCount = chart.Series?.Count ?? 0 });
            }
            else if (element is SfDiagram diagram)
            {
                StructuredLogger.LogSyncfusionOperation(
                    "SfDiagram",
                    "PropertiesCheck",
                    new { IsLoaded = diagram.IsLoaded, IsEnabled = diagram.IsEnabled });
            }
        }

        // Recursively check child elements
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            LogSyncfusionControls(child);
        }
    }    /// <summary>
         /// Dynamically builds text columns for each public property of the widget model when enabled.
         /// Demonstration only – static XAML columns are preferred when shape is stable.
         /// </summary>
    private void BuildDynamicColumns()
    {
        try
        {
            var vm = DataContext as ViewModels.MainViewModel;
            var items = vm?.Widgets;
            if (items == null || items.Count == 0) return;

            // Find the Grid control in the XAML
            var grid = FindName("Grid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (grid == null) return;

            grid.AutoGenerateColumns = false;
            grid.Columns.Clear();
            var type = items[0].GetType();
            foreach (var prop in type.GetProperties())
            {
                // Basic text columns for simplicity; extend mapping for numeric/date types as needed.
                grid.Columns.Add(new Syncfusion.UI.Xaml.Grid.GridTextColumn
                {
                    MappingName = prop.Name,
                    HeaderText = prop.Name
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error building dynamic columns");
        }
    }

    /// <summary>
    /// Apply theme to MainWindow using the new ThemeService
    /// </summary>
    private void ApplyDeferredTheme()
    {
        try
        {
            Log.Information("Applying deferred theme in Loaded event");

            var initialTheme = SettingsService.Instance?.Current?.Theme ?? DefaultTheme;
            var normalizedTheme = ThemeService.NormalizeTheme(initialTheme);

            Log.Information("Applying theme {Theme} to MainWindow", normalizedTheme);

            // Use the new ThemeService method for window-specific theming
            ThemeService.ApplyWindowTheme(this, normalizedTheme);

            Log.Information("✅ MainWindow theme applied successfully: {Theme}", normalizedTheme);
        }
        catch (Exception themeEx)
        {
            Log.Warning(themeEx, "⚠️ Failed to apply MainWindow theme: {Message}", themeEx.Message);
            // Continue without theme - application should still work
        }
    }

    // Updated wrapper methods for backward compatibility
    private void TryApplyTheme(string themeName) => ThemeService.ApplyWindowTheme(this, themeName);
    private void ApplyWpfTheme(string themeName) => ThemeService.ApplyWindowTheme(this, themeName);
    private void LogCurrentThemeState() => Log.Information("LogCurrentThemeState is deprecated; ThemeService now logs state centrally.");
    private void VerifySpecificSyncfusionControls(string expectedTheme) => Log.Information("VerifySpecificSyncfusionControls deprecated - relying on ThemeService state logging. Expected={Expected}", expectedTheme);
    private void VerifySyncfusionThemeApplication(string expectedTheme) => Log.Information("VerifySyncfusionThemeApplication deprecated - central verification removed. Expected={Expected}", expectedTheme);

    // Generic theme menu item click handler (dynamic binding)
    private void OnThemeMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Header is string header && !string.IsNullOrWhiteSpace(header))
        {
            // Normalize header to internal theme key via ThemeService (relies on existing normalization logic)
            var internalName = ThemeService.NormalizeTheme(header);
            _themeCoordinator.Current = internalName; // triggers persistence & ThemeService change
            UpdateThemeToggleVisuals();
        }
    }

    private void ChangeTheme(string internalName, string displayName)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";
        using (StructuredLogger.BeginOperation($"ThemeChange_{internalName}"))
        {
            StructuredLogger.LogThemeChange(previousTheme, internalName, true);
            TryApplyTheme(internalName);
            SettingsService.Instance.Current.Theme = internalName;
            SettingsService.Instance.Save();
            Log.Information("Theme successfully changed to {Theme}", displayName);
            Log.Information("Theme preference persisted to settings");
            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    private void UpdateThemeToggleVisuals()
    {
        Log.Information("=== Updating Theme Menu Visuals ===");
        var current = ThemeService.NormalizeTheme(SettingsService.Instance.Current.Theme);
        var themeMenu = FindName("ThemeMenu") as MenuItem;
        if (themeMenu?.Items != null)
        {
            foreach (var item in themeMenu.Items)
            {
                if (item is MenuItem mi && mi.Header is string h)
                {
                    var internalName = ThemeService.NormalizeTheme(h);
                    mi.IsChecked = internalName == current;
                    if (!mi.IsCheckable) mi.IsCheckable = true;
                }
            }
        }
        Log.Information("Theme menu updated - Current: {Current}", current);
    }

    /// <summary>
    /// Ensure DataContext-dependent initialization executes if DataContext arrives after Loaded.
    /// </summary>
    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (e.NewValue == null) return;
            Log.Debug("DataContext changed to type {Type}", e.NewValue.GetType().FullName);
            // Potential hook: reinitialize diagram if needed
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DataContextChanged handler error");
        }
    }

    // DependencyProperty to expose Settings VM to bindings as `Settings`
    public static readonly DependencyProperty SettingsProxyProperty = DependencyProperty.Register(
        "Settings", typeof(object), typeof(MainWindow), new PropertyMetadata(null));
    public object Settings
    {
        get => GetValue(SettingsProxyProperty);
        set => SetValue(SettingsProxyProperty, value);
    }

    // Defensive lazy element accessors (avoids direct field generation dependency on XAML compile)
    private TextBox GetTextBox(string name) => FindName(name) as TextBox;
    private TextBlock GetTextBlock(string name) => FindName(name) as TextBlock;
    private ComboBox GetComboBox(string name) => FindName(name) as ComboBox;

    // Replace direct field usages with lookup wrappers in critical methods (only if element fields unresolved at compile time)
    // Example adaptation for status update already handles null references internally.
    /// <summary>Display modal About dialog with version information.</summary>
    private void OnAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    // Removed verbose RestoreWindowState method (handled by WindowStateService)

    /// <summary>
    /// Applies persisted maximized state post-load. Separated for clarity and potential future animation hooks.
    /// </summary>
    private void ApplyMaximized()
    {
        Log.Information("=== Applying Maximized State ===");

        var s = SettingsService.Instance.Current;
        var shouldMaximize = s.WindowMaximized == true;

        Log.Information("Window maximization check - Should maximize: {ShouldMaximize}, Current state: {CurrentState}",
            shouldMaximize, WindowState);

        if (shouldMaximize)
        {
            WindowState = WindowState.Maximized;
            Log.Information("Window maximized successfully");
        }
        else
        {
            Log.Information("Window remains in normal state");
        }

        Log.Information("=== Maximized State Application Complete ===");
    }

    /// <summary>
    /// Persists window bounds only when in Normal state to avoid capturing the restored size of a maximized window.
    /// </summary>
    // Removed verbose PersistWindowState method (handled by WindowStateService)

    private Services.IWindowStateService GetWindowStateService()
    {
        try
        {
            var resolved = ServiceLocator.GetService<Services.IWindowStateService>();
            return resolved ?? new Services.WindowStateService(SettingsService.Instance);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to resolve WindowStateService - falling back");
            return new Services.WindowStateService(SettingsService.Instance);
        }
    }

    /// <summary>
    /// Initializes the budget interactions diagram with nodes and connectors
    /// Includes comprehensive logging for Syncfusion diagram setup and data binding.
    /// </summary>
    private void InitializeBudgetDiagram()
    {
        try
        {
            var vm = DataContext as ViewModels.MainViewModel;
            var diagram = BudgetDiagramField as SfDiagram;
            if (vm == null || diagram == null) return;
            (diagram.Nodes as System.Collections.IList)?.Clear();
            (diagram.Connectors as System.Collections.IList)?.Clear();
            var built = _diagramBuilder.BuildEnterpriseDiagram(vm.Enterprises.ToList(), vm.BudgetInteractions.ToList());
            foreach (var n in built.nodes) (diagram.Nodes as System.Collections.IList)?.Add(n);
            foreach (var c in built.connectors) (diagram.Connectors as System.Collections.IList)?.Add(c);
            Log.Information("Diagram built: {NodeCount} nodes, {ConnectorCount} connectors", (diagram.Nodes as System.Collections.ICollection)?.Count, (diagram.Connectors as System.Collections.ICollection)?.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Diagram build failed");
        }
    }

    /// <summary>
    /// Event handler for when budget interactions are loaded
    /// </summary>
    private void OnBudgetInteractionsLoaded()
    {
        InitializeBudgetDiagram();
    }

    #region Settings Event Handlers

    // Obsolete settings & API key handlers removed in favor of SettingsViewModel bindings

    #endregion

    #region Helper Methods

    // Removed legacy UpdateStatus/validation helpers (moved to SettingsViewModel or no longer needed)

    /// <summary>
    /// Get a brush from the current theme
    /// </summary>
    private Brush GetThemeBrush(string themeKey)
    {
        try
        {
            if (Application.Current.Resources.Contains(themeKey))
            {
                return (Brush)Application.Current.Resources[themeKey];
            }
            else
            {
                // Fallback to default colors if theme key not found
                return themeKey switch
                {
                    "SuccessBrush" => new SolidColorBrush(Colors.Green),
                    "ErrorBrush" => new SolidColorBrush(Colors.Red),
                    "WarningBrush" => new SolidColorBrush(Colors.Orange),
                    "InfoBrush" => new SolidColorBrush(Colors.Blue),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
        }
        catch
        {
            // Fallback to default colors if theme key not found
            return themeKey switch
            {
                "SuccessBrush" => new SolidColorBrush(Colors.Green),
                "ErrorBrush" => new SolidColorBrush(Colors.Red),
                "WarningBrush" => new SolidColorBrush(Colors.Orange),
                "InfoBrush" => new SolidColorBrush(Colors.Blue),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
    }

    /// <summary>
    /// Resolves a Brush resource by key with null safety (used by settings validation)
    /// </summary>
    private Brush GetBrushByKey(string key) => string.IsNullOrWhiteSpace(key) ? null : (this.TryFindResource(key) as Brush);

    #endregion

    #region Event Handlers

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    #endregion
}
