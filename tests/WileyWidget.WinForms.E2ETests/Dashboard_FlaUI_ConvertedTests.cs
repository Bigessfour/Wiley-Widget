using System;
using System.IO;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
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

        [Fact(Skip = "Interactive UI E2E - run on a self-hosted interactive runner or locally")]
        public void Dashboard_LaunchesAndShowsMainWindow()
        {
            StartApp();
            using var window = GetMainWindow();

            Assert.NotNull(window);
            // Title is environment specific; check for partial match to be tolerant
            Assert.Contains("Wiley", window.Title, StringComparison.OrdinalIgnoreCase);

            // Basic smoke: ensure key top-level controls exist (IDs may be added by devs)
            // Example: var dashboardButton = window.FindFirstDescendant(cf => cf.ByAutomationId("DashboardButton"))?.AsButton();
            // if (dashboardButton != null) Assert.True(dashboardButton.IsEnabled);
        }

        [Fact(Skip = "Needs automation IDs/paths from developers — please implement the full flow")]
        public void Dashboard_Export_PDF_CreatesValidFile()
        {
            // TODO: Implement full export flow using automation IDs (recommended: give Export button/format dropdown automation IDs)
            // Guidance for implementer (you):
            // 1) StartApp();
            // 2) Find and click the 'Dashboard' navigation item if needed
            // 3) Wait for loading to finish
            // 4) Find and click Export button
            // 5) Select PDF option in file-type chooser
            // 6) Interact with SaveFileDialog (FlaUI supports Window.Modal dialogs)
            // 7) Enter a temp path (use Path.GetTempFileName() or a .pdf extension) and save
            // 8) Assert the file exists and contains "%PDF-" header

            StartApp();
            using var window = GetMainWindow();

            // Placeholder assertion until automation is implemented
            Assert.True(true, "Export PDF flow needs automation implementation; see method notes.");
        }

        [Fact(Skip = "Needs automation IDs/paths from developers — please implement the full flow")]
        public async Task Dashboard_AutoRefresh_UpdatesDataPeriodically()
        {
            // TODO: Implement the auto-refresh verification:
            // 1) StartApp(); navigate to Dashboard
            // 2) Locate a LastUpdated label control and parse timestamp
            // 3) Wait > configured interval (e.g., 30s) and assert new timestamp is different
            // Optionally, set test-specific shorter interval via a test mode or environment variable

            StartApp();
            using var window = GetMainWindow();

            // placeholder until automation implemented
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.True(true, "Auto-refresh test needs automation implementation; see method notes.");
        }

        public void Dispose()
        {
            try { _automation?.Dispose(); } catch { }
            try { if (_app != null && !_app.HasExited) { _app.Close(); _app.Dispose(); } } catch { }
        }
    }
}
