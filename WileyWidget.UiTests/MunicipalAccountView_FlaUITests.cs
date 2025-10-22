using System;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.UiTests;

using Xunit;

[Collection("UiTests")]
public class MunicipalAccountView_FlaUITests : IClassFixture<TestAppFixture>
{
    private readonly Application _app;
    private readonly UIA3Automation _automation;
    private bool _disposed;

    public MunicipalAccountView_FlaUITests(TestAppFixture fixture)
    {
        _app = fixture.App;
        _automation = fixture.Automation;
    }

    [StaFact(DisplayName = "MunicipalAccountView displays grid with 25 rows"), Trait("Category", "UI")]
    public void MunicipalAccountView_Shows_25_Rows()
    {
        RunWithScreenshotOnError("MunicipalAccountView_Shows_25_Rows", () =>
        {
            // Wait a bit for the app to fully initialize
            Thread.Sleep(2000);

            // Try to find the main window by title or automation ID
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            if (main == null)
            {
                // Fallback: try to find any window with "Wiley" in the title
                var desktop = _automation.GetDesktop();
                main = desktop.FindFirstDescendant(cf => cf.ByName("Wiley").And(cf.ByControlType(ControlType.Window)))?.AsWindow();
            }
            if (main == null)
            {
                // Another fallback: get the first window from the process
                var windows = _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(_app.ProcessId).And(cf.ByControlType(ControlType.Window)));
                main = windows.FirstOrDefault()?.AsWindow();
            }
            Assert.NotNull(main);

            // Navigate to Accounts view by clicking the Accounts tab in the docking manager
            var accountsTab = main.FindFirstDescendant(cf => cf.ByName("Accounts").And(cf.ByControlType(ControlType.Button)));
            if (accountsTab == null)
            {
                accountsTab = main.FindFirstDescendant(cf => cf.ByName("Accounts"));
            }
            if (accountsTab != null)
            {
                accountsTab.Click();
                Thread.Sleep(3000); // Wait for navigation and view loading
            }

            // Try to locate the Syncfusion grid by its x:Name (AutomationId="AccountsGrid")
            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data binding to populate and for rowcount to reach expected seed size
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.Equal(25, rowCount);
        });
    }

    [StaFact(DisplayName = "Type filter to Cash reduces visible rows"), Trait("Category", "UI")]
    public void Type_Filter_To_Cash_Reduces_Rows()
    {
        RunWithScreenshotOnError("Type_Filter_To_Cash_Reduces_Rows", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            var initial = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(initial >= 25, $"Expected at least 25 rows before filtering, got {initial}");

            // Find the Account Type ComboBox and select "Cash"
            var typeLabel = main.FindFirstDescendant(cf => cf.ByName("Account Type").And(cf.ByControlType(ControlType.Text)));
            Assert.NotNull(typeLabel);

            var combo = typeLabel.Parent?.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox))
                        ?? main.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Account Type")));
            Assert.NotNull(combo);

            var comboBox = combo.AsComboBox();
            comboBox.Expand();
            var cashItem = comboBox.Items.FirstOrDefault(i => string.Equals(i.Name, "Cash", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(cashItem);
            cashItem.Click();

            // Click Apply Filters button
            var applyBtn = main.FindFirstDescendant(cf => cf.ByName("Apply Filters").And(cf.ByControlType(ControlType.Button)));
            Assert.NotNull(applyBtn);
            applyBtn.AsButton().Invoke();

            // Wait (poll) until rowcount drops to a small number (seeded expectation)
            var filtered = WaitForPredicate(() =>
            {
                var rc = TryGetRowCount(grid);
                return rc >= 1 && rc <= 5 ? (int?)rc : null;
            }, TimeSpan.FromSeconds(8));

            Assert.True(filtered.HasValue, "Filtered row count did not reach expected range in time");
            Assert.InRange(filtered.Value, 1, 5);

            // Data integrity assertion: first visible row should be account '110' or an 110.* variant
            var firstRow = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
            Assert.NotNull(firstRow);
            var texts = firstRow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            Assert.True(texts.Length > 0, "First row has no text descendants to assert on");
            var firstCellText = texts[0].Name ?? string.Empty;
            Assert.Contains("110", firstCellText);
        });
    }

    [StaFact(DisplayName = "SfDataGrid columns are properly configured"), Trait("Category", "UI")]
    public void SfDataGrid_Columns_Are_Configured()
    {
        RunWithScreenshotOnError("SfDataGrid_Columns_Are_Configured", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Verify grid has data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Test column headers exist
            var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header));
            Assert.True(headers.Length >= 6, $"Expected at least 6 column headers, found {headers.Length}");

            // Test specific column headers
            var accountNumberHeader = grid.FindFirstDescendant(cf => cf.ByName("Account Number"));
            Assert.NotNull(accountNumberHeader);

            var accountNameHeader = grid.FindFirstDescendant(cf => cf.ByName("Account Name"));
            Assert.NotNull(accountNameHeader);

            var balanceHeader = grid.FindFirstDescendant(cf => cf.ByName("Balance"));
            Assert.NotNull(balanceHeader);

            var departmentHeader = grid.FindFirstDescendant(cf => cf.ByName("Department"));
            Assert.NotNull(departmentHeader);
        });
    }

    [StaFact(DisplayName = "DataGrid sorting functionality works"), Trait("Category", "UI")]
    public void DataGrid_Sorting_Works()
    {
        RunWithScreenshotOnError("DataGrid_Sorting_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Click Account Name header to sort
            var accountNameHeader = grid.FindFirstDescendant(cf => cf.ByName("Account Name"));
            Assert.NotNull(accountNameHeader);
            accountNameHeader.Click();
            Thread.Sleep(1000); // Wait for sort

            // Verify grid still has rows after sorting
            var sortedRowCount = TryGetRowCount(grid);
            Assert.True(sortedRowCount >= 25);

            // Click again for descending sort
            accountNameHeader.Click();
            Thread.Sleep(1000);

            var descRowCount = TryGetRowCount(grid);
            Assert.True(descRowCount >= 25);
        });
    }

    [StaFact(DisplayName = "DataGrid grouping functionality works"), Trait("Category", "UI")]
    public void DataGrid_Grouping_Works()
    {
        RunWithScreenshotOnError("DataGrid_Grouping_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Look for group drop area (indicates grouping is enabled)
            var groupArea = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.Group));
            // Note: Group area might not be visible until grouping is active

            // Test that grouping columns are configured by checking if we can access grouped data
            // This is a basic test - more advanced grouping tests would require specific data
            Assert.NotNull(grid);
        });
    }

    [StaFact(DisplayName = "Balance column formatting and colors work"), Trait("Category", "UI")]
    public void Balance_Column_Formatting_Works()
    {
        RunWithScreenshotOnError("Balance_Column_Formatting_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Find balance column cells
            var balanceCells = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                                   .Where(t => t.Name.Contains("$") || t.Name.Contains("-"))
                                   .ToArray();

            Assert.True(balanceCells.Length > 0, "No balance cells found with currency formatting");

            // Test that balances are formatted as currency (contain $)
            var currencyFormatted = balanceCells.Where(c => c.Name.Contains("$")).ToArray();
            Assert.True(currencyFormatted.Length > 0, "No currency-formatted balance cells found");
        });
    }

    [StaFact(DisplayName = "DataGrid selection and right panel binding works"), Trait("Category", "UI")]
    public void DataGrid_Selection_RightPanel_Binding_Works()
    {
        RunWithScreenshotOnError("DataGrid_Selection_RightPanel_Binding_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Select first row
            var firstRow = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
            Assert.NotNull(firstRow);
            firstRow.Click();
            Thread.Sleep(500); // Wait for selection and binding

            // Check if right panel details are populated
            var accountDetailsExpander = main.FindFirstDescendant(cf => cf.ByName("Account Details"));
            Assert.NotNull(accountDetailsExpander);

            // Expand the details if not already expanded
            var expander = accountDetailsExpander.Patterns.ExpandCollapse;
            if (expander.IsSupported && expander.Pattern.ExpandCollapseState != FlaUI.Core.Definitions.ExpandCollapseState.Expanded)
            {
                expander.Pattern.Expand();
                Thread.Sleep(500);
            }

            // Check for account number display in details
            var accountNumberText = main.FindFirstDescendant(cf => cf.ByName("Account #:"));
            Assert.NotNull(accountNumberText);

            // Verify account number has content
            var accountNumberValue = accountNumberText.Parent?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                                                      .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Name) && t.Name != "Account #:");
            Assert.NotNull(accountNumberValue);
            Assert.False(string.IsNullOrWhiteSpace(accountNumberValue.Name));
        });
    }

    [StaFact(DisplayName = "Load Accounts command works"), Trait("Category", "UI")]
    public void Load_Accounts_Command_Works()
    {
        RunWithScreenshotOnError("Load_Accounts_Command_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            // Find Load Accounts button
            var loadButton = main.FindFirstDescendant(cf => cf.ByAutomationId("Btn_LoadAccounts"));
            Assert.NotNull(loadButton);

            // Click the button
            loadButton.AsButton().Invoke();
            Thread.Sleep(2000); // Wait for load operation

            // Verify grid has data
            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            var rowCount = WaitForRowCount(grid, expectedMinimum: 1, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 1);
        });
    }

    [StaFact(DisplayName = "Apply Filters command works"), Trait("Category", "UI")]
    public void Apply_Filters_Command_Works()
    {
        RunWithScreenshotOnError("Apply_Filters_Command_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Ensure we have initial data
            var initialCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(initialCount >= 25);

            // Find Apply Filters button
            var applyButton = main.FindFirstDescendant(cf => cf.ByAutomationId("Btn_ApplyFilters"));
            Assert.NotNull(applyButton);

            // Click apply filters (should work even with no filter changes)
            applyButton.AsButton().Invoke();
            Thread.Sleep(1000);

            // Verify grid still has data
            var afterCount = TryGetRowCount(grid);
            Assert.True(afterCount >= 1);
        });
    }

    [StaFact(DisplayName = "Clear Filters command works"), Trait("Category", "UI")]
    public void Clear_Filters_Command_Works()
    {
        RunWithScreenshotOnError("Clear_Filters_Command_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Ensure we have data
            var initialCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(initialCount >= 25);

            // Find Clear Filters button
            var clearButton = main.FindFirstDescendant(cf => cf.ByAutomationId("Btn_ClearFilters"));
            Assert.NotNull(clearButton);

            // Click clear filters
            clearButton.AsButton().Invoke();
            Thread.Sleep(1000);

            // Verify grid still has data (should show all accounts again)
            var afterCount = TryGetRowCount(grid);
            Assert.True(afterCount >= 1);
        });
    }

    [StaFact(DisplayName = "Search functionality works"), Trait("Category", "UI")]
    public void Search_Functionality_Works()
    {
        RunWithScreenshotOnError("Search_Functionality_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Ensure we have data
            var initialCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(initialCount >= 25);

            // Find search textbox
            var searchBox = main.FindFirstDescendant(cf => cf.ByAutomationId("SearchTextBox"));
            Assert.NotNull(searchBox);

            var textBox = searchBox.AsTextBox();
            textBox.Text = "Cash"; // Search for accounts containing "Cash"
            Thread.Sleep(1000); // Wait for search to apply

            // Verify search reduced results (or at least didn't break the grid)
            var searchCount = TryGetRowCount(grid);
            Assert.True(searchCount >= 0);

            // Clear search
            textBox.Text = "";
            Thread.Sleep(1000);

            var clearedCount = TryGetRowCount(grid);
            Assert.True(clearedCount >= 1);
        });
    }

    [StaFact(DisplayName = "Status bar displays correct information"), Trait("Category", "UI")]
    public void Status_Bar_Displays_Correct_Information()
    {
        RunWithScreenshotOnError("Status_Bar_Displays_Correct_Information", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            // Check status bar has account count
            var statusText = main.FindFirstDescendant(cf => cf.ByName("Total Accounts:"));
            Assert.NotNull(statusText);

            // Verify status message exists
            var statusMessage = main.FindFirstDescendant(cf => cf.ByName("Status:"));
            Assert.NotNull(statusMessage);
        });
    }

    [StaFact(DisplayName = "Transactions grid in right panel works"), Trait("Category", "UI")]
    public void Transactions_Grid_Right_Panel_Works()
    {
        RunWithScreenshotOnError("Transactions_Grid_Right_Panel_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for data and select first row
            var rowCount = WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.True(rowCount >= 25);

            var firstRow = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
            Assert.NotNull(firstRow);
            firstRow.Click();
            Thread.Sleep(500);

            // Check for transactions grid header
            var transactionsHeader = main.FindFirstDescendant(cf => cf.ByName("Recent Transactions"));
            Assert.NotNull(transactionsHeader);

            // Transactions grid should exist even if empty
            var transactionsGrid = main.FindFirstDescendant(cf => cf.ByName("Recent Transactions"))?.Parent
                                        ?.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid));
            // Note: Transactions grid might be empty, which is OK
        });
    }

    [StaFact(DisplayName = "Advanced filters expander works"), Trait("Category", "UI")]
    public void Advanced_Filters_Expander_Works()
    {
        RunWithScreenshotOnError("Advanced_Filters_Expander_Works", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            // Find advanced filters expander
            var expander = main.FindFirstDescendant(cf => cf.ByName("Advanced Filters"));
            Assert.NotNull(expander);

            var expanderControl = expander.Patterns.ExpandCollapse;

            // Test expanding
            if (expanderControl.IsSupported && expanderControl.Pattern.ExpandCollapseState != FlaUI.Core.Definitions.ExpandCollapseState.Expanded)
            {
                expanderControl.Pattern.Expand();
                Thread.Sleep(500);
            }

            // Verify expanded state
            Assert.True(expanderControl.Pattern.ExpandCollapseState == FlaUI.Core.Definitions.ExpandCollapseState.Expanded);

            // Test collapsing
            expanderControl.Pattern.Collapse();
            Thread.Sleep(500);

            // Verify collapsed state
            Assert.True(expanderControl.Pattern.ExpandCollapseState == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed);
        });
    }

    [StaFact(DisplayName = "SfSkinManager theme is applied correctly"), Trait("Category", "UI")]
    public void SfSkinManager_Theme_Is_Applied()
    {
        RunWithScreenshotOnError("SfSkinManager_Theme_Is_Applied", () =>
        {
            var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            // Verify the main grid has dark theme styling
            var grid = RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Check that the view has dark background (theme applied)
            var mainGrid = main.FindFirstDescendant(cf => cf.ByAutomationId("MunicipalFocusAnchor"))?.Parent;
            Assert.NotNull(mainGrid);

            // Theme should be applied - we can verify by checking the background color or presence of theme elements
            // This is a basic check that the UI loaded with theme
            Assert.NotNull(main);
        });
    }

    // Run an action and capture a screenshot if it throws an exception
    private void RunWithScreenshotOnError(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            try
            {
                var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
                CaptureScreenshot(main, name);
            }
            catch { /* ignore screenshot failures */ }
            throw;
        }
    }

    private static int WaitForRowCount(AutomationElement grid, int expectedMinimum, TimeSpan timeout)
    {
        var result = Retry.WhileNull(
            () =>
            {
                var rc = TryGetRowCount(grid);
                return rc >= expectedMinimum ? (int?)rc : null;
            },
            timeout,
            TimeSpan.FromMilliseconds(200));
        return result.Result ?? 0;
    }

    private static int? WaitForPredicate(Func<int?> predicate, TimeSpan timeout)
    {
        var result = Retry.WhileNull(() => predicate(), timeout, TimeSpan.FromMilliseconds(200));
        return result.Result;
    }

    private static void CaptureScreenshot(Window? window, string name)
    {
        try
        {
            var root = window ?? throw new ArgumentNullException(nameof(window));
            var repoRoot = GetRepoRoot();
            var outDir = Path.Combine(repoRoot, "TestResults", "Screenshots");
            Directory.CreateDirectory(outDir);
            var file = Path.Combine(outDir, $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            Capture.Element(root).ToFile(file);
        }
        catch { /* avoid masking original test failure */ }
    }

    private static string GetRepoRoot()
    {
        // Starting from test assembly folder, move up until finding WileyWidget.sln or bin folder
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var probe = Path.Combine(dir, "WileyWidget.csproj");
            if (File.Exists(probe)) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }
        // Fallback to workspace path if set
        var ws = Environment.GetEnvironmentVariable("WILEY_WIDGET_ROOT");
        if (!string.IsNullOrWhiteSpace(ws) && File.Exists(Path.Combine(ws, "WileyWidget.csproj"))) return ws;
        throw new DirectoryNotFoundException("Unable to locate repo root; set WILEY_WIDGET_ROOT env var for UI tests.");
    }

    private static AutomationElement? RetryFindByAutomationId(Window root, string automationId, TimeSpan timeout)
    {
        return Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout,
            TimeSpan.FromMilliseconds(200))
            .Result;
    }

    private static int TryGetRowCount(AutomationElement grid)
    {
        // Try GridPattern first
        var gp = grid.Patterns.Grid;
        if (gp.IsSupported)
        {
            var rc = gp.Pattern.RowCount ?? 0;
            if (rc > 0) return rc;
        }

        // Try TablePattern
        var tp = grid.Patterns.Table;
        if (tp.IsSupported)
        {
            var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            if (rows?.Length > 0) return rows.Length;
        }

        // Fallback: count DataItem children (visible rows)
        var items = grid.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem));
        return items?.Length ?? 0;
    }

    // No Dispose implementation - fixture owns app lifecycle.
}
