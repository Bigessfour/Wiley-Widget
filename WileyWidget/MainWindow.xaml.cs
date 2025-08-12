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
    public MainWindow()
    {
        InitializeComponent();
    DataContext = new ViewModels.MainViewModel();
    // Apply persisted theme or default
    TryApplyTheme(SettingsService.Instance.Current.Theme);
    RestoreWindowState();
    Loaded += (_, _) => ApplyMaximized();
    Closing += (_, _) => PersistWindowState();
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails (e.g., renamed or removed).
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
            SfSkinManager.SetTheme(this, new Theme(themeName));
        }
        catch
        {
            if (themeName != "Fluent Light")
            {
                // Fallback
                try { SfSkinManager.SetTheme(this, new Theme("Fluent Light")); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>Switch to Fluent Dark theme and persist choice.</summary>
    private void OnFluentDark(object sender, RoutedEventArgs e)
    {
        TryApplyTheme("Fluent Dark");
        SettingsService.Instance.Current.Theme = "Fluent Dark";
        SettingsService.Instance.Save();
    Log.Information("Theme changed to {Theme}", "Fluent Dark");
    }
    /// <summary>Switch to Fluent Light theme and persist choice.</summary>
    private void OnFluentLight(object sender, RoutedEventArgs e)
    {
        TryApplyTheme("Fluent Light");
        SettingsService.Instance.Current.Theme = "Fluent Light";
        SettingsService.Instance.Save();
    Log.Information("Theme changed to {Theme}", "Fluent Light");
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
