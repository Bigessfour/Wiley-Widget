// ShellWindow.xaml.cs - Main application shell for Uno Platform

using Microsoft.UI.Xaml;

namespace WileyWidget.Uno.Views;

/// <summary>
/// Main application shell window.
/// Replaces WPF MainWindow with Uno Platform Window.
/// </summary>
public sealed partial class ShellWindow : Window
{
    public ShellWindow()
    {
        this.InitializeComponent();
        
        // Set window title
        Title = "WileyWidget";
        
        // TODO: Configure window size, position, etc.
    }
}
