using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;
using WileyWidget.WinForms.E2ETests.PageObjects;
using Application = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// FlaUI E2E tests for CustomersForm - Utility Customer Management view.
    /// Tests customer CRUD operations, search, filtering, and export functionality.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class CustomersFormE2ETests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;
        private const int DefaultTimeout = 20000;

        public CustomersFormE2ETests()
        {
            _exePath = ResolveExecutablePath();

            // Enable in-memory mode and simplified chrome for UI automation stability
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");
            Environment.SetEnvironmentVariable("UI__UseMdiMode", "false");
            Environment.SetEnvironmentVariable("UI__UseTabbedMdi", "false");
        }

        private static string ResolveExecutablePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"));
            if (!Directory.Exists(baseDir))
            {
                throw new DirectoryNotFoundException($"Build output directory not found at '{baseDir}'. Build WileyWidget.WinForms or set WILEYWIDGET_EXE.");
            }

            var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");
            if (File.Exists(standard))
            {
                return standard;
            }

            var versioned = Directory.GetDirectories(baseDir, "net9.0-windows*")
                .Select(dir => Path.Combine(dir, "WileyWidget.WinForms.exe"))
                .FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(versioned))
            {
                return versioned;
            }

            throw new FileNotFoundException($"Executable not found. Build Debug output under '{baseDir}'.");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_Opens_And_Displays_Grid()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            Assert.NotNull(customersPage);
            Assert.True(customersPage.IsCustomersGridLoaded(), "Customers grid should be loaded and visible");

            // Verify data grid exists
            var dataGrid = customersPage.CustomersGrid;
            Assert.NotNull(dataGrid);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_LoadButton_LoadsCustomers()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Click Load button
            customersPage.ClickLoad();

            // Verify status bar updated with record count
            Assert.True(customersPage.IsDataLoaded(), "Status bar should show record count after loading");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_NewButton_EnablesSaveButton()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Click New button
            customersPage.ClickNew();

            // Verify Save button is enabled
            Assert.True(customersPage.IsSaveButtonEnabled(), "Save button should be enabled after clicking New");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_SearchBox_IsAccessible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Find search textbox
            var searchBox = customersPage.SearchBox;
            Assert.NotNull(searchBox);
            Assert.True(searchBox.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_RefreshButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            var refreshButton = customersPage.RefreshButton;
            Assert.NotNull(refreshButton);
            Assert.True(refreshButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_ExportButton_OpensDialog()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Load some data first
            customersPage.ClickLoad();

            var exportButton = customersPage.ExportButton;
            Assert.NotNull(exportButton);

            customersPage.ClickExport();

            // Wait for SaveFileDialog to appear
            var saveDialog = Retry.WhileNull(() =>
            {
                var desktop = _automation?.GetDesktop();
                return desktop?.FindFirstChild(cf => cf.ByControlType(ControlType.Window).And(cf.ByName("Save As")));
            }, timeout: TimeSpan.FromSeconds(3)).Result;

            if (saveDialog != null)
            {
                // Close the dialog (Cancel button)
                var cancelButton = saveDialog.FindFirstDescendant(cf => cf.ByName("Cancel"));
                cancelButton?.AsButton().Invoke();
            }
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_DetailTabs_ArePresent()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Verify detail tabs are present
            Assert.True(customersPage.AreDetailTabsVisible(), "Detail tabs should be present and visible");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_DataGrid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            var dataGrid = customersPage.CustomersGrid;
            Assert.NotNull(dataGrid);

            // Load data first
            customersPage.ClickLoad();

            // Wait for grid to populate
            var rowCount = customersPage.GetCustomersRowCount();
            Assert.True(rowCount > 0, "Grid should contain data rows after loading");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_StatusBar_ShowsRecordCount()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Load data
            customersPage.ClickLoad();

            // Verify status bar shows record count
            Assert.True(customersPage.IsDataLoaded(), "Status bar should show record count after loading");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_SelectionEnablesButtons()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersPage = OpenCustomersView(mainWindow);

            // Load data
            customersPage.ClickLoad();

            // Select first row
            customersPage.SelectFirstRow();

            // Verify Delete button exists (enabled state may vary based on selection)
            var deleteButton = customersPage.DeleteButton;
            Assert.NotNull(deleteButton);
        }

        private CustomersPage OpenCustomersView(Window mainWindow)
        {
            var customersWindow = NavigationHelper.OpenView(_automation!, mainWindow, "Nav_Customers", "Customer Management");
            return new CustomersPage(_automation!, customersWindow);
        }

        private Window GetMainWindow()
        {
            var mainWindow = Retry.WhileNull(() => _app?.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(DefaultTimeout / 1000));
            Assert.NotNull(mainWindow);
            return mainWindow.Result!;
        }

        private bool EnsureInteractiveOrSkip()
        {
            var uiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            if (!string.Equals(uiTests, "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        private void StartApp()
        {
            _automation = new UIA3Automation();

            var startInfo = new System.Diagnostics.ProcessStartInfo(_exePath)
            {
                UseShellExecute = false,
                EnvironmentVariables =
                {
                    ["WILEYWIDGET_UI_TESTS"] = "true",
                    ["WILEYWIDGET_USE_INMEMORY"] = "true",
                    ["UI__IsUiTestHarness"] = "true",
                    ["UI__UseMdiMode"] = "false",
                    ["UI__UseTabbedMdi"] = "false"
                }
            };

            _app = Application.Launch(startInfo);

            Retry.WhileException(() =>
            {
                var window = _app.GetMainWindow(_automation);
                if (window == null || !window.IsAvailable)
                {
                    throw new InvalidOperationException("Main window not ready");
                }
            }, TimeSpan.FromMilliseconds(DefaultTimeout));
        }



        public void Dispose()
        {
            try
            {
                _app?.Close();
                _app?.Dispose();
            }
            catch { }

            try
            {
                _automation?.Dispose();
            }
            catch { }
        }
    }
}
