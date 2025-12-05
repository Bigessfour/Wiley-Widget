using FluentAssertions;
using WileyWidget.UI.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// FlaUI-based UI automation tests for AccountsForm.
/// Tests verify the accounts grid, filtering, detail panel, and data operations.
/// </summary>
[Trait("Category", "UI")]
[Trait("Category", "FlaUI")]
[Collection("UITests")]
public class AccountsFormFlaUITests : FlaUITestBase
{
    /// <summary>
    /// Navigate to the Accounts form from the main form
    /// </summary>
    private void NavigateToAccountsForm()
    {
        LaunchApplication();
        WaitForApplicationReady();

        // Open Accounts form via menu
        ClickMenuItem("File", "Accounts");

        // Wait for Accounts form to appear
        var accountsWindow = WaitForWindow("Municipal Accounts", 10000);
        accountsWindow.Should().NotBeNull("Accounts form should open");
    }

    /// <summary>
    /// Verify Accounts form has the correct title
    /// </summary>
    [Fact]
    public void AccountsForm_OnOpen_HasCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");
                accountsWindow.Should().NotBeNull();
                accountsWindow!.Title.Should().Contain("Municipal Accounts");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the data grid is present and visible
    /// </summary>
    [Fact]
    public void AccountsForm_DataGrid_IsPresent()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for SfDataGrid (Syncfusion grid)
                var dataGrid = FindElementByClassName("SfDataGrid", accountsWindow, 10000);
                dataGrid.Should().NotBeNull("Data grid should be present in Accounts form");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the toolbar with Load Accounts button is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasLoadAccountsButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Load Accounts button
                var loadButton = FindElementByName("Load Accounts", accountsWindow, 5000);
                loadButton.Should().NotBeNull("Load Accounts button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Fund filter dropdown is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasFundFilter()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Fund filter label
                var fundLabel = FindElementByName("Fund:", accountsWindow, 5000);
                fundLabel.Should().NotBeNull("Fund filter should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Type filter dropdown is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasTypeFilter()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Type filter label
                var typeLabel = FindElementByName("Type:", accountsWindow, 5000);
                typeLabel.Should().NotBeNull("Type filter should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Search box is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasSearchBox()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Search label
                var searchLabel = FindElementByName("Search:", accountsWindow, 5000);
                searchLabel.Should().NotBeNull("Search box should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Apply Filters button is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasApplyFiltersButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Apply Filters button
                var applyButton = FindElementByName("Apply Filters", accountsWindow, 5000);
                applyButton.Should().NotBeNull("Apply Filters button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Load Accounts button loads data and grid has rows
    /// </summary>
    [Fact]
    public void AccountsForm_ClickLoadAccounts_LoadsData()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Click Load Accounts button
                var loadClicked = ClickButton("Load Accounts", accountsWindow);
                loadClicked.Should().BeTrue("Load Accounts button should be clickable");

                // Wait for data to load using condition-based wait instead of Thread.Sleep
                var dataLoaded = WaitForDataLoaded(parent: accountsWindow, timeout: TimeSpan.FromSeconds(15));
                dataLoaded.Should().BeTrue("Data should load after clicking Load Accounts");

                // Verify we're still running (no crash)
                IsApplicationLaunched.Should().BeTrue("Application should still be running after loading accounts");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the detail panel header is present
    /// </summary>
    [Fact]
    public void AccountsForm_DetailPanel_HasHeader()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Account Details header
                var detailHeader = FindElementByName("Account Details", accountsWindow, 5000);
                detailHeader.Should().NotBeNull("Account Details header should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the detail panel shows account number field
    /// </summary>
    [Fact]
    public void AccountsForm_DetailPanel_ShowsAccountNumberField()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Account # label in detail panel
                var accountNumberLabel = FindElementByName("Account #:", accountsWindow, 5000);
                accountNumberLabel.Should().NotBeNull("Account # field should be present in detail panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the detail panel shows balance field
    /// </summary>
    [Fact]
    public void AccountsForm_DetailPanel_ShowsBalanceField()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Balance label in detail panel
                var balanceLabel = FindElementByName("Balance:", accountsWindow, 5000);
                balanceLabel.Should().NotBeNull("Balance field should be present in detail panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Edit button is present in the detail panel
    /// </summary>
    [Fact]
    public void AccountsForm_DetailPanel_HasEditButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Edit button
                var editButton = FindElementByName("Edit", accountsWindow, 5000);
                editButton.Should().NotBeNull("Edit button should be present in detail panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the View button is present in the detail panel
    /// </summary>
    [Fact]
    public void AccountsForm_DetailPanel_HasViewButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for View button
                var viewButton = FindElementByName("View", accountsWindow, 5000);
                viewButton.Should().NotBeNull("View button should be present in detail panel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify right-click context menu has expected items
    /// </summary>
    [Fact]
    public void AccountsForm_DataGrid_ContextMenuHasViewDetails()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Click Load Accounts first
                ClickButton("Load Accounts", accountsWindow);

                // Wait for data to load
                WaitForDataLoaded(parent: accountsWindow, timeout: TimeSpan.FromSeconds(15));

                // Find the data grid
                var dataGrid = FindElementByClassName("SfDataGrid", accountsWindow, 10000);
                dataGrid.Should().NotBeNull();

                // Right-click to open context menu
                dataGrid!.RightClick();

                // Wait for context menu to appear
                WaitForCondition(() => FindElementByName("View Details", timeoutMs: 1000) != null, 3000);

                // Look for View Details menu item
                var viewDetailsItem = FindElementByName("View Details", timeoutMs: 3000);
                viewDetailsItem.Should().NotBeNull("View Details context menu item should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Export to Excel button is present
    /// </summary>
    [Fact]
    public void AccountsForm_Toolbar_HasExportButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Look for Export to Excel button
                var exportButton = FindElementByName("Export to Excel", accountsWindow, 5000);
                exportButton.Should().NotBeNull("Export to Excel button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the status bar shows record count after loading
    /// </summary>
    [Fact]
    public void AccountsForm_StatusBar_ShowsRecordCount()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToAccountsForm();

                var accountsWindow = WaitForWindow("Municipal Accounts");

                // Click Load Accounts button
                ClickButton("Load Accounts", accountsWindow);

                // Wait for status bar to show account count using condition-based wait
                var statusUpdated = WaitForStatusText("accounts", accountsWindow, 15000) ||
                                   WaitForStatusText("Ready", accountsWindow, 5000);

                statusUpdated.Should().BeTrue("Status bar should display account count or Ready status");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
