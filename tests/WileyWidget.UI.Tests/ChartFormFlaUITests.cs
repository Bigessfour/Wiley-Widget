using FluentAssertions;
using WileyWidget.UI.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// FlaUI-based UI automation tests for ChartForm.
/// Tests verify chart controls, toolbar, summary panel, and export functionality.
/// </summary>
[Trait("Category", "UI")]
[Trait("Category", "FlaUI")]
[Collection("UITests")]
public class ChartFormFlaUITests : FlaUITestBase
{
    /// <summary>
    /// Navigate to the Chart form from the main form
    /// </summary>
    private void NavigateToChartForm()
    {
        LaunchApplication();
        WaitForApplicationReady();

        // Open Chart form via menu
        ClickMenuItem("File", "Charts");

        // Wait for Chart form to appear
        var chartWindow = WaitForWindow("Budget Analytics", 10000);
        chartWindow.Should().NotBeNull("Chart form should open");
    }

    /// <summary>
    /// Verify Chart form has the correct title
    /// </summary>
    [Fact]
    public void ChartForm_OnOpen_HasCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");
                chartWindow.Should().NotBeNull();
                chartWindow!.Title.Should().Contain("Budget Analytics");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Year selector is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasYearSelector()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Year label
                var yearLabel = FindElementByName("Year:", chartWindow, 5000);
                yearLabel.Should().NotBeNull("Year selector should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Category filter is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasCategoryFilter()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Category label
                var categoryLabel = FindElementByName("Category:", chartWindow, 5000);
                categoryLabel.Should().NotBeNull("Category filter should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Chart type selector is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasChartTypeSelector()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Chart label
                var chartLabel = FindElementByName("Chart:", chartWindow, 5000);
                chartLabel.Should().NotBeNull("Chart type selector should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Refresh Data button is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasRefreshButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Refresh Data button
                var refreshButton = FindElementByName("Refresh Data", chartWindow, 5000);
                refreshButton.Should().NotBeNull("Refresh Data button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Export to PDF button is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasExportButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Export to PDF button
                var exportButton = FindElementByName("Export to PDF", chartWindow, 5000);
                exportButton.Should().NotBeNull("Export to PDF button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Print button is present
    /// </summary>
    [Fact]
    public void ChartForm_Toolbar_HasPrintButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Print button
                var printButton = FindElementByName("Print", chartWindow, 5000);
                printButton.Should().NotBeNull("Print button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Revenue Trends chart group is present
    /// </summary>
    [Fact]
    public void ChartForm_ChartPanel_HasRevenueTrendsGroup()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Revenue Trends group box
                var revenueTrendsGroup = FindElementByName("Revenue Trends", chartWindow, 5000);
                revenueTrendsGroup.Should().NotBeNull("Revenue Trends chart group should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Expenditure Breakdown chart group is present
    /// </summary>
    [Fact]
    public void ChartForm_ChartPanel_HasExpenditureBreakdownGroup()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Expenditure Breakdown group box
                var expenditureGroup = FindElementByName("Expenditure Breakdown", chartWindow, 5000);
                expenditureGroup.Should().NotBeNull("Expenditure Breakdown chart group should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Cumulative Budget chart group is present
    /// </summary>
    [Fact]
    public void ChartForm_ChartPanel_HasCumulativeBudgetGroup()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Cumulative Budget group box
                var cumulativeGroup = FindElementByName("Cumulative Budget", chartWindow, 5000);
                cumulativeGroup.Should().NotBeNull("Cumulative Budget chart group should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Budget Proportions chart group is present
    /// </summary>
    [Fact]
    public void ChartForm_ChartPanel_HasBudgetProportionsGroup()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Budget Proportions group box
                var proportionsGroup = FindElementByName("Budget Proportions", chartWindow, 5000);
                proportionsGroup.Should().NotBeNull("Budget Proportions chart group should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Budget Summary panel is present
    /// </summary>
    [Fact]
    public void ChartForm_SummaryPanel_IsPresent()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Budget Summary header
                var summaryHeader = FindElementByName("📊 Budget Summary", chartWindow, 5000);
                summaryHeader.Should().NotBeNull("Budget Summary panel should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Refresh Data button refreshes the charts
    /// </summary>
    [Fact]
    public void ChartForm_ClickRefreshData_RefreshesCharts()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Click Refresh Data button
                var refreshClicked = ClickButton("Refresh Data", chartWindow);
                refreshClicked.Should().BeTrue("Refresh Data button should be clickable");

                // Wait for refresh to complete using condition-based wait
                var refreshComplete = WaitForDataLoaded(parent: chartWindow, timeout: TimeSpan.FromSeconds(15));
                refreshComplete.Should().BeTrue("Charts should refresh after clicking Refresh Data");

                // Verify the application is still running (no crash)
                IsApplicationLaunched.Should().BeTrue("Application should still be running after refresh");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the status bar shows data loaded message
    /// </summary>
    [Fact]
    public void ChartForm_StatusBar_ShowsDataLoadedMessage()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();

                // Wait for charts to load
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Wait for status bar to show data info
                var hasDataInfo = WaitForStatusText("data", chartWindow, 10000) ||
                                  WaitForStatusText("loaded", chartWindow, 5000) ||
                                  WaitForStatusText("Data Points", chartWindow, 5000);

                hasDataInfo.Should().BeTrue("Status bar should display data loading status");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Summary panel shows Total Transactions metric
    /// </summary>
    [Fact]
    public void ChartForm_SummaryPanel_ShowsTotalTransactions()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Total Transactions label
                var totalTransactionsLabel = FindElementByName("Total Transactions", chartWindow, 5000);
                totalTransactionsLabel.Should().NotBeNull("Total Transactions metric should be present in summary panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Summary panel shows Budget Variance metric
    /// </summary>
    [Fact]
    public void ChartForm_SummaryPanel_ShowsBudgetVariance()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for Budget Variance label
                var budgetVarianceLabel = FindElementByName("Budget Variance", chartWindow, 5000);
                budgetVarianceLabel.Should().NotBeNull("Budget Variance metric should be present in summary panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Summary panel shows YTD Actuals metric
    /// </summary>
    [Fact]
    public void ChartForm_SummaryPanel_ShowsYTDActuals()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for YTD Actuals label
                var ytdActualsLabel = FindElementByName("YTD Actuals", chartWindow, 5000);
                ytdActualsLabel.Should().NotBeNull("YTD Actuals metric should be present in summary panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify trend indicator is present
    /// </summary>
    [Fact]
    public void ChartForm_SummaryPanel_ShowsTrendIndicator()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Wait for trend indicator to appear
                var hasTrendIndicator = WaitForStatusText("Trending", chartWindow, 10000);

                hasTrendIndicator.Should().BeTrue("Trend indicator should be present in summary panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Syncfusion ChartControl elements are present
    /// </summary>
    [Fact]
    public void ChartForm_Charts_SyncfusionChartControlsArePresent()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Look for ChartControl class (Syncfusion chart)
                var chartControls = FindAllElements(cf => cf.ByClassName("ChartControl"), chartWindow);

                chartControls.Should().NotBeNullOrEmpty("At least one Syncfusion ChartControl should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify changing year selector triggers chart update without crash
    /// </summary>
    [Fact]
    public void ChartForm_ChangeYearSelector_UpdatesCharts()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToChartForm();
                WaitForApplicationReady();

                var chartWindow = WaitForWindow("Budget Analytics");

                // Find the year combo box
                var yearComboBoxes = FindAllElements(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox), chartWindow);

                // Should have at least a year selector
                yearComboBoxes.Should().NotBeNullOrEmpty("Year combo box should be present");

                // Select a different year if available
                var yearCombo = yearComboBoxes.FirstOrDefault();
                if (yearCombo != null)
                {
                    // Use the helper method with proper waits to select the second item (index 1)
                    var selected = SelectComboBoxItemByIndex(yearCombo, 1);
                    if (selected)
                    {
                        // Wait for charts to update
                        WaitForApplicationReady();
                    }
                }

                // Verify application is still running (charts updated without error)
                IsApplicationLaunched.Should().BeTrue("Application should still be running after year change");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
