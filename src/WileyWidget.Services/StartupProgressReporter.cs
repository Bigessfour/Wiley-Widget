using System;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// Reports startup progress to UI elements like splash screens or progress indicators.
/// Implements IStartupProgressReporter for loose coupling with UI framework.
/// </summary>
public class StartupProgressReporter : IStartupProgressReporter
{
    private object? _splashScreen;
    private double _currentProgress = 0;

    public void Report(double progress, string message, bool? isIndeterminate = null)
    {
        _currentProgress = progress;

        // Update UI element if attached
        // Example for WinForms ProgressBar:
        // if (_splashScreen is Form splash && splash.Controls["progressBar"] is ProgressBar pb)
        // {
        //     if (isIndeterminate == true)
        //         pb.Style = ProgressBarStyle.Marquee;
        //     else
        //     {
        //         pb.Style = ProgressBarStyle.Blocks;
        //         pb.Value = (int)Math.Min(progress, 100);
        //     }
        //     if (splash.Controls["lblMessage"] is Label lbl)
        //         lbl.Text = message;
        // }

        // Fallback: console logging for testing/debugging
        var progressType = isIndeterminate == true ? "INDETERMINATE" : $"{progress:F1}%";
        Console.WriteLine($"[Startup Progress] {progressType} - {message}");
    }

    public void Complete(string? finalMessage = null)
    {
        Report(100, finalMessage ?? "Application Ready", false);

        // Close or hide splash screen if attached
        if (_splashScreen != null)
        {
            // Example for WinForms:
            // if (_splashScreen is Form splash)
            // {
            //     splash.Invoke(new Action(() => splash.Close()));
            // }

            Console.WriteLine("[Startup Progress] Splash screen closed");
        }
    }

    public void AttachSplashScreen(object? splashScreen)
    {
        _splashScreen = splashScreen;

        if (splashScreen != null)
        {
            Console.WriteLine($"[Startup Progress] Attached splash screen: {splashScreen.GetType().Name}");
        }
    }
}
