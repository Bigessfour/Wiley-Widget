using System.Windows;
using Syncfusion.SfSkinManager; // Theme manager
using Syncfusion.Windows.Shared; // Theme names (if needed)
using WileyWidget.Services;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Primary shell window: applies persisted theme + window geometry, wires basic commands (theme switch, about),
/// and persists size/state on close. Keeps logic minimal—heavy operations belong in view models/services.
/// </summary>
public partial class MainWindow : Window
{
    // Runtime toggle for dynamic column generation (kept non-const to avoid CS0162 unreachable warning when false).
    private static readonly bool UseDynamicColumns = false; // set true to enable runtime column build

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();
        // Apply persisted theme or default
        TryApplyTheme(SettingsService.Instance.Current.Theme);
        if (UseDynamicColumns)
            BuildDynamicColumns();
        RestoreWindowState();
        Loaded += (_, _) => ApplyMaximized();
        Closing += (_, _) => PersistWindowState();
    UpdateThemeToggleVisuals();
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
            Grid.AutoGenerateColumns = false;
            Grid.Columns.Clear();
            var type = items[0].GetType();
            foreach (var prop in type.GetProperties())
            {
                // Basic text columns for simplicity; extend mapping for numeric/date types as needed.
                Grid.Columns.Add(new Syncfusion.UI.Xaml.Grid.GridTextColumn
                {
                    MappingName = prop.Name,
                    HeaderText = prop.Name
                });
            }
        }
        catch { /* swallow for demo */ }
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
        if (BtnFluentDark != null)
        {
            BtnFluentDark.IsEnabled = current != "FluentDark";
            BtnFluentDark.Label = current == "FluentDark" ? "✔ Fluent Dark" : "Fluent Dark";
        }
        if (BtnFluentLight != null)
        {
            BtnFluentLight.IsEnabled = current != "FluentLight";
            BtnFluentLight.Label = current == "FluentLight" ? "✔ Fluent Light" : "Fluent Light";
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
