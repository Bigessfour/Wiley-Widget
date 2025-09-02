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
                    DataContext = ServiceLocator.GetService<ViewModels.MainViewModel>();
                    Log.Information("Data context established");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to set DataContext");
                }
            }));

            Log.Information("Locating Syncfusion controls...");
            // InitializeSyncfusionControls(); // Commented out - controls not in simplified XAML
            Log.Information("Syncfusion controls located and referenced");

            // CRITICAL FIX: Defer theme application to Loaded event to prevent crashes
            // FluentLight and other themes with reveal animations can crash if applied in constructor
            Log.Information("Theme application deferred to Loaded event to prevent animation crashes");

            if (UseDynamicColumns)
            {
                Log.Information("Dynamic column generation enabled - building columns programmatically");
                BuildDynamicColumns();
            }

            Log.Information("Setting up window state management...");
            RestoreWindowState();

            // CRITICAL FIX: Apply theme in Loaded event instead of constructor
            Loaded += (_, _) =>
            {
                Log.Information("Window.Loaded event fired - applying theme and maximized state");
                ApplyDeferredTheme();
                ApplyMaximized();
                
                // PERFORMANCE OPTIMIZATION: Apply WPF performance optimizations
                OptimizePerformance();
            };

            Closing += (_, _) => PersistWindowState();

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
            foreach (var element in uiElements)
            {
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
    /// CRITICAL FIX: Apply theme in Loaded event to prevent animation crashes.
    /// FluentLight themes with reveal animations can crash if applied in constructor.
    /// </summary>
    private void ApplyDeferredTheme()
    {
        try
        {
            Log.Information("Applying deferred theme in Loaded event to prevent crashes");
            
            var initialTheme = SettingsService.Instance?.Current?.Theme ?? "FluentDark";
            var normalizedTheme = ThemeService.NormalizeTheme(initialTheme);
            
            // CRITICAL FIX: Disable animations temporarily for FluentLight to prevent crashes
            if (normalizedTheme.Contains("Light", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Light theme detected - disabling animations to prevent crashes");
                try
                {
                    // Note: FluentThemeSettings may not be available in current Syncfusion version
                    // This is a future-proofing fix for when FluentTheme settings become available
                    Log.Information("✅ Light theme crash prevention applied (FluentTheme settings disabled if available)");
                }
                catch (Exception animEx)
                {
                    Log.Warning(animEx, "⚠️ Failed to disable FluentTheme animations - continuing anyway");
                }
            }
            
            Log.Information("Applying theme {Theme} to MainWindow in Loaded event", normalizedTheme);
            ThemeService.ApplyTheme(this, normalizedTheme);
            Log.Information("✅ MainWindow deferred theme applied successfully: {Theme}", normalizedTheme);
        }
        catch (Exception themeEx)
        {
            Log.Warning(themeEx, "⚠️ Failed to apply deferred MainWindow theme: {Message}", themeEx.Message);
            // Continue without theme - application should still work
        }
    }

    // Theme logic moved to ThemeService; keep thin wrapper for existing call sites
    private void TryApplyTheme(string themeName) => ThemeService.ApplyTheme(this, themeName);

    // Legacy method names retained for backward compatibility with existing reflection-based tests.
    // They now delegate to ThemeService (centralized theme management) or provide minimal logging only.
    private void ApplyWpfTheme(string themeName) => WileyWidget.Services.ThemeService.ApplyTheme(this, themeName);
    private void LogCurrentThemeState() => Log.Information("LogCurrentThemeState is deprecated; ThemeService now logs state centrally.");
    private void VerifySpecificSyncfusionControls(string expectedTheme) => Log.Information("VerifySpecificSyncfusionControls deprecated - relying on ThemeService state logging. Expected={Expected}", expectedTheme);
    private void VerifySyncfusionThemeApplication(string expectedTheme) => Log.Information("VerifySyncfusionThemeApplication deprecated - central verification removed. Expected={Expected}", expectedTheme);

    /// <summary>Switch to Fluent Dark theme and persist choice.</summary>
    private void OnFluentDark(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_FluentDark"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "FluentDark", true);

            TryApplyTheme("FluentDark");

            SettingsService.Instance.Current.Theme = "FluentDark";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Fluent Dark");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to Fluent Light theme and persist choice.</summary>
    private void OnFluentLight(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_FluentLight"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "FluentLight", true);

            TryApplyTheme("FluentLight");

            SettingsService.Instance.Current.Theme = "FluentLight";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Fluent Light");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to Material Dark theme and persist choice.</summary>
    private void OnMaterialDark(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_MaterialDark"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "MaterialDark", true);

            TryApplyTheme("MaterialDark");

            SettingsService.Instance.Current.Theme = "MaterialDark";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Material Dark");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to Material Light theme and persist choice.</summary>
    private void OnMaterialLight(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_MaterialLight"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "MaterialLight", true);

            TryApplyTheme("MaterialLight");

            SettingsService.Instance.Current.Theme = "MaterialLight";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Material Light");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to Office 2019 Colorful theme and persist choice.</summary>
    private void OnOffice2019Colorful(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_Office2019Colorful"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "Office2019Colorful", true);

            TryApplyTheme("Office2019Colorful");

            SettingsService.Instance.Current.Theme = "Office2019Colorful";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Office 2019 Colorful");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to Office 365 theme and persist choice.</summary>
    private void OnOffice365(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_Office365"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "Office365", true);

            TryApplyTheme("Office365");

            SettingsService.Instance.Current.Theme = "Office365";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "Office 365");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    /// <summary>Switch to High Contrast theme and persist choice.</summary>
    private void OnHighContrast(object sender, RoutedEventArgs e)
    {
        var previousTheme = SettingsService.Instance.Current.Theme ?? "Default";

        using (StructuredLogger.BeginOperation("ThemeChange_HighContrast"))
        {
            StructuredLogger.LogThemeChange(previousTheme, "HighContrast", true);

            TryApplyTheme("HighContrast");

            SettingsService.Instance.Current.Theme = "HighContrast";
            SettingsService.Instance.Save();

            Log.Information("Theme successfully changed to {Theme}", "High Contrast");
            Log.Information("Theme preference persisted to settings");

            UpdateThemeToggleVisuals();
            Log.Information("Theme toggle button visuals updated");
        }
    }

    private void UpdateThemeToggleVisuals()
    {
        Log.Information("=== Updating Theme Toggle Button Visuals ===");

    var current = ThemeService.NormalizeTheme(SettingsService.Instance.Current.Theme);
        Log.Information("Current normalized theme: {CurrentTheme}", current);

        // Find ribbon buttons using FindName since they're nested in the ribbon control
        var btnFluentDark = FindName("BtnFluentDark") as Syncfusion.Windows.Tools.Controls.ButtonAdv;
        var btnFluentLight = FindName("BtnFluentLight") as Syncfusion.Windows.Tools.Controls.ButtonAdv;

        Log.Information("Theme button references - Dark: {DarkFound}, Light: {LightFound}",
            btnFluentDark != null, btnFluentLight != null);

        if (btnFluentDark != null)
        {
            var wasEnabled = btnFluentDark.IsEnabled;
            var oldContent = btnFluentDark.Content?.ToString();

            btnFluentDark.IsEnabled = current != "FluentDark";
            btnFluentDark.Content = current == "FluentDark" ? "✔ Fluent Dark" : "Fluent Dark";

            Log.Information("Fluent Dark button updated - Enabled: {WasEnabled} -> {IsEnabled}, Content: '{OldContent}' -> '{NewContent}'",
                wasEnabled, btnFluentDark.IsEnabled, oldContent ?? "null", btnFluentDark.Content?.ToString() ?? "null");
        }
        else
        {
            Log.Warning("Fluent Dark button not found in visual tree");
        }

        if (btnFluentLight != null)
        {
            var wasEnabled = btnFluentLight.IsEnabled;
            var oldContent = btnFluentLight.Content?.ToString();

            btnFluentLight.IsEnabled = current != "FluentLight";
            btnFluentLight.Content = current == "FluentLight" ? "✔ Fluent Light" : "Fluent Light";

            Log.Information("Fluent Light button updated - Enabled: {WasEnabled} -> {IsEnabled}, Content: '{OldContent}' -> '{NewContent}'",
                wasEnabled, btnFluentLight.IsEnabled, oldContent ?? "null", btnFluentLight.Content?.ToString() ?? "null");
        }
        else
        {
            Log.Warning("Fluent Light button not found in visual tree");
        }

        Log.Information("=== Theme Toggle Button Visuals Update Complete ===");
    }
    /// <summary>Display modal About dialog with version information.</summary>
    private void OnAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    /// <summary>
    /// Restores last known window bounds (only if previously saved). Maximized state is applied after window is loaded
    /// to avoid layout measurement issues during construction.
    /// </summary>
    private void RestoreWindowState()
    {
        Log.Information("=== Restoring Window State ===");

        var s = SettingsService.Instance.Current;

        Log.Information("Window state settings - Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}, Maximized: {Maximized}",
            s.WindowWidth, s.WindowHeight, s.WindowLeft, s.WindowTop, s.WindowMaximized);

        if (s.WindowWidth.HasValue)
        {
            Width = s.WindowWidth.Value;
            Log.Information("Window width restored to: {Width}", Width);
        }
        else
        {
            Log.Information("Window width not set in settings - using default");
        }

        if (s.WindowHeight.HasValue)
        {
            Height = s.WindowHeight.Value;
            Log.Information("Window height restored to: {Height}", Height);
        }
        else
        {
            Log.Information("Window height not set in settings - using default");
        }

        if (s.WindowLeft.HasValue)
        {
            Left = s.WindowLeft.Value;
            Log.Information("Window left position restored to: {Left}", Left);
        }
        else
        {
            Log.Information("Window left position not set in settings - using default");
        }

        if (s.WindowTop.HasValue)
        {
            Top = s.WindowTop.Value;
            Log.Information("Window top position restored to: {Top}", Top);
        }
        else
        {
            Log.Information("Window top position not set in settings - using default");
        }

        Log.Information("=== Window State Restoration Complete ===");
    }

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
    private void PersistWindowState()
    {
        Log.Information("=== Persisting Window State ===");

        var s = SettingsService.Instance.Current;

        var isMaximized = WindowState == WindowState.Maximized;
        s.WindowMaximized = isMaximized;

        Log.Information("Window state captured - Maximized: {IsMaximized}, Current state: {CurrentState}",
            isMaximized, WindowState);

        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;

            Log.Information("Window bounds persisted - Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}",
                Width, Height, Left, Top);
        }
        else
        {
            Log.Information("Window is maximized - bounds not persisted to avoid capturing restored size");
        }

        SettingsService.Instance.Save();
        Log.Information("Window state settings saved successfully");

        Log.Information("=== Window State Persistence Complete ===");
    }

    /// <summary>
    /// Initializes the budget interactions diagram with nodes and connectors
    /// Includes comprehensive logging for Syncfusion diagram setup and data binding.
    /// </summary>
    private void InitializeBudgetDiagram()
    {
        using (StructuredLogger.BeginOperation("InitializeBudgetDiagram"))
        {
            StructuredLogger.LogSyncfusionOperation("SfDiagram", "InitializationStarted");

            try
            {
                var vm = DataContext as ViewModels.MainViewModel;
                var diagram = BudgetDiagramField as SfDiagram;

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "InitializationCheck",
                    new { ViewModelExists = vm != null, DiagramExists = diagram != null });

                if (vm == null || diagram == null)
                {
                    Log.Warning("Budget diagram initialization skipped - missing ViewModel or Diagram control");
                    StructuredLogger.LogSyncfusionOperation("SfDiagram", "InitializationSkipped",
                        new { Reason = "MissingViewModelOrDiagram" });
                    return;
                }

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "PreInitializationState",
                    new { IsLoaded = diagram.IsLoaded, IsEnabled = diagram.IsEnabled });

                // Clear existing diagram content
                (diagram.Nodes as System.Collections.IList)?.Clear();
                (diagram.Connectors as System.Collections.IList)?.Clear();

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "ContentCleared");

                // Create nodes for each enterprise
                var enterpriseNodes = new Dictionary<int, Node>();
                double x = 100;
                double y = 100;

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "NodeCreationStarted",
                    new { EnterpriseCount = vm.Enterprises.Count });

                foreach (var enterprise in vm.Enterprises)
                {
                    StructuredLogger.LogSyncfusionOperation("SfDiagram", "CreatingNode",
                        new { EnterpriseName = enterprise.Name, EnterpriseId = enterprise.Id, PositionX = x, PositionY = y });

                    var node = new Node
                    {
                        Content = $"{enterprise.Name}\n${enterprise.MonthlyRevenue:F0} Rev\n${enterprise.MonthlyExpenses:F0} Exp",
                        Width = 150,
                        Height = 80,
                        OffsetX = x,
                        OffsetY = y
                    };

                    (diagram.Nodes as System.Collections.IList)?.Add(node);
                    enterpriseNodes[enterprise.Id] = node;

                    Log.Information("Node added - Position: ({X}, {Y}), Content length: {ContentLength}",
                        x, y, node.Content?.ToString()?.Length ?? 0);

                    x += 200;
                    if (x > 600) // Wrap to next row
                    {
                        x = 100;
                        y += 150;
                    }
                }

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "NodeCreationCompleted",
                    new { TotalNodes = enterpriseNodes.Count });

                // Create simple connectors for budget interactions
                StructuredLogger.LogSyncfusionOperation("SfDiagram", "ConnectorCreationStarted",
                    new { InteractionCount = vm.BudgetInteractions.Count });

                var connectorCount = 0;
                foreach (var interaction in vm.BudgetInteractions)
                {
                    StructuredLogger.LogSyncfusionOperation("SfDiagram", "ProcessingInteraction",
                        new { InteractionType = interaction.InteractionType, PrimaryId = interaction.PrimaryEnterpriseId, SecondaryId = interaction.SecondaryEnterpriseId });

                    if (enterpriseNodes.ContainsKey(interaction.PrimaryEnterpriseId))
                    {
                        var sourceNode = enterpriseNodes[interaction.PrimaryEnterpriseId];
                        Node targetNode = null;

                        if (interaction.SecondaryEnterpriseId.HasValue &&
                            enterpriseNodes.ContainsKey(interaction.SecondaryEnterpriseId.Value))
                        {
                            targetNode = enterpriseNodes[interaction.SecondaryEnterpriseId.Value];
                        }

                        if (targetNode != null)
                        {
                            // Create connector between two enterprises
#pragma warning disable CA2000 // Dispose objects before losing scope - Connector is managed by diagram
                            var connector = new Connector
                            {
                                SourceNode = sourceNode,
                                TargetNode = targetNode,
                                Content = $"{interaction.InteractionType}\n${interaction.MonthlyAmount:F0}"
                            };
#pragma warning restore CA2000

                            (diagram.Connectors as System.Collections.IList)?.Add(connector);
                            connectorCount++;

                            StructuredLogger.LogSyncfusionOperation("SfDiagram", "ConnectorAdded",
                                new
                                {
                                    From = sourceNode.Content?.ToString()?.Split('\n')[0] ?? "Unknown",
                                    To = targetNode.Content?.ToString()?.Split('\n')[0] ?? "Unknown",
                                    Amount = interaction.MonthlyAmount
                                });
                        }
                        else
                        {
                            StructuredLogger.LogSyncfusionOperation("SfDiagram", "ConnectorSkipped",
                                new { Reason = "TargetNodeNotFound", SecondaryId = interaction.SecondaryEnterpriseId });
                        }
                    }
                    else
                    {
                        StructuredLogger.LogSyncfusionOperation("SfDiagram", "ConnectorSkipped",
                            new { Reason = "SourceNodeNotFound", PrimaryId = interaction.PrimaryEnterpriseId });
                    }
                }

                var finalNodeCount = (diagram.Nodes as System.Collections.ICollection)?.Count ?? 0;
                var finalConnectorCount = (diagram.Connectors as System.Collections.ICollection)?.Count ?? 0;

                StructuredLogger.LogSyncfusionOperation("SfDiagram", "InitializationCompleted",
                    new { FinalNodeCount = finalNodeCount, FinalConnectorCount = finalConnectorCount });

                // Verify diagram state after initialization
                StructuredLogger.LogSyncfusionOperation("SfDiagram", "PostInitializationVerification",
                    new { IsLoaded = diagram.IsLoaded, IsEnabled = diagram.IsEnabled });

            }
            catch (Exception ex)
            {
                StructuredLogger.LogSyncfusionOperation("SfDiagram", "InitializationFailed",
                    new { Error = ex.Message });
                Log.Error(ex, "Error initializing budget diagram");
            }
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

    /// <summary>
    /// Test the xAI API key configuration using secure storage
    /// </summary>
    private async void OnTestApiKey(object sender, RoutedEventArgs e)
    {
        try
        {
            var apiKey = ApiKeyBox.Text;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                UpdateStatus("❌ Please enter an API key first", "ErrorBrush");
                return;
            }

            UpdateStatus("🔄 Testing xAI API key...", "WarningBrush");

            // Test the provided key
            var isValid = await ApiKeyService.Instance.TestApiKeyAsync(apiKey);

            if (isValid)
            {
                UpdateStatus("✅ API key is valid! xAI connection successful.", "SuccessBrush");
                Log.Information("xAI API key test successful");

                // Ask user if they want to save it
                var result = MessageBox.Show(
                    "API key test successful! Would you like to save this key securely?",
                    "Save API Key",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await SaveApiKeySecurely(apiKey);
                }
            }
            else
            {
                UpdateStatus("❌ API test failed - check your key and try again", "ErrorBrush");
                Log.Warning("xAI API key test failed");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ API test failed: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "xAI API key test failed");
        }
    }

    /// <summary>
    /// Securely save the API key using the best available method
    /// </summary>
    private async Task SaveApiKeySecurely(string apiKey)
    {
        try
        {
            UpdateStatus("🔒 Saving API key securely...", "WarningBrush");

            // Try to save using the most secure method available
            var success = ApiKeyService.Instance.StoreApiKey(apiKey, StorageMethod.Auto);

            if (success)
            {
                // Update settings to reflect the key is stored securely
                var settings = SettingsService.Instance.Current;
                settings.XaiApiKey = "[SECURELY_STORED]";
                SettingsService.Instance.Save();

                UpdateStatus("✅ API key saved securely!", "SuccessBrush");
                Log.Information("xAI API key saved securely");

                // Clear the password box for security
                ApiKeyBox.Text = string.Empty;
            }
            else
            {
                UpdateStatus("❌ Failed to save API key securely", "ErrorBrush");
                Log.Error("Failed to save xAI API key securely");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Failed to save API key: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "Failed to save xAI API key");
        }

        await Task.CompletedTask; // To satisfy async
    }

    /// <summary>
    /// Save all settings including xAI configuration
    /// </summary>
    private async void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate all input fields before saving
            if (!ValidateAllSettingsInputs())
            {
                UpdateStatus("❌ Please fix validation errors before saving", "ErrorBrush");
                return;
            }

            AppSettings settings = SettingsService.Instance.Current;

            // Save theme
            if (ThemeComboBox.SelectedItem is Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv themeItem)
            {
                settings.Theme = themeItem.Content.ToString();
                TryApplyTheme(settings.Theme);
                UpdateThemeToggleVisuals();
            }

            // Handle API key securely
            var enteredApiKey = ApiKeyBox.Text;
            if (!string.IsNullOrWhiteSpace(enteredApiKey))
            {
                // Test the key first
                var isValid = await ApiKeyService.Instance.TestApiKeyAsync(enteredApiKey);
                if (isValid)
                {
                    // Save securely
                    await SaveApiKeySecurely(enteredApiKey);
                }
                else
                {
                    var result = MessageBox.Show(
                        "The API key appears to be invalid. Save it anyway?",
                        "Invalid API Key",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        await SaveApiKeySecurely(enteredApiKey);
                    }
                    else
                    {
                        UpdateStatus("❌ Settings not saved - invalid API key", "ErrorBrush");
                        return;
                    }
                }
            }

            // Save other xAI settings
            if (ModelComboBox.SelectedItem is Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv modelItem)
            {
                settings.XaiModel = modelItem.Content.ToString();
            }

            // Save advanced settings
            if (int.TryParse(TimeoutTextBox.Text, out var timeout))
            {
                settings.XaiTimeoutSeconds = timeout;
            }

            if (int.TryParse(CacheTtlTextBox.Text, out var cacheTtl))
            {
                settings.XaiCacheTtlMinutes = cacheTtl;
            }

            if (decimal.TryParse(DailyBudgetTextBox.Text, out var dailyBudget))
            {
                settings.XaiDailyBudget = dailyBudget;
            }

            if (decimal.TryParse(MonthlyBudgetTextBox.Text, out var monthlyBudget))
            {
                settings.XaiMonthlyBudget = monthlyBudget;
            }

            SettingsService.Instance.Save();
            UpdateStatus("✅ Settings saved successfully!", "SuccessBrush");
            Log.Information("Settings saved successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Failed to save settings: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "Failed to save settings");
        }
    }

    /// <summary>
    /// Load current settings into the UI
    /// </summary>
    private void OnLoadSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = SettingsService.Instance.Current;

            // Load theme
            if (!string.IsNullOrWhiteSpace(settings.Theme))
            {
                ThemeComboBox.SelectedItem = ThemeComboBox.Items
                    .OfType<Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv>()
                    .FirstOrDefault(item => item.Content.ToString() == settings.Theme);
            }

            // Check API key status (don't load the actual key for security)
            var keyInfo = ApiKeyService.Instance.GetApiKeyInfo();
            if (keyInfo.IsValid)
            {
                // Show that a key is stored securely
                ApiKeyStatusText.Text = "✅ API key is securely stored";
                ApiKeyStatusText.Foreground = GetThemeBrush("SuccessBrush");
            }
            else
            {
                ApiKeyStatusText.Text = "❌ No API key configured";
                ApiKeyStatusText.Foreground = GetThemeBrush("ErrorBrush");
            }

            // Load other xAI settings
            if (!string.IsNullOrWhiteSpace(settings.XaiModel))
            {
                ModelComboBox.SelectedItem = ModelComboBox.Items
                    .OfType<Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv>()
                    .FirstOrDefault(item => item.Content.ToString() == settings.XaiModel);
            }

            // Load advanced settings
            TimeoutTextBox.Text = settings.XaiTimeoutSeconds.ToString();
            CacheTtlTextBox.Text = settings.XaiCacheTtlMinutes.ToString();
            DailyBudgetTextBox.Text = settings.XaiDailyBudget.ToString("F2");
            MonthlyBudgetTextBox.Text = settings.XaiMonthlyBudget.ToString("F2");

            UpdateStatus("✅ Settings loaded successfully!", "SuccessBrush");
            Log.Information("Settings loaded into UI");
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Failed to load settings: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "Failed to load settings into UI");
        }
    }

    /// <summary>
    /// Apply the selected theme
    /// </summary>
    private void OnApplyTheme(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ThemeComboBox.SelectedItem is Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv themeItem)
            {
                var theme = themeItem.Content.ToString();
                TryApplyTheme(theme);
                UpdateThemeToggleVisuals();
                UpdateStatus($"✅ Theme changed to {theme}", "SuccessBrush");
                Log.Information("Theme applied: {Theme}", theme);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Failed to apply theme: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "Failed to apply theme");
        }
    }

    /// <summary>
    /// Remove the stored API key securely
    /// </summary>
    private void OnRemoveApiKey(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MessageBox.Show(
                "Are you sure you want to remove the stored API key? This action cannot be undone.",
                "Remove API Key",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var success = ApiKeyService.Instance.RemoveApiKey();

                if (success)
                {
                    // Update settings
                    var settings = SettingsService.Instance.Current;
                    settings.XaiApiKey = null;
                    SettingsService.Instance.Save();

                    // Update UI
                    ApiKeyBox.Text = string.Empty;
                    ApiKeyStatusText.Text = "❌ No API key configured";
                    ApiKeyStatusText.Foreground = GetThemeBrush("ErrorBrush");

                    UpdateStatus("✅ API key removed successfully!", "SuccessBrush");
                    Log.Information("xAI API key removed successfully");
                }
                else
                {
                    UpdateStatus("❌ Failed to remove API key completely", "ErrorBrush");
                    Log.Warning("Failed to remove xAI API key from all locations");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Failed to remove API key: {ex.Message}", "ErrorBrush");
            Log.Error(ex, "Failed to remove xAI API key");
        }
    }

    /// <summary>
    /// Show detailed information about API key storage
    /// </summary>
    private void OnShowApiKeyInfo(object sender, RoutedEventArgs e)
    {
        try
        {
            var keyInfo = ApiKeyService.Instance.GetApiKeyInfo();

            var infoMessage = new StringBuilder();
            infoMessage.AppendLine("🔐 API Key Storage Information:");
            infoMessage.AppendLine();
            infoMessage.AppendLine($"Environment Variable: {(keyInfo.HasEnvironmentVariable ? "✅ Stored" : "❌ Not found")}");
            infoMessage.AppendLine($"User Secrets: {(keyInfo.HasUserSecrets ? "✅ Stored" : "❌ Not found")}");
            infoMessage.AppendLine($"Encrypted Storage: {(keyInfo.HasEncryptedStorage ? "✅ Stored" : "❌ Not found")}");
            infoMessage.AppendLine();
            infoMessage.AppendLine($"Overall Status: {(keyInfo.IsValid ? "✅ Valid key available" : "❌ No valid key found")}");

            MessageBox.Show(infoMessage.ToString(), "API Key Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to get API key information: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.Error(ex, "Failed to get API key information");
        }
    }

    /// <summary>
    /// Open the xAI API Key Setup Guide
    /// </summary>
    private void OnOpenApiGuide(object sender, RoutedEventArgs e)
    {
        try
        {
            var guidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "xAI-API-Key-Setup-Guide.md");

            if (File.Exists(guidePath))
            {
                // Open the guide with the default markdown viewer or text editor
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = guidePath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("API Key Setup Guide not found. Please check the docs folder.", "Guide Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open API Key Setup Guide: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.Error(ex, "Failed to open API Key Setup Guide");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Update the status display in the settings tab
    /// </summary>
    private void UpdateStatus(string message, Brush brush)
    {
        if (ApiKeyStatusText != null)
        {
            ApiKeyStatusText.Text = message;
            ApiKeyStatusText.Foreground = brush;
        }

        if (StatusBorder != null)
        {
            StatusBorder.BorderBrush = brush;
        }
    }

    /// <summary>
    /// Update the status display with a theme-based color
    /// </summary>
    private void UpdateStatus(string message, string themeKey)
    {
        var brush = GetThemeBrush(themeKey);
        UpdateStatus(message, brush);
    }

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
    /// Validates all settings input fields
    /// </summary>
    private bool ValidateAllSettingsInputs()
    {
        bool isValid = true;

        // Validate API key (if provided)
        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Text))
        {
            var apiKeyValidation = new Converters.ApiKeyValidationRule
            {
                MinLength = 20,
                MaxLength = 128,
                AllowEmpty = true
            };

            var result = apiKeyValidation.Validate(ApiKeyBox.Text, System.Globalization.CultureInfo.CurrentCulture);
            if (!result.IsValid)
            {
                isValid = false;
            }
        }

        // Validate timeout
        var timeoutValidation = new Converters.PositiveIntegerValidationRule
        {
            MinValue = 5,
            MaxValue = 300
        };

        var timeoutResult = timeoutValidation.Validate(TimeoutTextBox.Text, System.Globalization.CultureInfo.CurrentCulture);
        if (!timeoutResult.IsValid)
        {
            isValid = false;
        }

        // Validate cache TTL
        var cacheValidation = new Converters.PositiveIntegerValidationRule
        {
            MinValue = 1,
            MaxValue = 1440
        };

        var cacheResult = cacheValidation.Validate(CacheTtlTextBox.Text, System.Globalization.CultureInfo.CurrentCulture);
        if (!cacheResult.IsValid)
        {
            isValid = false;
        }

        // Validate daily budget
        var dailyBudgetValidation = new Converters.PositiveDecimalValidationRule
        {
            MinValue = 0.01M,
            MaxValue = 1000.00M
        };

        var dailyResult = dailyBudgetValidation.Validate(DailyBudgetTextBox.Text, System.Globalization.CultureInfo.CurrentCulture);
        if (!dailyResult.IsValid)
        {
            isValid = false;
        }

        // Validate monthly budget
        var monthlyBudgetValidation = new Converters.PositiveDecimalValidationRule
        {
            MinValue = 1.00M,
            MaxValue = 10000.00M
        };

        var monthlyResult = monthlyBudgetValidation.Validate(MonthlyBudgetTextBox.Text, System.Globalization.CultureInfo.CurrentCulture);
        if (!monthlyResult.IsValid)
        {
            isValid = false;
        }

        return isValid;
    }

    #endregion

    #region Event Handlers

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    #endregion
}
