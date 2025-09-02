using System.Reflection;
using System.Windows;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;

namespace WileyWidget.Views;

/// <summary>
/// Modal dialog showing application version information (InformationalVersion if present; falls back to assembly version).
/// </summary>
public partial class AboutWindow : ChromelessWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        
        // Apply FluentLight theme via code (moved from XAML)
        try
        {
            // .NET 9.0 ThemeMode handling for AboutWindow
            try
            {
                var fluentThemeSettingsType = Type.GetType("Syncfusion.SfSkinManager.FluentThemeSettings, Syncfusion.SfSkinManager.WPF");
                if (fluentThemeSettingsType != null)
                {
                    var themeModeProperty = fluentThemeSettingsType.GetProperty("ThemeMode");
                    if (themeModeProperty != null)
                    {
                        themeModeProperty.SetValue(null, 0); // Light mode
                    }
                }
            }
            catch (Exception modeEx)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeMode not available: {modeEx.Message}");
            }

            using (var theme = new Theme("FluentLight"))
            {
                SfSkinManager.SetTheme(this, theme);
            }
        }
        catch (Exception ex)
        {
            // Fallback to default if theme fails
            System.Diagnostics.Debug.WriteLine($"Theme application failed: {ex.Message}");
        }
        
        var asm = Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                           ?? asm.GetName().Version?.ToString() ?? "Unknown";
        VersionText.Text = $"Version: {infoVersion}";
    }

    /// <summary>Close the dialog.</summary>
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
