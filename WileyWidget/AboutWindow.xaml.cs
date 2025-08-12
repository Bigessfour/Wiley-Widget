using System.Reflection;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Modal dialog showing application version information (InformationalVersion if present; falls back to assembly version).
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var asm = Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                           ?? asm.GetName().Version?.ToString() ?? "Unknown";
        VersionText.Text = $"Version: {infoVersion}";
    }

    /// <summary>Close the dialog.</summary>
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
