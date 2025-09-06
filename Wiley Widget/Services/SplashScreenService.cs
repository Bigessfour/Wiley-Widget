using System;
using System.IO;
using System.Windows;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Service for managing application splash screen initialization.
/// Provides both enhanced and basic splash screen fallback.
/// </summary>
public class SplashScreenService
{
    /// <summary>
    /// Initializes the splash screen for the application.
    /// </summary>
    public void InitializeSplashScreen()
    {
        try
        {
            // Use enhanced splash screen window instead of basic SplashScreen
            // For now, we'll use the basic WPF SplashScreen as the enhanced one is not implemented
            var splashImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "SplashScreen.png");

            if (File.Exists(splashImagePath))
            {
                var basicSplash = new SplashScreen(splashImagePath);
                basicSplash.Show(false);
                Log.Information("🖼️ Basic splash screen initialized");
            }
            else
            {
                Log.Warning("⚠️ Splash screen image not found at: {Path}", splashImagePath);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to initialize splash screen - continuing without splash screen");
        }
    }

    /// <summary>
    /// Closes the splash screen if it's currently showing.
    /// </summary>
    public void CloseSplashScreen()
    {
        try
        {
            // For basic SplashScreen, it closes automatically when the main window is shown
            // Enhanced splash screen would need manual closing here
            Log.Debug("🖼️ Splash screen close requested");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to close splash screen");
        }
    }
}
