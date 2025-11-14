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
    /// Updates the status bar with current license and connection information.
    /// </summary>
    private void UpdateStatus()
    {
        try
        {
            // Update license status
            var licenseStatus = App.SyncfusionLicenseStatus switch
            {
                App.LicenseRegistrationStatus.Success => "License: ✓ Registered",
                App.LicenseRegistrationStatus.TrialMode => "License: ⚠ Trial Mode",
                App.LicenseRegistrationStatus.Failed => "License: ✗ Failed",
                App.LicenseRegistrationStatus.InvalidKey => "License: ✗ Invalid Key",
                App.LicenseRegistrationStatus.NetworkError => "License: ⚠ Network Error",
                _ => "License: Checking..."
            };
            
            LicenseStatusText.Text = licenseStatus;
            
            // Update main status
            StatusText.Text = App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.Success 
                ? "Ready" 
                : "Trial Mode";
            
            StatusBarText.Text = "Application initialized - WinUI 3 with Prism";
            
            Log.Information("[MAINWINDOW] Status updated - License: {Status}", App.SyncfusionLicenseStatus);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MAINWINDOW] Error updating status");
        }
    }
}
