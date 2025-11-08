using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;

namespace WileyWidget.Tests.E2E
{
    public class SampleWpfE2ETests : BaseE2ETest
    {
        [Fact]
        public void CanLoadMainWindowAndInteractWithSyncfusionControl()
        {
            // Assert main window loads
            MainWindow.Title.Should().Contain("Wiley Widget"); // Adjust to your app's title

            // Find and interact with a Syncfusion control (e.g., SfButton with AutomationId)
            var button = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("submitButton"))?.AsButton();
            button.Should().NotBeNull("Submit button should exist");
            button.Click();

            // Verify outcome (e.g., a label updates after click)
            var resultLabel = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("resultLabel"))?.AsLabel();
            resultLabel.Text.Should().Be("Success"); // Adjust expected text
        }

        [Fact]
        public void CanFillAndValidateSyncfusionGrid()
        {
            // Example for a Syncfusion SfDataGrid
            var grid = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dataGrid"))?.AsGrid();
            grid.Should().NotBeNull("Data grid should exist");

            // Simulate adding a row or editing (using patterns)
            // grid.Rows[0].Cells[0].Value = "Test Value"; // Use ValuePattern if supported
            // Assert changes
        }
    }
}
