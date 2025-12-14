using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using FlaUIApplication = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    [CollectionDefinition("UI Tests", DisableParallelization = true)]
    public class UITestsCollection { }

    [Collection("UI Tests")]
    public class Dashboard_FlaUI_ConvertedTests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;

        public Dashboard_FlaUI_ConvertedTests()
        {
            // Try environment variable first so CI/test runners can provide published exe.
            _exePath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE") ?? Path.Combine("..","..","..","WileyWidget.WinForms","bin","Debug","net9.0-windows10.0.26100.0","WileyWidget.WinForms.exe");

            // Disable MDI mode for UI tests so forms open as separate windows
            Environment.SetEnvironmentVariable("UI:UseMdiMode", "false");
            Environment.SetEnvironmentVariable("UI:UseTabbedMdi", "false");
        }

        private static bool IsModalWindow(Window candidate)
        {
            var windowPattern = candidate.Patterns.Window;
            if (!windowPattern.IsSupported)
            {
                return false;
            }

            return windowPattern.TryGetPattern(out var pattern) && (pattern.IsModal?.Value ?? false);
        }

        private bool EnsureInteractiveOrSkip()
        {
            // Prefer explicit opt-in via env var or a self-hosted runner label
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
            var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

            if (isCi && !optedIn && !selfHosted)
            {
                // CI without interactive runner should ignore these tests
                return false;
            }

            return true;
        }

        private void StartApp()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Please set WILEYWIDGET_EXE env var to published executable or build the WinForms project.");
            }

            // Set the license key in the current process environment so the launched app inherits it
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceXRQR2VfUER0W0o=");

            _app = FlaUIApplication.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("Application not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds)).Result ?? throw new InvalidOperationException("Main window not found");
        }

        private static AutomationElement? WaitForElement(Window window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 12)
        {
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        [Fact]
        public void Dashboard_LaunchesAndShowsMainWindow()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            Assert.NotNull(window);
            Assert.True(window.Title?.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0, "Main window title should contain the app name (Wiley)");

            // Basic smoke: ensure top-level toolbar controls are present
            var loadBtn = WaitForElement(window, cf => cf.ByAutomationId("Toolbar_LoadButton").Or(cf.ByName("Load Dashboard")))?.AsButton();
            var refreshBtn = WaitForElement(window, cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            var exportBtn = WaitForElement(window, cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton();

            Assert.NotNull(loadBtn);
            Assert.NotNull(refreshBtn);
            Assert.NotNull(exportBtn);
        }

        [Fact]
        public void Dashboard_Export_PDF_CreatesValidFile_Or_ShowsHelpfulMissingDependencyMessage()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Find and click Export button
            var exportBtn = WaitForElement(window, cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton();
            Assert.NotNull(exportBtn);

            var tempPdf = Path.Combine(Path.GetTempPath(), $"WileyDashboardExport_{Guid.NewGuid():N}.pdf");
            if (File.Exists(tempPdf)) File.Delete(tempPdf);

            exportBtn.Invoke();

            // Wait for SaveFileDialog (title 'Export Dashboard' as set in code) — try multiple ways
            var dialog = Retry.WhileNull(() => window.ModalWindows.FirstOrDefault(w =>
                  w.Title?.IndexOf("Export Dashboard", StringComparison.OrdinalIgnoreCase) >= 0
               || w.Title?.IndexOf("Save As", StringComparison.OrdinalIgnoreCase) >= 0), TimeSpan.FromSeconds(6));

            if (dialog.Success && dialog.Result != null)
            {
                var saveDialog = dialog.Result;

                // Try to find filename textbox & Save button
                var fileNameBox = saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.AsTextBox();
                var saveButton = saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Save")))?.AsButton()
                                 ?? saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton();

                if (fileNameBox == null || saveButton == null)
                {
                    // If dialog controls not found - try to fallback to sending keyboard input to dialog
                    saveDialog.Focus();
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
            var done = Retry.While(() => File.Exists(tempPdf), exists => !exists, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            if (done.Success && File.Exists(tempPdf))
            {
                // Validate PDF header
                using var fs = File.OpenRead(tempPdf);
                var header = new byte[5];
                fs.Read(header, 0, header.Length);
                var headerStr = System.Text.Encoding.ASCII.GetString(header);
                Assert.Equal("%PDF-", headerStr);
                try { File.Delete(tempPdf); } catch { }
            }
            else
            {
                // Look for a MessageBox explaining missing dependency
                var message = window.ModalWindows.FirstOrDefault(w =>
                    (w.Title?.IndexOf("Missing Dependencies", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                 || (w.Title?.IndexOf("Export Error", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                 || IsModalWindow(w));

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
        public async Task Dashboard_AutoRefresh_UpdatesData_WhenRefreshed()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Find the LastUpdated label by automation id
            var lastLabel = WaitForElement(window, cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel();
            Assert.NotNull(lastLabel);

            var firstText = lastLabel.Text ?? string.Empty;

            // Trigger refresh
            var refreshBtn = WaitForElement(window, cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            Assert.NotNull(refreshBtn);

            refreshBtn.Invoke();

            // Wait up to 12 seconds for the label text to change
            var changed = Retry.While(() => (lastLabel.Text ?? string.Empty) == firstText, same => same, TimeSpan.FromSeconds(12), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "Expected LastUpdated label to change after refresh, but it did not update in time.");
        }

        public void Dispose()
        {
            try { _automation?.Dispose(); } catch { }
            try { if (_app != null && !_app.HasExited) { _app.Kill(); _app.Dispose(); } } catch { }
        }
    }
}
