using System;

namespace WileyWidget.Services
{
    public interface IStartupProgressReporter
    {
        void Report(double progress, string message, bool? isIndeterminate = null);
        void Complete(string? finalMessage = null);
        void AttachSplashScreen(object? splashScreen);
    }
}
