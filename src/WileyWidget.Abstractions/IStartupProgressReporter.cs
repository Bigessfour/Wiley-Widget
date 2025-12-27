using System;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Represents a interface for istartupprogressreporter.
    /// </summary>
    public interface IStartupProgressReporter
    {
        void Report(double progress, string message, bool? isIndeterminate = null);
        void Complete(string? finalMessage = null);
        void AttachSplashScreen(object? splashScreen);
    }
}
