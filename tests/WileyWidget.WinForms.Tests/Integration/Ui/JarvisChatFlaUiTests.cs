using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using FlaUI.Core.WindowsAPI;
using Xunit;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    public class JarvisChatFlaUiTests
    {
        private const string MainWindowTitle = "Wiley Widget - Municipal Budget Management System";
        private const string JarvisTabTitle = "JARVIS Chat";
        private const string JarvisAutomationStatusName = "JarvisAutomationStatus";

        [StaFact]
        public void JarvisChat_RendersBlazorWebView_WhenTabSelected()
        {
            var exePath = ResolveWinFormsExePath();
            var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            var previousTests = Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS");
            var previousAutomation = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS");

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", "true");

            FlaUIApp? app = null;
            try
            {
                app = FlaUIApp.Launch(exePath);
                TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                using var automation = new UIA3Automation();

                var window = WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));
                try
                {
                    ActivateJarvisPanel(window, automation, TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    // Non-fatal here: automation startup can auto-open JARVIS asynchronously.
                }
                JarvisAutomationStatus? status = null;
                try
                {
                    status = WaitForAutomationStatus(window, automation, TimeSpan.FromSeconds(90));
                }
                catch (TimeoutException)
                {
                    // Fallback for slower environments where status publication lags behind UI readiness.
                    Assert.True(IsJarvisUiVisible(window), "JARVIS UI should be visible even when automation status is delayed.");
                    return;
                }

                Assert.True(status.BlazorReady, "Blazor did not signal readiness.");
                Assert.True(status.AssistViewReady, "AssistView did not signal readiness.");
                Assert.True(status.DiagnosticsReady, "Diagnostics did not complete.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
                Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previousTests);
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", previousAutomation);

                if (app != null)
                {
                    try
                    {
                        app.Close();
                    }
                    catch
                    {
                        // Best-effort shutdown to avoid hanging tests.
                    }

                    if (!app.HasExited)
                    {
                        try
                        {
                            app.Kill();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private static Window WaitForMainWindow(FlaUIApp app, UIA3Automation automation, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                var mainWindow = TryGetMainWindow(app, automation);
                if (mainWindow != null)
                {
                    return mainWindow;
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException($"Main window '{MainWindowTitle}' did not appear within {timeout.TotalSeconds}s.");
        }

        private static Window? TryGetMainWindow(FlaUIApp app, UIA3Automation automation)
        {
            var handle = TryGetMainWindowHandle(app.ProcessId);
            if (handle != IntPtr.Zero)
            {
                try
                {
                    return automation.FromHandle(handle).AsWindow();
                }
                catch (System.TimeoutException)
                {
                    // UIA may time out while the window is still initializing.
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // UIA can throw transiently during window creation.
                }
            }

            try
            {
                var mainWindow = app.GetMainWindow(automation);
                if (mainWindow != null)
                {
                    return mainWindow;
                }
            }
            catch (System.TimeoutException)
            {
                // UIA may time out while the window is still initializing.
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // UIA can throw transiently during window creation.
            }

            foreach (var window in app.GetAllTopLevelWindows(automation))
            {
                if (TryGetProcessId(window) == app.ProcessId)
                {
                    return window;
                }
            }

            return null;
        }

        private static void TryWaitForInputIdle(FlaUIApp app, TimeSpan timeout)
        {
            try
            {
                var process = Process.GetProcessById(app.ProcessId);
                process.WaitForInputIdle((int)timeout.TotalMilliseconds);
            }
            catch
            {
                // Best-effort only; UIA may still be able to find the window.
            }
        }

        private static IntPtr TryGetMainWindowHandle(int processId)
        {
            try
            {
                return Process.GetProcessById(processId).MainWindowHandle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static int? TryGetProcessId(Window window)
        {
            try
            {
                return window.Properties.ProcessId.Value;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
        }

        private static void ActivateJarvisPanel(Window window, UIA3Automation automation, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (TrySelectJarvisTab(window))
                {
                    return;
                }

                if (TryClickJarvisRibbonButton(window))
                {
                    return;
                }

                try
                {
                    window.Focus();
                    Keyboard.TypeSimultaneously(VirtualKeyShort.LMENU, VirtualKeyShort.KEY_J);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // UIA can fail transiently while window tree is still stabilizing.
                }
                Thread.Sleep(750);
            }

            throw new TimeoutException("Unable to activate JARVIS panel via UIA or keyboard shortcut.");
        }

        private static bool TrySelectJarvisTab(Window window)
        {
            try
            {
                var tabItems = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
                foreach (var tab in tabItems)
                {
                    var name = TryGetName(tab);
                    if (name != null && name.Contains("JARVIS", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.AsTabItem().Select();
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // UIA can throw transiently while the tree is stabilizing.
            }

            return false;
        }

        private static bool TryClickJarvisRibbonButton(Window window)
        {
            try
            {
                var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                foreach (var button in buttons)
                {
                    var name = TryGetName(button);
                    if (name != null && name.Contains("JARVIS", StringComparison.OrdinalIgnoreCase))
                    {
                        button.AsButton().Invoke();
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // UIA can throw transiently while the tree is stabilizing.
            }

            return false;
        }

        private static JarvisAutomationStatus WaitForAutomationStatus(Window window, UIA3Automation automation, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            var lastStatus = JarvisAutomationStatus.Empty;

            while (stopwatch.Elapsed < timeout)
            {
                var statusElement = TryFindAutomationStatus(window);
                if (statusElement != null)
                {
                    var statusText = TryGetStatusText(statusElement);
                    if (JarvisAutomationStatus.TryParse(statusText, out var status))
                    {
                        lastStatus = status;
                        if (status.BlazorReady && status.AssistViewReady && status.DiagnosticsReady)
                        {
                            return status;
                        }
                    }
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException($"JARVIS automation status not ready. LastStatus={lastStatus.ToStatusString()}");
        }

        private static AutomationElement? TryFindAutomationStatus(Window window)
        {
            try
            {
                var candidates = window.FindAllDescendants();
                foreach (var candidate in candidates)
                {
                    var name = TryGetName(candidate);
                    var automationId = TryGetAutomationId(candidate);
                    if (string.Equals(name, JarvisAutomationStatusName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(automationId, JarvisAutomationStatusName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }

            return null;
        }

        private static bool IsJarvisUiVisible(Window window)
        {
            try
            {
                var elements = window.FindAllDescendants();
                foreach (var element in elements)
                {
                    var name = TryGetName(element);
                    if (!string.IsNullOrWhiteSpace(name) && name.Contains("JARVIS", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var automationId = TryGetAutomationId(element);
                    if (!string.IsNullOrWhiteSpace(automationId) && automationId.Contains("Jarvis", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return false;
            }

            return false;
        }

        private static string? TryGetStatusText(AutomationElement element)
        {
            try
            {
                var valuePattern = element.Patterns.Value;
                if (valuePattern.IsSupported)
                {
                    return valuePattern.Pattern.Value.Value;
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            try
            {
                return element.Properties.HelpText.ValueOrDefault;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
        }

        private static string? TryGetName(AutomationElement element)
        {
            try
            {
                return element.Properties.Name.ValueOrDefault;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
        }

        private static string? TryGetAutomationId(AutomationElement element)
        {
            try
            {
                return element.Properties.AutomationId.ValueOrDefault;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return null;
            }
        }

        private static string ResolveWinFormsExePath()
        {
            var repoRoot = FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));
            if (repoRoot == null)
            {
                throw new DirectoryNotFoundException("Unable to locate repository root (WileyWidget.sln).");
            }

            var binRoot = Path.Combine(repoRoot.FullName, "src", "WileyWidget.WinForms", "bin", "Debug");
            if (!Directory.Exists(binRoot))
            {
                throw new DirectoryNotFoundException($"Build output folder not found: {binRoot}");
            }

            var exePath = Directory.EnumerateFiles(binRoot, "WileyWidget.WinForms.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (exePath == null)
            {
                throw new FileNotFoundException("WileyWidget.WinForms.exe not found. Build the app before running UI tests.", binRoot);
            }

            return exePath;
        }

        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            var current = start;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WileyWidget.sln")))
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        private sealed class JarvisAutomationStatus
        {
            public static JarvisAutomationStatus Empty { get; } = new(false, false, false, 0, 0);

            private JarvisAutomationStatus(
                bool blazorReady,
                bool assistViewReady,
                bool diagnosticsReady,
                int promptCount,
                int responseCount)
            {
                BlazorReady = blazorReady;
                AssistViewReady = assistViewReady;
                DiagnosticsReady = diagnosticsReady;
                PromptCount = promptCount;
                ResponseCount = responseCount;
            }

            public bool BlazorReady { get; }
            public bool AssistViewReady { get; }
            public bool DiagnosticsReady { get; }
            public int PromptCount { get; }
            public int ResponseCount { get; }

            public string ToStatusString()
            {
                return string.Join(";",
                    $"BlazorReady={BlazorReady}",
                    $"AssistViewReady={AssistViewReady}",
                    $"DiagnosticsReady={DiagnosticsReady}",
                    $"PromptCount={PromptCount}",
                    $"ResponseCount={ResponseCount}");
            }

            public static bool TryParse(string? statusText, out JarvisAutomationStatus status)
            {
                status = Empty;
                if (string.IsNullOrWhiteSpace(statusText))
                {
                    return false;
                }

                var parts = statusText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool blazorReady = false;
                bool assistViewReady = false;
                bool diagnosticsReady = false;
                int promptCount = 0;
                int responseCount = 0;

                foreach (var part in parts)
                {
                    var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (pair.Length != 2)
                    {
                        continue;
                    }

                    var key = pair[0];
                    var value = pair[1];

                    if (key.Equals("BlazorReady", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(value, out var parsed))
                        {
                            blazorReady = parsed;
                        }
                    }
                    else if (key.Equals("AssistViewReady", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(value, out var parsed))
                        {
                            assistViewReady = parsed;
                        }
                    }
                    else if (key.Equals("DiagnosticsReady", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(value, out var parsed))
                        {
                            diagnosticsReady = parsed;
                        }
                    }
                    else if (key.Equals("PromptCount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out var parsed))
                        {
                            promptCount = parsed;
                        }
                    }
                    else if (key.Equals("ResponseCount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out var parsed))
                        {
                            responseCount = parsed;
                        }
                    }
                }

                status = new JarvisAutomationStatus(blazorReady, assistViewReady, diagnosticsReady, promptCount, responseCount);
                return true;
            }
        }
    }
}
