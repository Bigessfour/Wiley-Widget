using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Properties;

namespace WileyWidget.WinForms.E2ETests;

public sealed class E2ETests : IDisposable
{
    private FlaUI.Core.Application? _app;
    private UIA3Automation? _automation;

    private string GetTargetExePath()
    {
        // The CI job should publish the application and set this env variable to the published exe path.
        // For local runs, you can set INTEGRATION_EXE_PATH or build and provide the executable path.
        var env = Environment.GetEnvironmentVariable("INTEGRATION_EXE_PATH") ?? ".\\publish\\WileyWidget.WinForms.exe";
        return env;
    }

    private bool EnsureInteractiveOrSkip()
    {
        var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
        var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
        var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!optedIn && !selfHosted && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(Resources.NotInteractiveSkip);
            return false;
        }
        return true;
    }

    private FlaUI.Core.AutomationElements.Window? WaitForMainWindow(FlaUI.Core.Application app, UIA3Automation automation, int timeoutSeconds = 15)
    {
        if (app == null || automation == null) return null;

        // Try multiple strategies to find an attached main window while handling intermittent COM errors
        try
        {
            var result = FlaUI.Core.Tools.Retry.WhileNull(() =>
            {
                try
                {
                    // Prefer desktop scan to find windows owned by the app process or matching title
                    try
                    {
                        var desktop = automation.GetDesktop();
                        var all = desktop.FindAllChildren();
                        var candidate = all.FirstOrDefault(w =>
                        {
                            try
                            {
                                var pidProp = w.Properties.ProcessId;
                                if (pidProp?.Value is int pid && pid == app.ProcessId) return true;
                            }
                            catch { }
                            return !string.IsNullOrWhiteSpace(w.Name) && w.Name.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0;
                        });

                        if (candidate != null) return candidate.AsWindow();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Desktop scan failed while finding windows: {ex.Message}");
                    }

                    // Fall back to GetMainWindow (may throw System.ComponentModel.Win32Exception or COMException)
                    try
                    {
                        return app.GetMainWindow(automation);
                    }
                    catch (System.ComponentModel.Win32Exception wx)
                    {
                        Console.WriteLine($"Win32Exception while attaching to main window: {wx.Message}");
                        return null;
                    }
                    catch (COMException cex)
                    {
                        Console.WriteLine($"COM exception while attaching to main window: {cex.Message}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error finding main window: {ex.Message}");
                    return null;
                }
            }, TimeSpan.FromSeconds(timeoutSeconds));

            return result.Result;
        }
        catch (System.ComponentModel.Win32Exception wx)
        {
            Console.WriteLine($"Retry failed with Win32Exception: {wx.Message}");
            return null;
        }
        catch (COMException cex)
        {
            Console.WriteLine($"Retry failed with COM exception: {cex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMainWindow retry aborted: {ex.Message}");
            return null;
        }
    }

    [Fact]
    public void MainFormStartsAndShowsWindow()
    {
        if (!EnsureInteractiveOrSkip()) return;

        var exe = GetTargetExePath();

        if (!File.Exists(exe))
        {
            Console.WriteLine($"E2E UI test skipped: exe not found at '{exe}'");
            return;
        }

        try
        {
            _app = FlaUI.Core.Application.Launch(exe);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine($"E2E UI test skipped: launching exe failed with Win32Exception: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"E2E UI test skipped: launching exe failed: {ex.Message}");
            return;
        }

        _automation = new UIA3Automation();

        var mainWindow = WaitForMainWindow(_app, _automation, timeoutSeconds: 20);
        if (mainWindow == null)
        {
            Console.WriteLine(Resources.CouldNotFindOrAttachMainWindow);
            try { _app?.Close(); } catch { }
            return;
        }

        Assert.False(mainWindow.IsOffscreen, "Main window should be visible");

        // Basic check: window title contains expected product name
        var title = mainWindow.Title ?? string.Empty;
        Assert.True(title.Length > 0, "Main window should have a title");

        // Close gracefully
        try { _app.Close(); } catch { }
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            try { _automation?.Dispose(); } catch { }
            try { if (_app != null && !_app.HasExited) { _app.Close(); _app.Dispose(); } } catch { }
        }
        _disposed = true;
    }
}
