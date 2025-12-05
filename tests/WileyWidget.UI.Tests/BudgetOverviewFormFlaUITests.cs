using FluentAssertions;
using WileyWidget.UI.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// FlaUI-based UI automation tests for BudgetOverviewForm.
/// Tests verify form opening, summary cards, metrics grid, and refresh functionality.
/// </summary>
[Trait("Category", "UI")]
[Trait("Category", "FlaUI")]
[Collection("UITests")]
public class BudgetOverviewFormFlaUITests : FlaUITestBase
{
    /// <summary>
    /// Navigate to the Budget Overview form from the main form
    /// </summary>
    private void NavigateToBudgetOverviewForm()
    {
        LaunchApplication();
        WaitForApplicationReady();

        // Open Budget Overview form via menu
        ClickMenuItem("File", "Budget Overview");

        // Wait for Budget Overview form to appear
        var budgetWindow = WaitForWindow("Budget Overview", 10000);
        budgetWindow.Should().NotBeNull("Budget Overview form should open");
    }

    /// <summary>
    /// Verify Budget Overview form has the correct title
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_OnOpen_HasCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");
                budgetWindow.Should().NotBeNull();
                budgetWindow!.Title.Should().Contain("Budget Overview");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Refresh button is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasRefreshButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Refresh button
                var refreshButton = FindElementByName("🔄 Refresh", budgetWindow, 5000);
                refreshButton.Should().NotBeNull("Refresh button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Financial Metrics group box is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasMetricsSection()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Financial Metrics group
                var metricsGroup = FindElementByName("Financial Metrics", budgetWindow, 5000);
                metricsGroup.Should().NotBeNull("Financial Metrics section should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Total Budget label is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasTotalBudgetLabel()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Total Budget label
                var totalBudgetLabel = FindElementByName("Total Budget", budgetWindow, 5000);
                totalBudgetLabel.Should().NotBeNull("Total Budget label should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Total Actual label is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasTotalActualLabel()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Total Actual label
                var totalActualLabel = FindElementByName("Total Actual", budgetWindow, 5000);
                totalActualLabel.Should().NotBeNull("Total Actual label should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Variance label is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasVarianceLabel()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Variance label
                var varianceLabel = FindElementByName("Variance", budgetWindow, 5000);
                varianceLabel.Should().NotBeNull("Variance label should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the form can be opened via quick toolbar button
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_CanOpenFromQuickToolbar()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Click the Budget button in quick toolbar
                var budgetButton = FindElementByName("💰 Budget", MainWindow, 5000);
                budgetButton.Should().NotBeNull("Budget quick toolbar button should be present");
                budgetButton!.Click();

                // Wait for Budget Overview form to appear
                var budgetWindow = WaitForWindow("Budget Overview", 10000);
                budgetWindow.Should().NotBeNull("Budget Overview form should open from quick toolbar");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the form closes properly when closed
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_CanBeClosed()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");
                budgetWindow.Should().NotBeNull();

                // Close the form
                budgetWindow!.Close();

                // Verify we're back to main form
                Thread.Sleep(500);
                MainWindow.Should().NotBeNull("Main window should still be present after closing Budget Overview");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
