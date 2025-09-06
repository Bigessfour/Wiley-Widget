using System.Collections.Generic;
using Serilog.Context;
using Serilog;
using WileyWidget.Views;

namespace WileyWidget.Infrastructure.Bootstrap;

/// <summary>
/// Startup progress tracker for splash screen and user feedback.
/// Provides progress indication during application initialization.
/// </summary>
public class StartupProgressTracker
{
    private int _currentStep;
    private readonly int _totalSteps;
    private readonly List<string> _steps;
    private readonly SplashScreenWindow _splashScreen;

    public StartupProgressTracker(SplashScreenWindow splashScreen = null)
    {
        _splashScreen = splashScreen;
        _steps = new List<string>
        {
            "Loading configuration...",
            "Configuring logging...",
            "Registering licenses...",
            "Initializing components...",
            "Loading user settings...",
            "Starting application..."
        };
        _totalSteps = _steps.Count;
    }

    public void AdvanceStep()
    {
        _currentStep++;
        var progress = (double)_currentStep / _totalSteps * 100;
        var statusText = _currentStep > 0 && _currentStep <= _steps.Count ? _steps[_currentStep - 1] : "Complete";

        // Update splash screen if available
        _splashScreen?.UpdateProgress(progress, statusText);

        using (LogContext.PushProperty("StartupProgress", progress))
        using (LogContext.PushProperty("CurrentStep", _currentStep))
        using (LogContext.PushProperty("TotalSteps", _totalSteps))
        {
            Log.Information("🚀 Startup Progress: {CurrentStep}/{TotalSteps} ({StartupProgress:F1}%) - {_steps[_currentStep - 1]}", _currentStep, _totalSteps, progress);
        }
    }

    public double GetProgress() => (double)_currentStep / _totalSteps * 100;
    public string GetCurrentStepText() => _currentStep > 0 && _currentStep <= _steps.Count ? _steps[_currentStep - 1] : "Complete";
}
