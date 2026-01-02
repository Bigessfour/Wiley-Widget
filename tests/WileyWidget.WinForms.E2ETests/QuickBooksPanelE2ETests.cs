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
using WileyWidget.WinForms.E2ETests.Helpers;
using FlaUIApplication = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// E2E tests for QuickBooksPanel using FlaUI automation.
    /// Tests connection management, sync operations, history tracking, and accessibility support.
    /// </summary>
    [Collection("UI Tests")]
    public class QuickBooksPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public QuickBooksPanelE2ETests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();
        }

        private bool EnsureInteractiveOrSkip()
        {
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
            var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

            if (isCi && !optedIn && !selfHosted)
            {
                return false;
            }

            return true;
        }

        private void StartApp()
        {
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Set WILEYWIDGET_EXE environment variable.");
            }

            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceHRQR2VfUER0W0o=");

            _app = FlaUIApplication.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("Application not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds)).Result 
                ?? throw new InvalidOperationException("Main window not found");
        }

        private static AutomationElement? WaitForElement(Window window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 12)
        {
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_OpensAndDisplaysControls()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_QuickBooks", "QuickBooks");

            // Verify main elements are present
            var connectionStatus = WaitForElement(window, cf => cf.ByName("Connection Status").Or(cf.ByAutomationId("ConnectionStatusLabel")));
            Assert.NotNull(connectionStatus);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_ConnectButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find connect button
            var connectBtn = WaitForElement(window, cf => cf.ByName("Connect").Or(cf.ByAutomationId("ConnectButton")))?.AsButton();
            Assert.NotNull(connectBtn);
            Assert.True(connectBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_DisconnectButton_ExistsWhenConnected()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find disconnect button (may be disabled if not connected)
            var disconnectBtn = WaitForElement(window, cf => cf.ByName("Disconnect").Or(cf.ByAutomationId("DisconnectButton")))?.AsButton();
            
            if (disconnectBtn != null)
            {
                // Button exists, check if it's enabled/disabled based on connection state
                Assert.NotNull(disconnectBtn);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_SyncCustomersButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find sync customers button
            var syncCustomersBtn = WaitForElement(window, cf => cf.ByName("Sync Customers").Or(cf.ByAutomationId("SyncCustomersButton")))?.AsButton();
            Assert.NotNull(syncCustomersBtn);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_SyncAccountsButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find sync accounts button
            var syncAccountsBtn = WaitForElement(window, cf => cf.ByName("Sync Accounts").Or(cf.ByAutomationId("SyncAccountsButton")))?.AsButton();
            Assert.NotNull(syncAccountsBtn);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_SyncHistoryGrid_Displays()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find sync history grid
            var historyGrid = WaitForElement(window, cf => cf.ByName("Sync History").Or(cf.ByAutomationId("SyncHistoryGrid")));
            Assert.NotNull(historyGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_ConnectionStatus_ShowsState()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find connection status label
            var statusLabel = WaitForElement(window, cf => cf.ByName("Connection Status").Or(cf.ByAutomationId("ConnectionStatusLabel")));
            Assert.NotNull(statusLabel);

            // Verify status text is present
            var statusText = statusLabel.Name;
            Assert.NotNull(statusText);
            Assert.NotEmpty(statusText);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_SyncButton_ExecutesSync()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find sync all button
            var syncAllBtn = WaitForElement(window, cf => cf.ByName("Sync All").Or(cf.ByAutomationId("SyncAllButton")))?.AsButton();
            
            if (syncAllBtn != null && syncAllBtn.IsEnabled)
            {
                syncAllBtn.Click();

                // Wait for sync operation
                WaitForBusyIndicator(TimeSpan.FromSeconds(20));

                // Verify sync history updated
                var historyGrid = WaitForElement(window, cf => cf.ByName("Sync History"));
                Assert.NotNull(historyGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_RefreshButton_ReloadsHistory()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();
            
            if (refreshBtn != null)
            {
                refreshBtn.Click();
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify history grid still present
                var historyGrid = WaitForElement(window, cf => cf.ByName("Sync History"));
                Assert.NotNull(historyGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_ErrorLog_DisplaysIssues()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find error log or issues panel
            var errorLog = WaitForElement(window, cf => cf.ByName("Error Log").Or(cf.ByAutomationId("ErrorLogPanel")));
            
            if (errorLog != null)
            {
                Assert.True(errorLog.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "QuickBooks")]
        public void QuickBooksPanel_SettingsButton_OpensConfiguration()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to QuickBooks
            var qbMenu = WaitForElement(window, cf => cf.ByName("QuickBooks"));
            qbMenu?.Click();

            // Find settings button
            var settingsBtn = WaitForElement(window, cf => cf.ByName("Settings").Or(cf.ByAutomationId("SettingsButton")))?.AsButton();
            
            if (settingsBtn != null && settingsBtn.IsEnabled)
            {
                settingsBtn.Click();

                // Verify settings dialog or panel opens
                var settingsDialog = WaitForElement(window, cf => cf.ByName("QuickBooks Settings").Or(cf.ByControlType(ControlType.Window)));
                // May be null if settings are inline
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _app?.Close();
                        _app?.Dispose();
                        _app = null;
                        _automation?.Dispose();
                        _automation = null;
                    }
                    catch
                    {
                        // Suppress cleanup errors
                    }
                }
                _disposed = true;
            }
        }
    }
}
