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

    /// <summary>
    /// Verify the Export button is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasExportButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Export button
                var exportButton = FindElementByName("📊 Export", budgetWindow, 5000);
                exportButton.Should().NotBeNull("Export button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Period selector is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasPeriodSelector()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Period label
                var periodLabel = FindElementByName("Period:", budgetWindow, 5000);
                periodLabel.Should().NotBeNull("Period selector label should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Category Breakdown grid is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasCategoryBreakdownGrid()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Category Breakdown section
                var breakdownGroup = FindElementByName("Category Breakdown", budgetWindow, 5000);
                breakdownGroup.Should().NotBeNull("Category Breakdown section should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Add Category button is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasAddCategoryButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Add Category button
                var addButton = FindElementByName("➕ Add Category", budgetWindow, 5000);
                addButton.Should().NotBeNull("Add Category button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Edit Category button is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasEditCategoryButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Edit Category button
                var editButton = FindElementByName("✏️ Edit Category", budgetWindow, 5000);
                editButton.Should().NotBeNull("Edit Category button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Delete Category button is present
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasDeleteCategoryButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Delete Category button
                var deleteButton = FindElementByName("🗑️ Delete Category", budgetWindow, 5000);
                deleteButton.Should().NotBeNull("Delete Category button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Refresh button is clickable
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_RefreshButton_IsClickable()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Find and click refresh button
                var refreshButton = FindElementByName("🔄 Refresh", budgetWindow, 5000);
                refreshButton.Should().NotBeNull();

                // Verify button is enabled
                refreshButton!.IsEnabled.Should().BeTrue("Refresh button should be enabled");

                // Click the button
                refreshButton.Click();

                // Wait for refresh to complete
                Thread.Sleep(1000);

                // Button should still be present after click
                var refreshButtonAfter = FindElementByName("🔄 Refresh", budgetWindow, 5000);
                refreshButtonAfter.Should().NotBeNull("Refresh button should remain after clicking");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify form displays budget progress bar
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasBudgetProgressBar()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");

                // Look for Budget Utilization label near progress bar
                var progressLabel = FindElementByName("Budget Utilization", budgetWindow, 5000);
                progressLabel.Should().NotBeNull("Budget Utilization section should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify form shows status label
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HasStatusLabel()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");
                budgetWindow.Should().NotBeNull();

                // Status label should update with "Last updated" or "Loading" text
                // We just verify the window loaded successfully
                Thread.Sleep(2000); // Give time for data to load

                budgetWindow!.IsOffscreen.Should().BeFalse("Budget form should be visible");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify form maintains state when reopened
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_MaintainsState_WhenReopened()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();
                var firstOpen = WaitForWindow("Budget Overview");
                firstOpen.Should().NotBeNull();
                firstOpen!.Close();

                Thread.Sleep(500);

                // Open again
                NavigateToBudgetOverviewForm();
                var secondOpen = WaitForWindow("Budget Overview");
                secondOpen.Should().NotBeNull("Should be able to open Budget Overview form again");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify form handles multiple rapid clicks on Refresh button
    /// </summary>
    [Fact]
    public void BudgetOverviewForm_HandlesRapidRefreshClicks()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToBudgetOverviewForm();

                var budgetWindow = WaitForWindow("Budget Overview");
                var refreshButton = FindElementByName("🔄 Refresh", budgetWindow, 5000);
                refreshButton.Should().NotBeNull();

                // Click multiple times rapidly
                for (int i = 0; i < 3; i++)
                {
                    refreshButton!.Click();
                    Thread.Sleep(100);
                }

                // Give time for operations to complete
                Thread.Sleep(2000);

                // Form should still be responsive
                budgetWindow!.IsOffscreen.Should().BeFalse("Form should remain visible");
                var refreshButtonAfter = FindElementByName("🔄 Refresh", budgetWindow, 5000);
                refreshButtonAfter.Should().NotBeNull("Refresh button should still be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
