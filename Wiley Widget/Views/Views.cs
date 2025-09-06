using System.Windows;
using System.Windows.Controls;

namespace WileyWidget.Views;

/// <summary>
/// Dashboard view.
/// </summary>
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Component initialization logic
    }
}

/// <summary>
/// Settings view.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Component initialization logic
    }
}

/// <summary>
/// Splash screen window for startup progress.
/// </summary>
public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Component initialization logic
    }

    public void UpdateProgress(double progress, string statusText)
    {
        // Update progress display
    }
}
