using System;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;
using FlaUIApplication = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    [Collection("UI Tests")]
    public sealed class PanelAndOverlayE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;
        private static readonly object _testLock = new object();

        public PanelAndOverlayE2ETests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");

            // Set the license key to avoid popup
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceXRQR2VfUER0W0o=");

            System.Threading.Monitor.Enter(_testLock);
        }

        private static AutomationElement? WaitForElement(Window? window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 12)
        {
            if (window == null) return null;
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        private void DismissLicensePopups()
        {
            if (_automation == null) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 2000)
            {
                try { System.Windows.Forms.Application.DoEvents(); } catch { }
            }

            var allWindows = _automation.GetDesktop().FindAllChildren();
            foreach (var window in allWindows)
            {
                if (window.Name != null && (window.Name.IndexOf("License", StringComparison.OrdinalIgnoreCase) >= 0 || window.Name.IndexOf("Syncfusion", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try
                    {
                        var closeButton = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK").Or(cf.ByName("Close"))));
                        if (closeButton != null) closeButton.AsButton().Click();
                        else window.AsWindow().Close();
                    }
                    catch { }
                }
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void PanelHeader_Refresh_ShowsLoadingAndLoadsGrid()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            var window = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result ?? throw new InvalidOperationException("Main window not found");
            var accounts = NavigationHelper.OpenView(_automation!, window, "Nav_Accounts", "Municipal Accounts");
            Assert.NotNull(accounts);

            var header = WaitForElement(accounts, cf => cf.ByAutomationId("headerLabel").Or(cf.ByName("Municipal Accounts")), 8);
            Assert.NotNull(header);

            var refresh = WaitForElement(accounts, cf => cf.ByAutomationId("btnRefresh").Or(cf.ByName("Refresh accounts list").Or(cf.ByName("Refresh"))), 8)?.AsButton();
            Assert.NotNull(refresh);

            refresh!.Click();

            var loading = WaitForElement(accounts, cf => cf.ByControlType(ControlType.Text).And(cf.ByName("Loading...")), 10);
            Assert.NotNull(loading);

            // Wait for the grid to be present after load completes
            var grid = Retry.WhileNull(() => accounts.FindFirstDescendant(cf => cf.ByAutomationId("dataGridAccounts")), TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(250)).Result;
            Assert.NotNull(grid);

            // Close the view for cleanup
            NavigationHelper.CloseView(accounts);
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void PanelNavigation_CachingAndClose_Works()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            var window = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result ?? throw new InvalidOperationException("Main window not found");
            var accounts = NavigationHelper.OpenView(_automation!, window, "Nav_Accounts", "Municipal Accounts");
            Assert.NotNull(accounts);

            // Count header labels for this panel text - should be exactly one
            var headers = window.FindAllDescendants(cf => cf.ByAutomationId("headerLabel")).Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.IndexOf("Municipal Accounts", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Assert.Single(headers);

            // Click nav again - should not duplicate
            var accountsAgain = NavigationHelper.OpenView(_automation!, window, "Nav_Accounts", "Municipal Accounts");
            Assert.NotNull(accountsAgain);

            var headersAfter = window.FindAllDescendants(cf => cf.ByAutomationId("headerLabel")).Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.IndexOf("Municipal Accounts", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Assert.Single(headersAfter);

            // Close via panel header close button
            var closeBtn = WaitForElement(accounts, cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Close")), 8)?.AsButton();
            Assert.NotNull(closeBtn);
            closeBtn!.Click();

            // Wait for header to disappear
            var headerGone = Retry.WhileTrue(() => window.FindFirstDescendant(cf => cf.ByAutomationId("headerLabel").And(cf.ByName("Municipal Accounts"))) != null, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            Assert.True(headerGone.Success, "Panel header did not disappear after clicking Close");
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void ChartPanel_Refresh_ShowsLoadingChartData()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            var window = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result ?? throw new InvalidOperationException("Main window not found");
            var charts = NavigationHelper.OpenView(_automation!, window, "Nav_Charts", "Budget Analytics");
            Assert.NotNull(charts);

            var refresh = WaitForElement(charts, cf => cf.ByName("Refresh").Or(cf.ByName("Refresh chart data")).Or(cf.ByAutomationId("btnRefresh")), 8)?.AsButton();
            Assert.NotNull(refresh);

            refresh!.Click();

            var loading = WaitForElement(charts, cf => cf.ByName("Loading chart data..."), 10);
            Assert.NotNull(loading);

            var cartesian = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Cartesian"), 12);
            Assert.NotNull(cartesian);

            var pie = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Pie"), 8);
            Assert.NotNull(pie);

            NavigationHelper.CloseView(charts);
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void ReportsPanel_Shows_NoDataOverlay_WhenNotLoaded()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            var window = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result ?? throw new InvalidOperationException("Main window not found");
            var reports = NavigationHelper.OpenView(_automation!, window, "Nav_Reports", "Reports");
            Assert.NotNull(reports);

            var noData = WaitForElement(reports, cf => cf.ByName("No report loaded"), 8);
            Assert.NotNull(noData);

            NavigationHelper.CloseView(reports);
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void Settings_Theme_Selection_Updates_MainThemeToggle()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));

            var window = _app.GetMainWindow(_automation!);

            var mainThemeBtn = WaitForElement(window, cf => cf.ByAutomationId("ThemeToggle").Or(cf.ByName("Theme_Toggle")).Or(cf.ByControlType(ControlType.Button)), 8)?.AsButton();
            Assert.NotNull(mainThemeBtn);
            var original = mainThemeBtn!.Name ?? string.Empty;

            var settings = NavigationHelper.OpenView(_automation!, window!, "Nav_Settings", "Settings");
            Assert.NotNull(settings);

            var themeCombo = WaitForElement(settings, cf => cf.ByAutomationId("themeCombo"), 8)?.AsComboBox();
            Assert.NotNull(themeCombo);

            var items = themeCombo!.Items ?? Array.Empty<AutomationElement>();
            if (items.Length > 1)
            {
                var currentIndex = Array.IndexOf(items, themeCombo.SelectedItem);
                var newIndex = currentIndex == 0 ? 1 : 0;
                themeCombo.Select(newIndex);

                // Wait for main theme button text to change
                var changed = Retry.WhileTrue(() =>
                {
                    var btnElement = window!.FindFirstDescendant(cf => cf.ByAutomationId("ThemeToggle").Or(cf.ByName("Theme_Toggle")).Or(cf.ByControlType(ControlType.Button)));
                    if (btnElement is null)
                        throw new InvalidOperationException("Theme toggle button not found");
                    var btn = btnElement.AsButton();
                    return string.Equals(btn.Name ?? string.Empty, original, StringComparison.Ordinal);
                }, TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(200));

                Assert.True(changed.Success, "Theme did not change after selecting new theme");
            }
            else
            {
                Assert.True(items.Length >= 1, "Theme combo should expose at least one option.");
            }

            NavigationHelper.CloseView(settings);
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void Dashboard_Refresh_Has_Description()
        {
            _automation = new UIA3Automation();
            _app = FlaUIApplication.Launch(_exePath);
            DismissLicensePopups();
            var window = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result ?? throw new InvalidOperationException("Main window not found");
            var dashboard = NavigationHelper.OpenView(_automation!, window, "Nav_Dashboard", "Dashboard");
            Assert.NotNull(dashboard);

            var refresh = WaitForElement(dashboard, cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")), 8);
            Assert.NotNull(refresh);

            var helpText = refresh?.Properties.HelpText?.Value?.ToString() ?? string.Empty;
            Assert.Contains("Reload", helpText, StringComparison.OrdinalIgnoreCase);

            NavigationHelper.CloseView(dashboard);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _app?.Close();
                System.Threading.Thread.Sleep(500);
                if (_app != null && !_app.HasExited)
                {
                    try { _app.Kill(); System.Threading.Thread.Sleep(250); } catch { }
                }
            }
            catch { }
            finally
            {
                try { _app?.Dispose(); } catch { }
                try { _automation?.Dispose(); } catch { }

                try { System.Threading.Monitor.Exit(_testLock); } catch { }
            }
        }
    }
}
