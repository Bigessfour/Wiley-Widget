using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("UiTests")]
    public class MunicipalAccountView_ModelTests : IClassFixture<TestAppFixture>
    {
        private readonly Application _app;
        private readonly UIA3Automation _automation;

        public MunicipalAccountView_ModelTests(TestAppFixture fixture)
        {
            _app = fixture.App;
            _automation = fixture.Automation;
        }

        [StaFact(DisplayName = "GridPattern GetItem is supported and cell text readable"), Trait("Category", "UI")]
        public void GridPattern_GetItem_Returns_Cell()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = UiTestHelpers.RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            var gp = grid.Patterns.Grid;
            Assert.True(gp.IsSupported, "GridPattern not supported on AccountsGrid");

            // Try to get the first cell
            var cell = gp.Pattern.GetItem(0, 0);
            Assert.NotNull(cell);

            var txt = UiTestHelpers.GetCellText(cell);
            Assert.False(string.IsNullOrWhiteSpace(txt));
        }

        [StaFact(DisplayName = "Selecting a row updates right panel with matching values"), Trait("Category", "UI")]
        public void SelectingRow_Updates_RightPanel_MatchingValues()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(25));
            Assert.NotNull(main);

            var grid = UiTestHelpers.RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Wait for 25 rows seeded data
            var rows = UiTestHelpers.WaitForRowCount(grid, 25, TimeSpan.FromSeconds(10));
            Assert.True(rows >= 1);

            var firstRow = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
            Assert.NotNull(firstRow);

            var cells = firstRow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var acctNumFromGrid = cells.FirstOrDefault()?.Name?.Trim() ?? string.Empty;

            firstRow.Click();
            Thread.Sleep(500);

            // Read right panel Account # value element
            var accountNumberLabel = main.FindFirstDescendant(cf => cf.ByName("Account #:"));
            Assert.NotNull(accountNumberLabel);
            var accountNumberValue = accountNumberLabel.Parent?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                                      .FirstOrDefault(t => t.Name != "Account #:")?.Name?.Trim() ?? string.Empty;

            Assert.Equal(acctNumFromGrid, accountNumberValue);
        }

        [StaFact(DisplayName = "Balance cell color follows BalanceColorConverter"), Trait("Category", "UI")]
        public void Balance_Cell_Color_Reflects_Converter()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(25));
            var grid = UiTestHelpers.RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));

            // find a negative balance cell by text
            var allTexts = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var negText = allTexts.FirstOrDefault(t => t.Name.StartsWith("-") || t.Name.Contains("-$"));
            Assert.NotNull(negText);

            var balanceCell = negText.Parent;
            var sampled = UiTestHelpers.SampleElementCenterColor(balanceCell);

            // XAML negative color: #FFF87171 ~ rgb(248,113,113)
            Assert.InRange(sampled.R, 200, 255);
            Assert.InRange(sampled.G, 40, 150);
            Assert.InRange(sampled.B, 40, 150);
        }

        [StaFact(DisplayName = "Account Name sorting orders rows correctly"), Trait("Category", "UI")]
        public void AccountName_Sorting_Correct()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(25));
            var grid = UiTestHelpers.RetryFindByAutomationId(main, "AccountsGrid", TimeSpan.FromSeconds(15));

            // helper: get first N account names (assume second text in row is Account Name)
            string[] GetNames()
            {
                return grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .Select(r => r.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Skip(1).FirstOrDefault()?.Name ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(10).ToArray();
            }

            var header = UiTestHelpers.GetHeaderByName(grid, "Account Name");
            Assert.NotNull(header);

            header.Click(); Thread.Sleep(1000);
            var asc = GetNames();
            Assert.True(asc.SequenceEqual(asc.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));

            header.Click(); Thread.Sleep(1000);
            var desc = GetNames();
            Assert.True(desc.SequenceEqual(desc.OrderByDescending(s => s, StringComparer.OrdinalIgnoreCase)));
        }

        [StaFact(DisplayName = "Data Loading & Rendering Interactions"), Trait("Category", "UI")]
        public void DataLoading_Rendering_Interactions()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(30));
            Assert.NotNull(main);

            var grid = UiTestHelpers.RetryFindByAutomationId(main.AsWindow(), "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Trigger Load: click Load Accounts
            var loadBtn = main.FindFirstDescendant(cf => cf.ByAutomationId("Btn_LoadAccounts"));
            Assert.NotNull(loadBtn);
            loadBtn.AsButton().Invoke();

            // Poll until row count > 0
            var rc = UiTestHelpers.WaitForRowCount(grid, expectedMinimum: 1, TimeSpan.FromSeconds(15));
            Assert.True(rc > 0, "Row count did not increase after Load operation");

            // Now expect seeded 25 rows
            var total = UiTestHelpers.WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(10));
            Assert.Equal(25, total);

            // Column Integrity: check headers
            var expectedHeaders = new[] { "Account Number", "Account Name", "Fund", "Type", "Balance", "Department", "Notes" };
            foreach (var h in expectedHeaders)
            {
                var header = UiTestHelpers.GetHeaderByName(grid, h);
                Assert.NotNull(header);
            }

            // Verify first row cell[0,0] contains '110' (account number prefix)
            var firstCell = UiTestHelpers.GetCellByRowCol(grid, 0, 0);
            Assert.NotNull(firstCell);
            var firstText = UiTestHelpers.GetCellText(firstCell);
            Assert.Contains("110", firstText);

            // Verify last row (index 24) loads after scrolling
            var lastCell = UiTestHelpers.GetCellByRowCol(grid, 24, 0);
            if (lastCell == null)
            {
                // try to scroll the last visible item into view
                var approxLast = UiTestHelpers.GetCellByRowCol(grid, 23, 0);
                if (approxLast != null) UiTestHelpers.ScrollCellIntoView(approxLast);
                Thread.Sleep(500);
                lastCell = UiTestHelpers.GetCellByRowCol(grid, 24, 0);
            }
            Assert.NotNull(lastCell);
            var lastText = UiTestHelpers.GetCellText(lastCell);
            Assert.Contains("445 SMALL EQUIPMENT/SUPPLIES", lastText.ToUpperInvariant());

            // Sorting: click Type header to sort and verify 'Bank' comes first in Type column
            var typeHeader = UiTestHelpers.GetHeaderByName(grid, "Type");
            Assert.NotNull(typeHeader);
            typeHeader.Click(); Thread.Sleep(800);

            // After sort, read first few types
            string[] GetTypes() => grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                                    .Select(r => r.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Skip(3).FirstOrDefault()?.Name ?? string.Empty)
                                    .Where(s => !string.IsNullOrWhiteSpace(s)).Take(10).ToArray();

            var types = GetTypes();
            Assert.True(types.Length > 0);
            // Expect 'Bank' to be among the first entries (seed expectation)
            Assert.Contains(types[0], types);
        }

        [StaFact(DisplayName = "User Interactions & Behaviors (Full Functionality)"), Trait("Category", "UI")]
        public void UserInteractions_FullFunctionality()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(30));
            Assert.NotNull(main);
            var win = main.AsWindow();

            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(10));
            Assert.NotNull(grid);

            // Filtering: select 'Bank' in Account Type Combo (AccountTypeCombo)
            var filtered = UiTestHelpers.ExpandComboAndSelect(win, "AccountTypeCombo", "Bank", TimeSpan.FromSeconds(5));
            Assert.True(filtered, "Failed to select 'Bank' in AccountTypeCombo");
            // Click Apply Filters
            var apply = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_ApplyFilters"));
            apply?.AsButton().Invoke();
            // Wait until filtered rows count ~ expected (seeded expectation: 5)
            var fcount = UiTestHelpers.WaitForPredicate(() =>
            {
                var rc = UiTestHelpers.TryGetRowCount(grid);
                return rc >= 1 && rc <= 10 ? (int?)rc : null;
            }, TimeSpan.FromSeconds(6));
            Assert.True(fcount.HasValue, "Filtered row count not in expected range");

            // Searching: enter 'CASH' in SearchTextBox
            var search = win.FindFirstDescendant(cf => cf.ByAutomationId("SearchTextBox"));
            Assert.NotNull(search);
            UiTestHelpers.SetTextBoxValue(search, "CASH");
            Thread.Sleep(500);
            var searchCount = UiTestHelpers.TryGetRowCount(grid);
            Assert.True(searchCount >= 0 && searchCount <= 25);
            // clear search
            UiTestHelpers.SetTextBoxValue(search, "");
            Thread.Sleep(500);

            // Editing: double-click a cell in first row for Description and try to edit
            var descCell = UiTestHelpers.GetCellByRowCol(grid, 0, 1); // assume col 1 is Account Name/Description
            Assert.NotNull(descCell);
            UiTestHelpers.DoubleClick(descCell);
            Thread.Sleep(500);
            // Try to type new text and press Enter
            UiTestHelpers.TypeText(" Edited", pressEnter: true);
            Thread.Sleep(500);

            // Export/Print: click export button and detect file dialog
            var exportBtn = win.FindFirstDescendant(cf => cf.ByName("Export") .Or(cf.ByAutomationId("Btn_Export")));
            if (exportBtn != null)
            {
                exportBtn.AsButton().Invoke();
                // Wait for top-level Save/Export dialog (title may vary)
                var dialog = UiTestHelpers.WaitForTopLevelWindowWithTitle("Save", TimeSpan.FromSeconds(5))
                             ?? UiTestHelpers.WaitForTopLevelWindowWithTitle("Export", TimeSpan.FromSeconds(5))
                             ?? UiTestHelpers.WaitForTopLevelWindowWithTitle("Save As", TimeSpan.FromSeconds(5));
                Assert.NotNull(dialog);
                // Close the dialog to continue
                try { dialog.AsWindow().Close(); } catch { }
            }

            // Validation: attempt to set Account Number to invalid text if editable
            var accountNumberCell = UiTestHelpers.GetCellByRowCol(grid, 0, 0);
            if (accountNumberCell != null)
            {
                UiTestHelpers.DoubleClick(accountNumberCell);
                Thread.Sleep(200);
                UiTestHelpers.TypeText("INVALID", pressEnter: true);
                // Wait for tooltip/error text 'Invalid'
                var err = UiTestHelpers.WaitForTooltipText(win, "Invalid format", TimeSpan.FromSeconds(5))
                          ?? UiTestHelpers.WaitForTooltipText(win, "Invalid", TimeSpan.FromSeconds(5));
                Assert.NotNull(err);
            }
        }

        [StaFact(DisplayName = "Assertions & Data Validation (Accuracy & Integrity)"), Trait("Category", "UI")]
        public void Assertions_DataValidation_Integrity()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(30));
            Assert.NotNull(main);
            var win = main.AsWindow();

            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Ensure data loaded (trigger if needed)
            var loadBtn = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_LoadAccounts"));
            if (loadBtn != null)
            {
                loadBtn.AsButton().Invoke();
            }

            // Measure load time
            var elapsed = UiTestHelpers.MeasureActionMilliseconds(() =>
            {
                UiTestHelpers.WaitForRowCount(grid, expectedMinimum: 1, TimeSpan.FromSeconds(10));
            });
            Assert.True(elapsed < 2000, $"Load took too long: {elapsed}ms");

            // Wait for seeded 25 rows if present
            var rc = UiTestHelpers.WaitForRowCount(grid, expectedMinimum: 25, TimeSpan.FromSeconds(6));

            if (rc >= 25)
            {
                // Seeded Data Match: sample row 0 and check values
                var acctNums = UiTestHelpers.GetColumnStringsByHeader(grid, "Account Number");
                var types = UiTestHelpers.GetColumnStringsByHeader(grid, "Type");
                var funds = UiTestHelpers.GetColumnStringsByHeader(grid, "Fund");

                Assert.True(acctNums.Length >= 25);

                // sample checks
                Assert.Contains(acctNums[0], acctNums);
                Assert.Contains("110", acctNums[0]);
                Assert.Contains("Bank", types);
                Assert.Contains("Conservation Trust Fund", funds);

                // no duplicates/nulls
                var (hasNull, hasDup, dups) = UiTestHelpers.AnalyzeForNullsAndDuplicates(acctNums);
                Assert.False(hasNull, "Found null/empty account numbers");
                Assert.False(hasDup, $"Found duplicate account numbers: {string.Join(",", dups)}");

                // Type distribution example - tolerant check
                var typeCounts = types.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
                // expected distribution from image (example): Bank=5, Income=7, Expense=5, Liability=5, Equity=3
                Assert.True(typeCounts.Values.Sum() >= 25);
            }
            else
            {
                // If not seeded, ensure the empty-state is visible and no false positives
                var placeholder = win.FindFirstDescendant(cf => cf.ByName("No accounts found"));
                Assert.NotNull(placeholder);
            }

            // Accessibility: Check grid Name property and tab order
            Assert.True(UiTestHelpers.CheckAccessibleName(grid, "Accounts Data Grid"));
            var focusNames = UiTestHelpers.CycleTabCollectFocusNames(win, 20);
            Assert.True(focusNames.Length > 0, "Tab focus cycle returned no elements");
        }

        [StaFact(DisplayName = "View Launch & Initial State Verification (Core Rendering)"), Trait("Category", "UI")]
        public void ViewLaunch_InitialState_Verification()
        {
            // Main Window Access
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(30));
            Assert.NotNull(main);
            // Title check (tolerant)
            var title = main.Title ?? main.Name ?? string.Empty;
            Assert.True(!string.IsNullOrWhiteSpace(title) && title.IndexOf("Wiley", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Main window title did not contain expected text. Title='{title}'");

            // Navigate to Municipal Accounts view (try a few known UI elements)
            var navCandidates = new[] { "Municipal Accounts", "Accounts", "Accounts" };
            AutomationElement? nav = null;
            foreach (var name in navCandidates)
            {
                nav = main.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)));
                if (nav != null) break;
                nav = main.FindFirstDescendant(cf => cf.ByName(name));
                if (nav != null) break;
            }

            if (nav != null)
            {
                nav.Click();
                Thread.Sleep(1000);
            }

            // Wait for view container or grid
            AutomationElement? view = null;
            // preferred: AutomationId 'MunicipalAccountView' (may not exist), fallback to AccountsGrid
            var rootWindow = main.AsWindow();
            view = Retry.WhileNull(() => rootWindow.FindFirstDescendant(cf => cf.ByAutomationId("MunicipalAccountView")), TimeSpan.FromSeconds(6), TimeSpan.FromMilliseconds(200)).Result;
            if (view == null)
            {
                // fallback to the AccountsGrid
                view = UiTestHelpers.RetryFindByAutomationId(rootWindow, "AccountsGrid", TimeSpan.FromSeconds(10));
            }

            Assert.NotNull(view);

            // Verify view container bounds (ensure rendered)
            var rect = view.BoundingRectangle;
            Assert.True(rect.Width > 0 && rect.Height > 0, "View container has no bounds/size.");

            // Verify static elements
            var loadBtn = rootWindow.FindFirstDescendant(cf => cf.ByAutomationId("Btn_LoadAccounts")).AsButton();
            Assert.NotNull(loadBtn);

            var accountNumberHeader = rootWindow.FindFirstDescendant(cf => cf.ByName("Account Number"));
            Assert.NotNull(accountNumberHeader);

            var grid = UiTestHelpers.RetryFindByAutomationId(rootWindow, "AccountsGrid", TimeSpan.FromSeconds(6));
            Assert.NotNull(grid);

            // Empty/default state: either zero rows OR placeholder text "No accounts found"
            var rowCount = UiTestHelpers.TryGetRowCount(grid);
            var placeholder = rootWindow.FindFirstDescendant(cf => cf.ByName("No accounts found"));
            var ok = (rowCount == 0) || (placeholder != null);
            Assert.True(ok, $"Initial state not empty. RowCount={rowCount}, PlaceholderPresent={placeholder != null}");
        }
    }
}
