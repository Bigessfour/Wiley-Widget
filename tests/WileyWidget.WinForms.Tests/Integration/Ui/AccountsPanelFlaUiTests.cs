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
    public class AccountsPanelFlaUiTests
    {
        private const string MainWindowTitle = "Wiley Widget - Municipal Budget Management System";
        private const string AccountsPanelTitle = "Chart of Accounts";

        [StaFact]
        public void AccountsPanel_RendersWithButtons_WhenPanelActivated()
        {
            var exePath = ResolveWinFormsExePath();
            var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            var previousTests = Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS");

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");

            FlaUIApp? app = null;
            try
            {
                app = FlaUIApp.Launch(exePath);
                TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                using var automation = new UIA3Automation();

                var window = WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Activate Accounts panel (via ribbon button or keyboard shortcut)
                ActivateAccountsPanel(window, automation, TimeSpan.FromSeconds(30));

                // Wait for panel to fully load
                Thread.Sleep(2000);

                // Verify buttons are present
                var newAccountButton = FindElementByNameOrId(window, "New Account", "btnNewAccount", TimeSpan.FromSeconds(10));
                Assert.NotNull(newAccountButton);
                Assert.True(newAccountButton.Properties.IsEnabled.Value, "New Account button should be enabled");

                var editButton = FindElementByNameOrId(window, "Edit", "btnEdit", TimeSpan.FromSeconds(5));
                Assert.NotNull(editButton);
                // Edit button may be disabled initially (no selection)

                var deleteButton = FindElementByNameOrId(window, "Delete", "btnDelete", TimeSpan.FromSeconds(5));
                Assert.NotNull(deleteButton);
                // Delete button may be disabled initially (no selection)

                var refreshButton = FindElementByNameOrId(window, "Refresh", "btnRefresh", TimeSpan.FromSeconds(5));
                Assert.NotNull(refreshButton);
                Assert.True(refreshButton.Properties.IsEnabled.Value, "Refresh button should be enabled");

                // Verify grid is present
                var grid = FindElementByNameOrId(window, "Accounts Grid", "dataGridAccounts", TimeSpan.FromSeconds(10));
                Assert.NotNull(grid);

                // Verify data loads (grid should have rows after load)
                // This is a basic smoke test - we're not validating specific data
                var hasData = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasData, "Grid should load account data");
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
                Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previousTests);

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

        [StaFact]
        public void AccountsPanel_RefreshButton_ReloadsData_WhenClicked()
        {
            var exePath = ResolveWinFormsExePath();
            var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            var previousTests = Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS");

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");

            FlaUIApp? app = null;
            try
            {
                app = FlaUIApp.Launch(exePath);
                TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                using var automation = new UIA3Automation();

                var window = WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Activate Accounts panel
                ActivateAccountsPanel(window, automation, TimeSpan.FromSeconds(30));
                Thread.Sleep(2000);

                // Wait for initial data load
                var hasData = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasData, "Grid should load initial data");

                // Find and click refresh button
                var refreshButton = FindElementByNameOrId(window, "Refresh", "btnRefresh", TimeSpan.FromSeconds(5));
                Assert.NotNull(refreshButton);

                refreshButton.AsButton().Invoke();
                Thread.Sleep(1000);

                // Verify data is still present after refresh
                var hasDataAfterRefresh = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasDataAfterRefresh, "Grid should still have data after refresh");
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
                Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previousTests);

                if (app != null)
                {
                    try
                    {
                        app.Close();
                    }
                    catch
                    {
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
                }
                catch (System.Runtime.InteropServices.COMException)
                {
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
            }
            catch (System.Runtime.InteropServices.COMException)
            {
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

        private static void ActivateAccountsPanel(Window window, UIA3Automation automation, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                // Try to find and click the Accounts navigation button
                if (TryClickAccountsButton(window))
                {
                    Thread.Sleep(1500); // Give panel time to activate and load
                    return;
                }

                // Try keyboard shortcut Ctrl+A (from menu)
                window.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.LCONTROL, VirtualKeyShort.KEY_A);
                Thread.Sleep(750);

                // Check if panel appeared
                if (IsAccountsPanelVisible(window))
                {
                    return;
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException("Unable to activate Accounts panel via UIA or keyboard shortcut.");
        }

        private static bool TryClickAccountsButton(Window window)
        {
            try
            {
                // Look for ToolStripButton with text "Accounts" or Name="Nav_Accounts"
                var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                foreach (var button in buttons)
                {
                    var name = TryGetName(button);
                    var automationId = TryGetAutomationId(button);

                    if ((name != null && name.Equals("Accounts", StringComparison.OrdinalIgnoreCase))
                        || (automationId != null && automationId.Equals("Nav_Accounts", StringComparison.OrdinalIgnoreCase)))
                    {
                        button.AsButton().Invoke();
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            return false;
        }

        private static bool IsAccountsPanelVisible(Window window)
        {
            try
            {
                var elements = window.FindAllDescendants();
                foreach (var element in elements)
                {
                    var name = TryGetName(element);
                    if (name != null && name.Contains(AccountsPanelTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            return false;
        }

        private static AutomationElement? FindElementByNameOrId(Window window, string name, string automationId, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var elements = window.FindAllDescendants();
                    foreach (var element in elements)
                    {
                        var elementName = TryGetName(element);
                        var elementId = TryGetAutomationId(element);

                        if ((elementName != null && elementName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            || (elementId != null && elementId.Equals(automationId, StringComparison.OrdinalIgnoreCase)))
                        {
                            return element;
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }

                Thread.Sleep(250);
            }

            return null;
        }

        private static bool WaitForGridData(Window window, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    // Look for grid elements or data grid patterns
                    var grid = FindElementByNameOrId(window, "Accounts Grid", "dataGridAccounts", TimeSpan.FromSeconds(2));
                    if (grid != null)
                    {
                        // Try to find grid rows (cells, data items, etc.)
                        var children = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                        if (children.Length > 0)
                        {
                            return true;
                        }

                        // Alternative: Check for text elements that might indicate data
                        var textElements = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                        if (textElements.Length > 10) // Arbitrary threshold for "has data"
                        {
                            return true;
                        }
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }

                Thread.Sleep(500);
            }

            return false;
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
    }
}
