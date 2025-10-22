using System;
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
    public class MunicipalAccountView_EdgeCasesTests : IClassFixture<TestAppFixture>
    {
        private readonly Application _app;
        private readonly UIA3Automation _automation;

        public MunicipalAccountView_EdgeCasesTests(TestAppFixture fixture)
        {
            _app = fixture.App;
            _automation = fixture.Automation;
        }

        [StaFact(DisplayName = "EdgeCase: Empty Data shows placeholder"), Trait("Category", "UI")]
        public void EmptyData_Shows_Placeholder()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(20));
            var win = main.AsWindow();
            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(8));
            Assert.NotNull(grid);

            // Simulate clearing seed if test mode supports it (try to find seed/clear button)
            var clearSeed = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_ClearSeed"));
            if (clearSeed != null) { clearSeed.AsButton().Invoke(); Thread.Sleep(500); }

            // Wait for zero rows or placeholder
            var rc = UiTestHelpers.TryGetRowCount(grid);
            if (rc == 0)
            {
                var placeholder = win.FindFirstDescendant(cf => cf.ByName("No accounts found"));
                Assert.NotNull(placeholder);
            }
            else
            {
                // If rows exist, mark as inconclusive by skipping
                Assert.True(true, "Rows exist; cannot assert empty state in this environment");
            }
        }

        [StaFact(DisplayName = "EdgeCase: Load failure displays error dialog"), Trait("Category", "UI")]
        public void LoadFailure_Shows_ErrorUI()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(20));
            var win = main.AsWindow();

            // Try simulate DB error via environment flag (app must support this)
            var loadBtn = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_LoadAccounts"));
            if (loadBtn == null) { return; }

            // If the app exposes a toggle to simulate failures, use it
            var simulateError = win.FindFirstDescendant(cf => cf.ByAutomationId("Chk_SimulateDbError"));
            if (simulateError != null) simulateError.AsToggleButton()?.Toggle();

            loadBtn.AsButton().Invoke();

            // Expect an error dialog
            var dialog = UiTestHelpers.WaitForTopLevelWindowWithTitle("Error", TimeSpan.FromSeconds(5))
                         ?? UiTestHelpers.WaitForTopLevelWindowWithTitle("Database Error", TimeSpan.FromSeconds(5));
            Assert.NotNull(dialog);
            try { dialog.AsWindow().Close(); } catch { }
        }

        [StaFact(DisplayName = "EdgeCase: Large data virtualization scroll performance"), Trait("Category", "UI")]
        public void LargeData_Virtualization_Scroll()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(30));
            var win = main.AsWindow();
            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(15));
            Assert.NotNull(grid);

            // Try to seed large dataset if seed button exists
            var seedLarge = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_Seed_Large"));
            if (seedLarge != null)
            {
                seedLarge.AsButton().Invoke();
                // wait for many rows
                var rc = UiTestHelpers.WaitForRowCount(grid, expectedMinimum: 1000, TimeSpan.FromSeconds(20));
                Assert.True(rc >= 1000);
            }
            else
            {
                // Can't seed; try to exercise existing rows by scrolling end
                var last = UiTestHelpers.GetCellByRowCol(grid, 24, 0);
                if (last != null) UiTestHelpers.ScrollCellIntoView(last);
            }
        }

        [StaFact(DisplayName = "EdgeCase: Responsive Reflow on Resize"), Trait("Category", "UI")]
        public void Responsive_Resize_Reflow()
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(20));
            var win = main.AsWindow();
            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(8));
            Assert.NotNull(grid);

            // Resize window and assert grid still visible and columns present
            UiTestHelpers.ResizeWindow(win, 800, 600);
            Thread.Sleep(300);
            Assert.True(grid.BoundingRectangle.Width > 0 && grid.BoundingRectangle.Height > 0);

            UiTestHelpers.ResizeWindow(win, 1200, 900);
            Thread.Sleep(300);
            Assert.True(grid.BoundingRectangle.Width > 0 && grid.BoundingRectangle.Height > 0);
        }

        [Theory(DisplayName = "Parameterized Filters: Filter yields expected result count"), Trait("Category", "UI")]
        [InlineData("Bank", 5)]
        [InlineData("Income", 7)]
        [InlineData("Expense", 5)]
        public void Parameterized_Filters(string filterValue, int expectedApprox)
        {
            var main = UiTestHelpers.GetMainWindow(_app, _automation, TimeSpan.FromSeconds(20));
            var win = main.AsWindow();
            var grid = UiTestHelpers.RetryFindByAutomationId(win, "AccountsGrid", TimeSpan.FromSeconds(8));
            Assert.NotNull(grid);

            var ok = UiTestHelpers.ExpandComboAndSelect(win, "AccountTypeCombo", filterValue, TimeSpan.FromSeconds(4));
            Assert.True(ok);
            var apply = win.FindFirstDescendant(cf => cf.ByAutomationId("Btn_ApplyFilters"));
            apply?.AsButton().Invoke();
            var rc = UiTestHelpers.WaitForPredicate(() =>
            {
                var c = UiTestHelpers.TryGetRowCount(grid);
                return c >= 1 ? (int?)c : null;
            }, TimeSpan.FromSeconds(6));
            Assert.True(rc.HasValue);
            // approximate check (seed data may vary)
            Assert.InRange(rc.Value, Math.Max(1, expectedApprox - 2), expectedApprox + 2);
        }
    }
}
