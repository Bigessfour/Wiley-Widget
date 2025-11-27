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

namespace WileyWidget.WinForms.E2ETests
{
    public class Dashboard_FlaUI_ConvertedTests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;

        public Dashboard_FlaUI_ConvertedTests()
        {
            // Try environment variable first so CI/test runners can provide published exe.
            _exePath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE") ?? Path.Combine("..","..","..","WileyWidget.WinForms","bin","Debug","net9.0-windows","WileyWidget.WinForms.exe");
        }

        private void EnsureInteractiveOrSkip()
        {
            // Prefer explicit opt-in via env var or a self-hosted runner label
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!optedIn && !selfHosted && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new Xunit.Sdk.SkipException("Skipping interactive UI test: requires self-hosted interactive runner (or set WILEYWIDGET_UI_TESTS=true).");
            }
        }

        private void StartApp()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Please set WILEYWIDGET_EXE env var to published executable or build the WinForms project.");
            }

            _app = Application.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("Application not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds)).Result ?? throw new InvalidOperationException("Main window not found");
        }

        [Fact]
        public void Dashboard_LaunchesAndShowsMainWindow()
        {
            EnsureInteractiveOrSkip();
            StartApp();
            using var window = GetMainWindow();

            Assert.NotNull(window);
            Assert.True(window.Title?.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0, "Main window title should contain the app name (Wiley)");

            // Basic smoke: ensure top-level toolbar controls are present
            var loadBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_LoadButton").Or(cf.ByName("Load Dashboard")))?.AsButton();
            var refreshBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            var exportBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton();

            Assert.NotNull(loadBtn);
            Assert.NotNull(refreshBtn);
            Assert.NotNull(exportBtn);
        }

        [Fact]
        public void Dashboard_Export_PDF_CreatesValidFile_Or_ShowsHelpfulMissingDependencyMessage()
        {
            EnsureInteractiveOrSkip();
            StartApp();
            using var window = GetMainWindow();

            // Find and click Export button
            var exportBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_ExportButton").Or(cf.ByName("Export")))?.AsButton();
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
            var done = Retry.WhileFalse(() => File.Exists(tempPdf), TimeSpan.FromSeconds(10));
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
                 || w.Modal ? true : false);

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
            EnsureInteractiveOrSkip();
            StartApp();
            using var window = GetMainWindow();

            // Find the LastUpdated label by automation id
            var lastLabel = window.FindFirstDescendant(cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel();
            Assert.NotNull(lastLabel);

            var firstText = lastLabel.Text ?? string.Empty;

            // Trigger refresh
            var refreshBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            Assert.NotNull(refreshBtn);

            refreshBtn.Invoke();

            // Wait up to 12 seconds for the label text to change
            var changed = Retry.While(() => (lastLabel.Text ?? string.Empty) == firstText, TimeSpan.FromSeconds(12));
            Assert.False(changed.Success, "Expected LastUpdated label to change after refresh, but it did not update in time.");
        }

        public void Dispose()
        {
            try { _automation?.Dispose(); } catch { }
            try { if (_app != null && !_app.HasExited) { _app.Close(); _app.Dispose(); } } catch { }
        }
    }
}
