using System;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Application = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests.Helpers
{
    internal static class UiTestSession
    {
        private static readonly object SyncRoot = new();
        private static SessionState? _state;

        internal static SessionState GetOrStart(UiTestSessionOptions options)
        {
            lock (SyncRoot)
            {
                if (_state == null || _state.App.HasExited || !_state.Options.Equals(options))
                {
                    _state?.Dispose();
                    _state = SessionState.Start(options);
                }

                return _state;
            }
        }

        internal static void Reset()
        {
            lock (SyncRoot)
            {
                _state?.Dispose();
                _state = null;
            }
        }

        internal sealed class SessionState : IDisposable
        {
            internal SessionState(Application app, UIA3Automation automation, UiTestSessionOptions options)
            {
                App = app;
                Automation = automation;
                Options = options;
            }

            internal Application App { get; }
            internal UIA3Automation Automation { get; }
            internal UiTestSessionOptions Options { get; }

            internal Window GetMainWindow(int timeoutSeconds = 15)
            {
                var main = Retry.WhileNull(() => App.GetMainWindow(Automation), TimeSpan.FromSeconds(timeoutSeconds));
                return main.Result ?? throw new InvalidOperationException("Main window was not found.");
            }

            public void Dispose()
            {
                try { App.Close(); } catch { }
                try { App.Dispose(); } catch { }
                try { Automation.Dispose(); } catch { }
            }

            internal static SessionState Start(UiTestSessionOptions options)
            {
                options.ApplyEnvironment();

                var exePath = options.ResolveExecutablePath();
                var app = Application.Launch(exePath);
                var automation = new UIA3Automation();

                // Wait for the main window to be responsive before returning the session
                Retry.WhileException(() =>
                {
                    var window = app.GetMainWindow(automation);
                    if (window == null || !window.IsAvailable)
                    {
                        throw new InvalidOperationException("Main window not ready");
                    }
                }, TimeSpan.FromSeconds(30));

                return new SessionState(app, automation, options);
            }
        }
    }

    internal readonly record struct UiTestSessionOptions(string ExecutablePath, bool UseMdiMode, bool UseTabbedMdi, bool UseInMemory, bool IsUiTestHarness)
    {
        private const string LicenseKey = "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceXRQR2VfUER0W0o=";

        internal static UiTestSessionOptions UiHarness(string exePath)
        {
            return new UiTestSessionOptions(exePath, UseMdiMode: false, UseTabbedMdi: false, UseInMemory: true, IsUiTestHarness: true);
        }

        internal static UiTestSessionOptions MdiHarness(string exePath)
        {
            return new UiTestSessionOptions(exePath, UseMdiMode: true, UseTabbedMdi: true, UseInMemory: true, IsUiTestHarness: true);
        }

        internal void ApplyEnvironment()
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", UseInMemory ? "true" : "false");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", IsUiTestHarness ? "true" : "false");
            Environment.SetEnvironmentVariable("UI__UseMdiMode", UseMdiMode ? "true" : "false");
            Environment.SetEnvironmentVariable("UI__UseTabbedMdi", UseTabbedMdi ? "true" : "false");
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", LicenseKey);
        }

        internal string ResolveExecutablePath()
        {
            if (!string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath))
            {
                return ExecutablePath;
            }

            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"));
            if (!Directory.Exists(baseDir))
            {
                throw new DirectoryNotFoundException($"Build output directory not found at '{baseDir}'. Build WileyWidget.WinForms or set WILEYWIDGET_EXE to a published executable.");
            }

            var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");
            if (File.Exists(standard))
            {
                return standard;
            }

            var versioned = Directory.GetDirectories(baseDir, "net9.0-windows*")
                .Select(dir => Path.Combine(dir, "WileyWidget.WinForms.exe"))
                .FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(versioned))
            {
                return versioned;
            }

            throw new FileNotFoundException($"Executable not found. Set WILEYWIDGET_EXE to a published executable or build Debug output under '{baseDir}'.");
        }
    }
}
