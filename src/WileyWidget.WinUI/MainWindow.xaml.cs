// MainWindow.xaml.cs - WinUI 3 Main Window with Prism Integration
//
// Ported from WPF MainWindow (November 13, 2025)
// This is the shell window that hosts Prism regions for dynamic content navigation

using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.WinUI;

/// <summary>
/// Main window that serves as the shell for the Prism-based WinUI application.
/// Contains the primary navigation region for loading views dynamically.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        Log.Information("[MAINWINDOW] Initializing main window");
        
        this.InitializeComponent();
        
        Title = "Wiley Widget - WinUI 3";
        
        // Set initial status
        UpdateStatus();
        
        Log.Information("[MAINWINDOW] Main window initialized successfully");
    }

    /// <summary>
    /// Updates the status bar with current application information.
    /// </summary>
    private void UpdateStatus()
    {
        try
        {
            // No Syncfusion license checks needed - using open-source components
            Log.Information("[MAINWINDOW] Status updated - Application ready");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MAINWINDOW] Error updating status");
        }
    }
}
