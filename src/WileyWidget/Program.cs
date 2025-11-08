using System;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Application entry point with proper STA threading model for WPF applications.
/// This class provides the proper threading model and initialization sequence
/// for hosting WPF within the .NET Generic Host pattern.
///
/// Based on Microsoft Documentation:
/// - https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview
/// - https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point. The STAThreadAttribute is required for WPF applications
    /// as per Microsoft documentation: "WPF uses the single-threaded apartment (STA) threading model."
    /// </summary>
    [STAThread]
    public static int Main()
    {
        try
        {
            var app = new App();
            return app.Run();
        }
        catch (Exception ex)
        {
            // Critical: Log unhandled exceptions during shutdown
            // This prevents silent failures during app termination
            try
            {
                Serilog.Log.Fatal(ex, "Unhandled exception in Program.Main during application execution or shutdown");
            }
            catch
            {
                // If logging fails, write to console as last resort
                Console.Error.WriteLine($"FATAL: Unhandled exception: {ex}");
            }
            finally
            {
                Serilog.Log.CloseAndFlush();
            }

            // Return non-zero exit code to indicate failure
            return 1;
        }
    }
}
