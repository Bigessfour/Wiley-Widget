using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;

namespace WileyWidget.UiTests;

public class TestAppFixture : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }

    private bool _disposed;

    public TestAppFixture()
    {
        var repoRoot = GetRepoRoot();
        var exePath = Path.Combine(repoRoot, "bin", "Debug", "net9.0-windows", "WileyWidget.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"App exe not found at: {exePath}. Build the app before running UI tests.");
        }

        var startInfo = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false
        };
        startInfo.EnvironmentVariables["WILEY_WIDGET_TESTMODE"] = "1";

        App = Application.Launch(startInfo);
        Automation = new UIA3Automation();
    }

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var probe = Path.Combine(dir, "WileyWidget.csproj");
            if (File.Exists(probe)) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }
        var ws = Environment.GetEnvironmentVariable("WILEY_WIDGET_ROOT");
        if (!string.IsNullOrWhiteSpace(ws) && File.Exists(Path.Combine(ws, "WileyWidget.csproj"))) return ws;
        throw new DirectoryNotFoundException("Unable to locate repo root; set WILEY_WIDGET_ROOT env var for UI tests.");
    }

    ~TestAppFixture()
    {
        // Finalizer calls Dispose(false)
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            try
            {
                if (App != null && !App.HasExited)
                {
                    try { App.Close(); } catch { }
                    try { App.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2)); } catch { }
                    try { App.Kill(); } catch { }
                }
                try { App?.Dispose(); } catch { }
            }
            catch { /* ignore */ }

            try { Automation?.Dispose(); } catch { }
        }

        // TODO: free unmanaged resources here if any

        _disposed = true;
    }
}
