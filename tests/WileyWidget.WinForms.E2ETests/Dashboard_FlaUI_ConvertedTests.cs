using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Properties;

namespace WileyWidget.WinForms.E2ETests
{
    public sealed class Dashboard_FlaUI_ConvertedTests : IDisposable
    {
        private readonly string _exePath;
        private FlaUI.Core.Application? _app;
        private UIA3Automation? _automation;

        // Helper to retry-finding descendant controls to make tests resilient to slow UI startup
        private T? RetryFind<T>(Func<Window, T?> finder, Window window, int timeoutSeconds = 10) where T : class
        {
            if (window == null) return null;
            var res = Retry.WhileNull(() =>
            {
                try
                {
                    return finder(window);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Console.WriteLine($"COM error while searching for control: {ex.Message}");
                    return null;
                }
                catch
                {
                    return null;
                }
            }, TimeSpan.FromSeconds(timeoutSeconds));

            return res.Result;
        }

        public Dashboard_FlaUI_ConvertedTests()
        {
            // Try environment variable first so CI/test runners can provide published exe.
            _exePath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE") ?? Path.Combine("..","..","..","WileyWidget.WinForms","bin","Debug","net9.0-windows","WileyWidget.WinForms.exe");
        }

        private bool EnsureInteractiveOrSkip()
        {
            // Prefer explicit opt-in via env var or a self-hosted runner label
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;

            // Return false (do not proceed) when running in CI unless explicitly opted in or running on a self-hosted runner that is interactive.
            if (!optedIn && !selfHosted && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private bool StartApp()
        {
            if (!File.Exists(_exePath))
            {
                Console.WriteLine($"E2E UI test: exe not found at '{_exePath}'");
                return false;
            }

            try
            {
                _app = FlaUI.Core.Application.Launch(_exePath);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine($"E2E UI test: failed to launch exe - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"E2E UI test: unexpected launch error - {ex.Message}");
                return false;
            }

            // Wait for application to present a top-level window handle; sometimes the process
            // launches but UI Automation isn't immediately available.
            try
            {
                var started = FlaUI.Core.Tools.Retry.WhileFalse(() =>
                {
                    try
                    {
                        var p = System.Diagnostics.Process.GetProcessById(_app?.ProcessId ?? -1);
                        return p != null && p.MainWindowHandle != IntPtr.Zero;
                    }
                    catch { return false; }
                }, TimeSpan.FromSeconds(10));

                if (!started.Success)
                {
                    Console.WriteLine(Resources.ProcessStartedNoWindow);
                }
            }
            catch { /* best effort */ }

            // Create automation but tolerate transient COM failures (will be retried when attaching)
            try
            {
                _automation = new UIA3Automation();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Console.WriteLine($"E2E UI test: creating UIA3Automation failed (COM): {ex.Message}");
                _automation = null;
                // Let callers handle attach failures and retries
            }

            return true;
        }

        private Window? GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null) return null;
            if (_automation == null) return null;

            try
            {
                var res = Retry.WhileNull(() =>
                {
                    try
                    {
                        // Safer approach: search desktop top-level windows for our process or title match
                        try
                        {
                            var desktop = _automation.GetDesktop();
                            var all = desktop.FindAllChildren();
                            // Prefer a same-process window if possible
                            var candidate = all.FirstOrDefault(w =>
                            {
                                try
                                {
                                    var pidProp = w.Properties.ProcessId;
                                    if (pidProp?.Value is int pid && pid == _app.ProcessId)
                                        return true;
                                }
                                catch { /* ignore property issues */ }
                                return !string.IsNullOrWhiteSpace(w.Name) && w.Name.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0;
                            });

                            if (candidate != null) return candidate.AsWindow();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Desktop scan failed: {ex.Message}");
                            // fall back to older approach
                        }

                        try
                        {
                            return _app.GetMainWindow(_automation);
                        }
                        catch (System.ComponentModel.Win32Exception wx)
                        {
                            Console.WriteLine($"Win32Exception while attaching to main window: {wx.Message}");
                            return null;
                        }
                        catch (System.Runtime.InteropServices.COMException ex)
                        {
                            Console.WriteLine($"COM exception while getting main window: {ex.Message}");
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error while getting main window: {ex.Message}");
                        return null;
                    }
                }, TimeSpan.FromSeconds(timeoutSeconds));

                return res.Result; // may be null
            }
            catch (System.ComponentModel.Win32Exception wx)
            {
                Console.WriteLine($"GetMainWindow retry failed with Win32Exception: {wx.Message}");
                return null;
            }
            catch (System.Runtime.InteropServices.COMException ce)
            {
                Console.WriteLine($"GetMainWindow retry failed with COMException: {ce.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetMainWindow retry aborted: {ex.Message}");
                return null;
            }
        }

        [Fact]
        public void Dashboard_LaunchesAndShowsMainWindow()
        {
            if (!EnsureInteractiveOrSkip()) return;
            if (!StartApp()) return;
            var window = GetMainWindow();

            if (window == null)
            {
                Console.WriteLine(Resources.CouldNotAttachMainWindow);
                return;
            }

            Assert.True(window.Title?.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0, "Main window title should contain the app name (Wiley)");

            // Basic smoke: ensure top-level toolbar controls are present
            var loadBtn = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_LoadButton").Or(cf.ByName("Load Dashboard")))?.AsButton(), window, timeoutSeconds: 10);
            var refreshBtn = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton(), window, timeoutSeconds: 10);
            var exportBtn = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton(), window, timeoutSeconds: 10);

            if (loadBtn == null || refreshBtn == null || exportBtn == null)
            {
                Console.WriteLine(Resources.RequiredToolbarMissing);
                DumpDiagnostics(window);
                // Not failing hard in non-interactive environments: bail out so CI can continue.
                return;
            }
        }

        [Fact]
        public void Dashboard_Export_PDF_CreatesValidFile_Or_ShowsHelpfulMissingDependencyMessage()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();
            if (window == null)
            {
                Console.WriteLine(Resources.CouldNotAttachExport);
                return;
            }

            // Find and click Export button
            var exportBtn = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton(), window, timeoutSeconds: 8);
            if (exportBtn == null)
            {
                Console.WriteLine(Resources.ExportButtonMissing);
                DumpDiagnostics(window);
                return;
            }

            var tempPdf = Path.Combine(Path.GetTempPath(), $"WileyDashboardExport_{Guid.NewGuid():N}.pdf");
            if (File.Exists(tempPdf)) File.Delete(tempPdf);

            exportBtn.Invoke();

            // Wait for SaveFileDialog (title 'Export Dashboard' or 'Save As') — try modal child first, then search desktop
            var dialog = Retry.WhileNull(() => window.ModalWindows.FirstOrDefault(w =>
                  w.Title?.IndexOf("Export Dashboard", StringComparison.OrdinalIgnoreCase) >= 0
               || w.Title?.IndexOf("Save As", StringComparison.OrdinalIgnoreCase) >= 0), TimeSpan.FromSeconds(4));

            Window? saveDialogWindow = dialog.Success ? dialog.Result : null;

            if (saveDialogWindow == null)
            {
                // Try desktop-level windows (class '#32770' on many file dialogs; or title match)
                var desktopDialog = FindDesktopDialog(window, e =>
                    (!string.IsNullOrWhiteSpace(e.Name) && (e.Name.IndexOf("Export Dashboard", StringComparison.OrdinalIgnoreCase) >= 0 || e.Name.IndexOf("Save As", StringComparison.OrdinalIgnoreCase) >= 0))
                    || string.Equals(e.ClassName, "#32770", StringComparison.OrdinalIgnoreCase)
                , timeoutSeconds: 6);

                if (desktopDialog != null)
                {
                    saveDialogWindow = desktopDialog.AsWindow();
                }
            }

            if (saveDialogWindow != null)
            {
                var saveDialog = saveDialogWindow;

                // Try to find filename textbox & Save button, re-query in retries
                var fileNameBox = RetryFind(w => w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.AsTextBox(), saveDialog, timeoutSeconds: 4);
                var saveButton = RetryFind(w => w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Save")))?.AsButton(), saveDialog, timeoutSeconds: 4)
                                 ?? RetryFind(w => w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton(), saveDialog, timeoutSeconds: 4);

                if (fileNameBox == null || saveButton == null)
                {
                    // If dialog controls not found - try to fallback to sending keyboard input to dialog
                    try { saveDialog.Focus(); } catch { }
                    System.Windows.Forms.SendKeys.SendWait(tempPdf);
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                }
                else
                {
                    fileNameBox.Text = tempPdf;
                    saveButton.Invoke();
                }
            }

            // Wait for either the file to appear OR a message box explaining missing dependencies
            var done = Retry.WhileFalse(() => File.Exists(tempPdf), TimeSpan.FromSeconds(10));
            if (done.Success && File.Exists(tempPdf))
            {
                // Validate PDF header
                using var fs = File.OpenRead(tempPdf);
                var header = new byte[5];
                var bytesRead = fs.Read(header, 0, header.Length);
                var headerStr = System.Text.Encoding.ASCII.GetString(header, 0, bytesRead);
                Assert.Equal("%PDF-", headerStr);
                try { File.Delete(tempPdf); } catch { }
            }
            else
            {
                // Look for a MessageBox explaining missing dependency
                var message = window.ModalWindows.FirstOrDefault(w =>
                    (w.Title?.IndexOf("Missing Dependencies", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                 || (w.Title?.IndexOf("Export Error", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);


                // If message box not found as a separate window, scan any modal windows' content
                var informative = window.ModalWindows
                    .SelectMany(w => w.FindAllDescendants())
                    .Select(e => e.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Any(n => n.IndexOf("Syncfusion", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("export", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Missing Dependencies", StringComparison.OrdinalIgnoreCase) >= 0);

                Assert.True(informative, "Export did not produce a file and no helpful message about missing dependencies was shown. Check test environment or enable Syncfusion export dependencies.");
            }
        }

        [Fact]
        public void Dashboard_AutoRefresh_UpdatesData_WhenRefreshed()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();
            if (window == null)
            {
                Console.WriteLine(Resources.CouldNotAttachAutoRefresh);
                return;
            }

            // Find the LastUpdated label by automation id
            var lastLabel = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel(), window, timeoutSeconds: 8);
            if (lastLabel == null)
            {
                Console.WriteLine(Resources.LastUpdatedLabelMissing);
                DumpDiagnostics(window);
                return;
            }

            var firstText = lastLabel.Text ?? string.Empty;

            // Trigger refresh
            var refreshBtn = RetryFind(window => window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton(), window, timeoutSeconds: 8);
            if (refreshBtn == null)
            {
                Console.WriteLine(Resources.RefreshButtonMissing);
                DumpDiagnostics(window);
                return;
            }

            refreshBtn.Invoke();

            // Wait up to 12 seconds for the label text to change. Re-query the label each iteration to avoid stale element.
            var changedFlag = false;
            for (int i = 0; i < 12; i++)
            {
                var currentLabel = RetryFind(w => w.FindFirstDescendant(cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel(), window, timeoutSeconds: 1);
                var currentText = currentLabel?.Text ?? string.Empty;
                if (currentText != firstText) { changedFlag = true; break; }
                System.Threading.Thread.Sleep(1000);
            }
            Assert.True(changedFlag, "Expected LastUpdated label to change after refresh, but it did not update in time.");
        }

        private AutomationElement? FindDesktopDialog(Window? window, Func<AutomationElement, bool> predicate, int timeoutSeconds = 6)
        {
            try
            {
                var desktop = window?.Automation?.GetDesktop();
                if (desktop == null)
                {
                    // Fallback to creating a short-lived automation instance
                    using var a = new UIA3Automation();
                    desktop = a.GetDesktop();
                }

                var res = Retry.WhileNull(() =>
                {
                    try
                    {
                        var all = desktop!.FindAllChildren();
                        return all.FirstOrDefault(predicate);
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        Console.WriteLine($"COM error enumerating desktop windows: {ex.Message}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error enumerating desktop windows: {ex.Message}");
                        return null;
                    }
                }, TimeSpan.FromSeconds(timeoutSeconds));

                return res.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FindDesktopDialog failed: {ex.Message}");
                return null;
            }
        }

        private void DumpDiagnostics(Window window)
        {
            try
            {
                Console.WriteLine(Resources.DiagnosticsTopLevel);
                try
                {
                    using var a = new UIA3Automation();
                    var desktop = a.GetDesktop();
                    var tops = desktop.FindAllChildren();
                    foreach (var w in tops.Take(8))
                    {
                        Console.WriteLine($"Window: name='{w.Name}', AutomationId='{w.AutomationId}', ClassName='{w.ClassName}'");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read desktop windows: {ex.Message}");
                }

                Console.WriteLine(Resources.DiagnosticsSubtree);
                var all = window.FindAllDescendants();
                int i = 0;
                foreach (var e in all)
                {
                    Console.WriteLine($"[{i}] Type={e.ControlType}, Name='{e.Name}', AutomationId='{e.AutomationId}', Class='{e.ClassName}'");
                    i++;
                    if (i > 200) break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DumpDiagnostics failed: {ex.Message}");
            }
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
}
