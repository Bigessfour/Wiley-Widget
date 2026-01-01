using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;

namespace WileyWidget.WinForms.E2ETests
{
    [Collection("UI Tests")]
    public sealed class Grid_FlaUI_Tests : IDisposable
    {
        private readonly string _exePath;
        private FlaUI.Core.Application? _app;
        private UIA3Automation? _automation;

        public Grid_FlaUI_Tests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();
        }

        private Window GetMainWindow()
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("App not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(12)).Result!;
        }

        private void StartApp()
        {
            if (!File.Exists(_exePath)) throw new FileNotFoundException(_exePath);
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "TEST");
            _app = FlaUI.Core.Application.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void Dashboard_Grid_SortFilterExport_EndToEnd()
        {
            // Skip if not interactive environment
            var env = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            if (!optedIn && !env.Contains("self-hosted", StringComparison.OrdinalIgnoreCase) && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                return;

            StartApp();
            var window = GetMainWindow();

            var dashboard = new PageObjects.DashboardPage(_automation!, window);

            // Ensure data loaded
            dashboard.ClickLoad();

            var fundsGrid = dashboard.FundsGrid ?? throw new InvalidOperationException("Funds grid not found");
            var beforeCount = dashboard.GetFundsRowCount();
            Assert.True(beforeCount > 0, "Expected some rows in Funds grid");

            // Sort by first column header
            dashboard.SortByColumn(fundsGrid, "Fund");
            WaitForBusyIndicator(TimeSpan.FromSeconds(2));
            var firstCellAfterSort = dashboard.GetCellValue(fundsGrid, 0, 0);
            Assert.False(string.IsNullOrEmpty(firstCellAfterSort));

            // Apply test filter via main nav control
            var applyFilterBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("Nav_ApplyGridFilter").Or(cf.ByName("Apply Grid Filter"))), TimeSpan.FromSeconds(5));
            Assert.True(applyFilterBtn.Success && applyFilterBtn.Result != null, "Apply Grid Filter button not found");
            applyFilterBtn.Result.AsButton().Invoke();

            // Wait for filter to apply
            WaitForBusyIndicator(TimeSpan.FromSeconds(2));
            var afterCount = dashboard.GetFundsRowCount();
            Assert.True(afterCount <= beforeCount, "Filtered rows should be <= original");

            // Export grid using nav control and SaveFileDialog handling
            var exportBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("Nav_ExportGrid").Or(cf.ByName("Export Grid"))), TimeSpan.FromSeconds(5));
            Assert.True(exportBtn.Success && exportBtn.Result != null, "Export Grid button not found");
            exportBtn.Result.AsButton().Invoke();

            // Wait for SaveFileDialog and save temporary file
            var dialog = Retry.WhileNull(() => window.ModalWindows.FirstOrDefault(w => w.Title?.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0 || w.Title?.IndexOf("Export", StringComparison.OrdinalIgnoreCase) >= 0), TimeSpan.FromSeconds(6));
            if (dialog.Result != null)
            {
                var saveDialog = dialog.Result;
                var fileNameBox = saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.AsTextBox();
                var saveButton = saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Save")))?.AsButton() ?? saveDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK")))?.AsButton();
                var temp = Path.Combine(Path.GetTempPath(), $"GridExport_{Guid.NewGuid():N}.xlsx");
                if (fileNameBox != null && saveButton != null)
                {
                    fileNameBox.Text = temp;
                    saveButton.Invoke();
                }
                else
                {
                    saveDialog.Focus();
                    System.Windows.Forms.SendKeys.SendWait(temp);
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                }

                // Wait for file
                var exists = Retry.WhileFalse(() => File.Exists(temp), TimeSpan.FromSeconds(8));
                Assert.True(File.Exists(temp) || exists.Success, "Export file should exist or be created by test harness");

                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        public void Dispose()
        {
#pragma warning disable CA1063 // Implement IDisposable correctly - test class doesn't need full pattern
#pragma warning disable CA1816 // Dispose should call GC.SuppressFinalize - not needed for sealed test class
            try { _automation?.Dispose(); } catch { }
            try { if (_app != null && !_app.HasExited) { _app.Kill(); } } catch { }
            try { _app?.Dispose(); } catch { }
            // Kill any lingering WileyWidget processes, but skip when running inside the IDE test host
            var vstestHost = Environment.GetEnvironmentVariable("VSTEST_HOST_PROCESSID");
            if (string.IsNullOrWhiteSpace(vstestHost))
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("WileyWidget.WinForms");
                foreach (var p in processes)
                {
                    try { p.Kill(); } catch { }
                }
            }
#pragma warning restore CA1816
#pragma warning restore CA1063
        }
    }
}
