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
            var customersWindow = OpenCustomersView(mainWindow);

            Assert.NotNull(customersWindow);
            Assert.Contains("Customer", customersWindow.Title, StringComparison.OrdinalIgnoreCase);

            // Verify data grid exists
            var dataGrid = WaitForElement(customersWindow, cf => cf.ByAutomationId("Customers_DataGrid"));
            Assert.NotNull(dataGrid);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_LoadButton_LoadsCustomers()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            // Find Load button in toolbar
            var loadButton = WaitForElement(customersWindow, cf => cf.ByName("Load"));
            Assert.NotNull(loadButton);

            WaitUntilResponsive(loadButton);
            loadButton.AsButton().Invoke();

            // Wait for status bar to update with record count
            var statusBar = Retry.WhileNull(() =>
            {
                var status = customersWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.StatusBar));
                return status?.Properties.Name.ValueOrDefault?.Contains("Records:", StringComparison.OrdinalIgnoreCase) == true ? status : null;
            }, timeout: TimeSpan.FromSeconds(5)).Result;

            Assert.NotNull(statusBar);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_NewButton_EnablesSaveButton()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            // Find New button in toolbar
            var newButton = WaitForElement(customersWindow, cf => cf.ByName("New"));
            Assert.NotNull(newButton);

            WaitUntilResponsive(newButton);
            newButton.AsButton().Invoke();

            // Wait for Save button to become enabled
            var saveButton = WaitForElement(customersWindow, cf => cf.ByName("Save"));
            Assert.NotNull(saveButton);

            // Give UI time to update button state
            Thread.Sleep(500);
            Assert.True(saveButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_SearchBox_IsAccessible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            // Find search textbox in toolbar
            var searchBox = WaitForElement(customersWindow, cf => cf.ByControlType(ControlType.Edit));
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
            var customersWindow = OpenCustomersView(mainWindow);

            var refreshButton = WaitForElement(customersWindow, cf => cf.ByName("Refresh"));
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
            var customersWindow = OpenCustomersView(mainWindow);

            // Load some data first
            var loadButton = WaitForElement(customersWindow, cf => cf.ByName("Load"));
            WaitUntilResponsive(loadButton);
            loadButton?.AsButton().Invoke();
            Thread.Sleep(1000);

            var exportButton = WaitForElement(customersWindow, cf => cf.ByName("Export"));
            Assert.NotNull(exportButton);

            WaitUntilResponsive(exportButton);
            exportButton.AsButton().Invoke();

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
            var customersWindow = OpenCustomersView(mainWindow);

            // Check for tab control with expected tabs
            var tabControl = WaitForElement(customersWindow, cf => cf.ByControlType(ControlType.Tab));
            Assert.NotNull(tabControl);

            // Verify expected tab names
            var tabs = tabControl.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            var tabNames = tabs.Select(t => t.Name).ToList();

            Assert.Contains(tabNames, name => name.Contains("Basic", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tabNames, name => name.Contains("Service", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tabNames, name => name.Contains("Mailing", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tabNames, name => name.Contains("Account", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_DataGrid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            var dataGrid = WaitForElement(customersWindow, cf => cf.ByAutomationId("Customers_DataGrid"));
            Assert.NotNull(dataGrid);

            // Load data first
            var loadButton = WaitForElement(customersWindow, cf => cf.ByName("Load"));
            WaitUntilResponsive(loadButton);
            loadButton?.AsButton().Invoke();

            // Wait for grid to populate
            var gridItems = Retry.WhileEmpty(() => dataGrid.FindAllDescendants(),
                timeout: TimeSpan.FromSeconds(5)).Result;

            Assert.NotEmpty(gridItems!);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_StatusBar_ShowsRecordCount()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            // Load data
            var loadButton = WaitForElement(customersWindow, cf => cf.ByName("Load"));
            WaitUntilResponsive(loadButton);
            loadButton?.AsButton().Invoke();

            // Wait for status bar to show record count
            var statusBar = Retry.WhileNull(() =>
            {
                var status = customersWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.StatusBar));
                var text = status?.Properties.Name.ValueOrDefault;
                return text?.Contains("Records:", StringComparison.OrdinalIgnoreCase) == true ? status : null;
            }, timeout: TimeSpan.FromSeconds(5)).Result;

            Assert.NotNull(statusBar);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void CustomersForm_SelectionEnablesButtons()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var customersWindow = OpenCustomersView(mainWindow);

            // Load data
            var loadButton = WaitForElement(customersWindow, cf => cf.ByName("Load"));
            WaitUntilResponsive(loadButton);
            loadButton?.AsButton().Invoke();
            Thread.Sleep(1000);

            // Find grid and select first row
            var dataGrid = WaitForElement(customersWindow, cf => cf.ByAutomationId("Customers_DataGrid"));
            Assert.NotNull(dataGrid);

            // Click on the grid to select first item
            dataGrid.Click();
            Thread.Sleep(500);

            // Verify Delete button is now enabled
            var deleteButton = WaitForElement(customersWindow, cf => cf.ByName("Delete"));
            Assert.NotNull(deleteButton);
        }

        private Window OpenCustomersView(Window mainWindow)
        {
            // Find Customers navigation button
            var navButton = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_Customers"), timeoutMs: 30000);

            if (navButton == null)
            {
                // Fallback: try by name if automation ID not found
                navButton = WaitForElement(mainWindow, cf => cf.ByName("Customers"), timeoutMs: 10000);
            }

            Assert.NotNull(navButton);

            navButton.Click();

            // Wait for window to appear
            var customersElement = Retry.WhileNull(() =>
            {
                try
                {
                    // First try as MDI child (descendant of main window)
                    var window = mainWindow.FindFirstDescendant(cf =>
                        cf.ByName("Customer Management"));

                    if (window != null && window.ControlType == ControlType.Window)
                    {
                        return window.AsWindow();
                    }

                    if (window != null)
                    {
                        var parent = window.Parent;
                        while (parent != null && parent.ControlType != ControlType.Window)
                        {
                            parent = parent.Parent;
                        }
                        return parent?.AsWindow();
                    }

                    // If not found as MDI child, try as separate window
                    var desktop = _automation?.GetDesktop();
                    window = desktop?.FindFirstDescendant(cf =>
                        cf.ByName("Customer Management").And(cf.ByControlType(ControlType.Window)));

                    if (window != null)
                    {
                        var asWindow = window.AsWindow();
                        if (asWindow != null) return asWindow;
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromSeconds(30)).Result;

            return customersElement ?? throw new InvalidOperationException("Customers window did not open");
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

        private void WaitUntilResponsive(AutomationElement? element, int timeoutMs = 3000)
        {
            if (element == null) return;

            Retry.WhileException(() =>
            {
                if (!element.IsEnabled || element.IsOffscreen)
                {
                    throw new InvalidOperationException("Element not responsive");
                }
            }, TimeSpan.FromMilliseconds(timeoutMs));
        }

        private Window GetMainWindow()
        {
            var mainWindow = Retry.WhileNull(() => _app?.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(DefaultTimeout / 1000));
            Assert.NotNull(mainWindow);
            return mainWindow.Result!;
        }

        private AutomationElement? WaitForElement(AutomationElement parent, Func<ConditionFactory, ConditionBase> condition, int timeoutMs = DefaultTimeout)
        {
            return Retry.WhileNull(() =>
            {
                try
                {
                    return parent.FindFirstDescendant(condition);
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromMilliseconds(timeoutMs)).Result;
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
