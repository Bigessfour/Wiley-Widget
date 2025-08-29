using System.Windows;
using Syncfusion.SfSkinManager; // Theme manager
using Syncfusion.UI.Xaml.Grid; // Added for Grid controls
using WileyWidget.Services;
using Serilog;

namespace WileyWidget;

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
    /// Initializes the main application window with comprehensive setup.
    /// 
    /// <para>Initialization sequence:</para>
    /// <list type="number">
    /// <item>Component initialization and data binding</item>
    /// <item>Theme application from persisted settings</item>
    /// <item>Dynamic column setup (if enabled)</item>
    /// <item>Window state restoration</item>
    /// <item>Event handler registration</item>
    /// <item>UI element state updates</item>
    /// </list>
    /// </summary>
    public MainWindow()
    {
        Log.Information("MainWindow initialization started");

        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();

        // Apply persisted theme or default
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        if (UseDynamicColumns)
        {
            Log.Information("Dynamic column generation enabled - building columns programmatically");
            BuildDynamicColumns();
        }

        RestoreWindowState();
        Loaded += (_, _) => ApplyMaximized();
        Closing += (_, _) => PersistWindowState();
        UpdateThemeToggleVisuals();

        Log.Information("MainWindow initialization completed successfully");
    }

    /// <summary>
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
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails (e.g., renamed or removed).
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(this, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch
        {
            if (themeName != "FluentLight")
            {
                // Fallback
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                try { SfSkinManager.SetTheme(this, new Theme("FluentLight")); } catch { /* ignore */ }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }
    }

    private string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty); // allow "Fluent Dark" legacy
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            _ => "FluentDark" // default
        };
    }

    /// <summary>Switch to Fluent Dark theme and persist choice.</summary>
    private void OnFluentDark(object sender, RoutedEventArgs e)
    {
        TryApplyTheme("FluentDark");
        SettingsService.Instance.Current.Theme = "FluentDark";
        SettingsService.Instance.Save();
        Log.Information("Theme changed to {Theme}", "Fluent Dark");
        UpdateThemeToggleVisuals();
    }
    /// <summary>Switch to Fluent Light theme and persist choice.</summary>
    private void OnFluentLight(object sender, RoutedEventArgs e)
    {
        TryApplyTheme("FluentLight");
        SettingsService.Instance.Current.Theme = "FluentLight";
        SettingsService.Instance.Save();
        Log.Information("Theme changed to {Theme}", "Fluent Light");
        UpdateThemeToggleVisuals();
    }

    private void UpdateThemeToggleVisuals()
    {
        var current = NormalizeTheme(SettingsService.Instance.Current.Theme);

        // Find ribbon buttons using FindName since they're nested in the ribbon control
        var btnFluentDark = FindName("BtnFluentDark") as System.Windows.Controls.Button;
        var btnFluentLight = FindName("BtnFluentLight") as System.Windows.Controls.Button;

        if (btnFluentDark != null)
        {
            btnFluentDark.IsEnabled = current != "FluentDark";
            btnFluentDark.Content = current == "FluentDark" ? "✔ Fluent Dark" : "Fluent Dark";
        }
        if (btnFluentLight != null)
        {
            btnFluentLight.IsEnabled = current != "FluentLight";
            btnFluentLight.Content = current == "FluentLight" ? "✔ Fluent Light" : "Fluent Light";
        }
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
        var s = SettingsService.Instance.Current;
        if (s.WindowWidth.HasValue) Width = s.WindowWidth.Value;
        if (s.WindowHeight.HasValue) Height = s.WindowHeight.Value;
        if (s.WindowLeft.HasValue) Left = s.WindowLeft.Value;
        if (s.WindowTop.HasValue) Top = s.WindowTop.Value;
    }

    /// <summary>
    /// Applies persisted maximized state post-load. Separated for clarity and potential future animation hooks.
    /// </summary>
    private void ApplyMaximized()
    {
        var s = SettingsService.Instance.Current;
        if (s.WindowMaximized == true)
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Persists window bounds only when in Normal state to avoid capturing the restored size of a maximized window.
    /// </summary>
    private void PersistWindowState()
    {
        var s = SettingsService.Instance.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;
        }
        SettingsService.Instance.Save();
    }
}
