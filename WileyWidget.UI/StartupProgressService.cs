using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Provides a single point for reporting startup progress so the splash screen and diagnostics stay in sync.
/// Thread-safe and resilient to cases where the splash screen is not yet constructed.
/// </summary>
public interface IStartupProgressReporter
{
    void Report(double progress, string message, bool? isIndeterminate = null);
    void Complete(string? finalMessage = null);
    void AttachSplashScreen(SplashScreenWindow? splashScreen);
}

internal sealed class StartupProgressService : IStartupProgressReporter
{
    private readonly object _syncRoot = new();
    private readonly List<StartupProgressSnapshot> _pendingSnapshots = new();
    private SplashScreenWindow? _splashScreen;
    private StartupProgressSnapshot? _lastSnapshot;
    private readonly ILogger _logger = Log.ForContext<StartupProgressService>();

    public void Report(double progress, string message, bool? isIndeterminate = null)
    {
        var sanitizedProgress = Math.Clamp(progress, 0, 100);
        var trimmedMessage = string.IsNullOrWhiteSpace(message) ? "" : message.Trim();

        var snapshot = new StartupProgressSnapshot(sanitizedProgress, trimmedMessage, isIndeterminate);
        _logger.Information("Startup progress {Progress}% - {Message}", sanitizedProgress, string.IsNullOrEmpty(trimmedMessage) ? "(no message)" : trimmedMessage);

        lock (_syncRoot)
        {
            _lastSnapshot = snapshot;
            if (_splashScreen == null)
            {
                _pendingSnapshots.Add(snapshot);
                return;
            }

            DispatchSnapshot(_splashScreen, snapshot);
        }
    }

    public void Complete(string? finalMessage = null)
    {
        var message = string.IsNullOrWhiteSpace(finalMessage) ? "Application ready" : finalMessage!.Trim();
        Report(100, message, false);

        SplashScreenWindow? splash;
        lock (_syncRoot)
        {
            splash = _splashScreen;
        }

        if (splash == null)
        {
            return;
        }

        void RunComplete()
        {
            try
            {
                splash.Complete();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Splash screen completion animation failed");
            }
        }

        if (splash.Dispatcher.CheckAccess())
        {
            RunComplete();
        }
        else
        {
            _ = splash.Dispatcher.InvokeAsync(RunComplete, DispatcherPriority.Render);
        }
    }

    public void AttachSplashScreen(SplashScreenWindow? splashScreen)
    {
        lock (_syncRoot)
        {
            if (_splashScreen == splashScreen)
            {
                return;
            }

            if (_splashScreen != null)
            {
                _splashScreen.Closed -= HandleSplashClosed;
            }

            _splashScreen = splashScreen;

            if (_splashScreen != null)
            {
                _splashScreen.Closed += HandleSplashClosed;

                foreach (var snapshot in _pendingSnapshots)
                {
                    DispatchSnapshot(_splashScreen, snapshot);
                }
                _pendingSnapshots.Clear();

                if (_lastSnapshot != null)
                {
                    DispatchSnapshot(_splashScreen, _lastSnapshot.Value);
                }
            }
        }
    }

    private void HandleSplashClosed(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (_splashScreen != null)
            {
                _splashScreen.Closed -= HandleSplashClosed;
            }
            _splashScreen = null;
        }
    }

    private static void DispatchSnapshot(SplashScreenWindow splash, StartupProgressSnapshot snapshot)
    {
        void Apply()
        {
            if (snapshot.IsIndeterminate.HasValue)
            {
                splash.IsIndeterminate = snapshot.IsIndeterminate.Value;
            }

            if (!string.IsNullOrEmpty(snapshot.Message))
            {
                splash.StatusText = snapshot.Message;
            }

            splash.UpdateProgress(snapshot.Progress, snapshot.Message);
        }

        if (splash.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _ = splash.Dispatcher.InvokeAsync(Apply, DispatcherPriority.Background);
        }
    }

    private readonly record struct StartupProgressSnapshot(double Progress, string Message, bool? IsIndeterminate);
}
