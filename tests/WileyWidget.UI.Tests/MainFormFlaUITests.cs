using FluentAssertions;
using WileyWidget.UI.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// FlaUI-based UI automation tests for MainForm.
/// Tests verify the main dashboard UI elements, navigation, and basic interactions.
/// </summary>
[Trait("Category", "UI")]
[Trait("Category", "FlaUI")]
[Collection("UITests")]
public class MainFormFlaUITests : FlaUITestBase
{
    /// <summary>
    /// Verify the main window title is correct upon startup
    /// </summary>
    [Fact]
    public void MainForm_OnStartup_HasCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                MainWindow.Should().NotBeNull();
                MainWindow!.Title.Should().Contain("Wiley Widget");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the main menu strip is present with expected menu items
    /// </summary>
    [Fact]
    public void MainForm_MenuStrip_HasExpectedMenuItems()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Verify File menu exists
                var fileMenu = FindElementByName("File");
                fileMenu.Should().NotBeNull("File menu should be present");

                // Verify View menu exists
                var viewMenu = FindElementByName("View");
                viewMenu.Should().NotBeNull("View menu should be present");

                // Verify Tools menu exists
                var toolsMenu = FindElementByName("Tools");
                toolsMenu.Should().NotBeNull("Tools menu should be present");

                // Verify Help menu exists
                var helpMenu = FindElementByName("Help");
                helpMenu.Should().NotBeNull("Help menu should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify dashboard cards are present on the main form
    /// </summary>
    [Fact]
    public void MainForm_Dashboard_HasExpectedCards()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Verify Accounts card is present
                var accountsCard = FindElementByName("📊 Accounts", timeoutMs: 5000);
                accountsCard.Should().NotBeNull("Accounts dashboard card should be present");

                // Verify Charts card is present
                var chartsCard = FindElementByName("📈 Charts", timeoutMs: 5000);
                chartsCard.Should().NotBeNull("Charts dashboard card should be present");

                // Verify Settings card is present
                var settingsCard = FindElementByName("⚙️ Settings", timeoutMs: 5000);
                settingsCard.Should().NotBeNull("Settings dashboard card should be present");

                // Verify Budget Status card is present
                var budgetStatusCard = FindElementByName("ℹ️ Budget Status", timeoutMs: 5000);
                budgetStatusCard.Should().NotBeNull("Budget Status dashboard card should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify status strip shows connection status
    /// </summary>
    [Fact]
    public void MainForm_StatusStrip_ShowsConnectionStatus()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Look for database connection status indicator
                var connectionStatus = FindElementByName("🟢 Database Connected", timeoutMs: 5000);
                connectionStatus.Should().NotBeNull("Database connection status should be displayed");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Accounts menu opens Accounts form
    /// </summary>
    [Fact]
    public void MainForm_ClickAccountsMenu_OpensAccountsForm()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Navigate to File > Accounts
                var menuClicked = ClickMenuItem("File", "Accounts");
                menuClicked.Should().BeTrue("File > Accounts menu should be clickable");

                // Wait for the Accounts form to appear
                var accountsWindow = WaitForWindow("Municipal Accounts", 10000);
                accountsWindow.Should().NotBeNull("Accounts form should open when Accounts menu is clicked");

                // Close the accounts window
                accountsWindow?.Close();
                WaitForWindowClosed("Municipal Accounts", 5000);
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Charts menu opens Chart form
    /// </summary>
    [Fact]
    public void MainForm_ClickChartsMenu_OpensChartForm()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Navigate to File > Charts
                var menuClicked = ClickMenuItem("File", "Charts");
                menuClicked.Should().BeTrue("File > Charts menu should be clickable");

                // Wait for the Chart form to appear
                var chartWindow = WaitForWindow("Budget Analytics", 10000);
                chartWindow.Should().NotBeNull("Chart form should open when Charts menu is clicked");

                // Close the chart window
                chartWindow?.Close();
                WaitForWindowClosed("Budget Analytics", 5000);
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Settings menu opens Settings form
    /// </summary>
    [Fact]
    public void MainForm_ClickSettingsMenu_OpensSettingsForm()
    {
        ExecuteWithRetry(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Log initial state
                LogDebug("MainForm_ClickSettingsMenu_OpensSettingsForm: Starting test");
                LogAllTopLevelWindows();

                // Navigate to Tools > Settings
                var menuClicked = ClickMenuItem("Tools", "Settings");
                menuClicked.Should().BeTrue("Tools > Settings menu should be clickable");

                // Wait for the Settings form to appear
                var settingsWindow = WaitForWindow("Settings", ChildWindowTimeout);
                settingsWindow.Should().NotBeNull($"Settings form should open when Settings menu is clicked. Available windows: {GetAvailableWindowTitles()}");

                // Close the settings window
                settingsWindow?.Close();
                WaitForWindowClosed("Settings", TimeSpan.FromSeconds(5));
            }
            finally
            {
                CloseChildWindows();
            }
        }, maxRetries: 3);
    }

    /// <summary>
    /// Test that Tools menu opens and Settings submenu item is visible.
    /// This isolates menu accessibility from form opening.
    /// </summary>
    [Fact]
    public void MainForm_ToolsMenu_OpensAndShowsSettings()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Find and click Tools menu
                var toolsMenu = FindElementByName("Tools");
                toolsMenu.Should().NotBeNull("Tools menu should be present");

                DumpElementDetails(toolsMenu, "Tools menu");

                // Use mouse simulation to click Tools menu
                ClickElementWithMouse(toolsMenu!);
                WaitForInputIdle();

                // Wait for Settings menu item to appear
                var settingsFound = WaitForCondition(
                    () => FindElementByName("Settings", timeout: TimeSpan.FromSeconds(1)) != null,
                    TimeSpan.FromSeconds(3),
                    "Settings menu item to appear");

                if (!settingsFound)
                {
                    // Dump desktop elements to see what's available
                    var desktop = Automation?.GetDesktop();
                    DumpAllElements(desktop, "Desktop after Tools click");
                    DumpAllElements(MainWindow, "MainWindow after Tools click");
                }

                settingsFound.Should().BeTrue("Settings submenu item should appear after clicking Tools menu");

                // Now click Settings
                var settingsItem = FindElementByName("Settings");
                settingsItem.Should().NotBeNull("Settings menu item should be visible");

                ClickElementWithMouse(settingsItem!);
                WaitForInputIdle();

                // Verify Settings window opened
                var settingsWindow = WaitForWindow("Settings", ChildWindowTimeout);
                settingsWindow.Should().NotBeNull($"Settings form should open. Available windows: {GetAvailableWindowTitles()}");

                // Close the window
                settingsWindow?.Close();
            }
            finally
            {
                CloseChildWindows();
            }
        });
    }

    /// <summary>
    /// Verify quick action toolbar is present with expected buttons
    /// </summary>
    [Fact]
    public void MainForm_QuickToolbar_HasExpectedButtons()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Verify Quick Accounts button exists
                var accountsBtn = FindElementByName("📊 Accounts", timeoutMs: 5000);
                accountsBtn.Should().NotBeNull("Quick Accounts button should be present");

                // Verify Quick Charts button exists
                var chartsBtn = FindElementByName("📈 Charts", timeoutMs: 5000);
                chartsBtn.Should().NotBeNull("Quick Charts button should be present");

                // Verify Quick Settings button exists
                var settingsBtn = FindElementByName("⚙️ Settings", timeoutMs: 5000);
                settingsBtn.Should().NotBeNull("Quick Settings button should be present");

                // Verify Refresh button exists
                var refreshBtn = FindElementByName("🔄 Refresh", timeoutMs: 5000);
                refreshBtn.Should().NotBeNull("Refresh button should be present");

                // Verify Export button exists
                var exportBtn = FindElementByName("📄 Export", timeoutMs: 5000);
                exportBtn.Should().NotBeNull("Export button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking dashboard Accounts card opens Accounts form
    /// </summary>
    [Fact]
    public void MainForm_ClickAccountsCard_OpensAccountsForm()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Find and click the Accounts card
                var accountsCard = FindElementByName("📊 Accounts", timeoutMs: 5000);
                accountsCard.Should().NotBeNull();

                // Wait for element to be enabled before clicking
                WaitForElementEnabled(accountsCard!);
                accountsCard!.Click();

                // Wait for the Accounts form to appear
                var accountsWindow = WaitForWindow("Municipal Accounts", 10000);
                accountsWindow.Should().NotBeNull("Accounts form should open when Accounts card is clicked");

                // Close the accounts window
                accountsWindow?.Close();
                WaitForWindowClosed("Municipal Accounts", 5000);
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Refresh button triggers UI update without error
    /// </summary>
    [Fact]
    public void MainForm_ClickRefreshButton_UpdatesWithoutError()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Click the Refresh button from quick toolbar
                var refreshClicked = ClickButton("🔄 Refresh");
                refreshClicked.Should().BeTrue("Refresh button should be clickable");

                // Wait for refresh to complete using condition-based wait
                WaitForApplicationReady();

                // Verify the main window is still responsive (no crash/hang)
                MainWindow.Should().NotBeNull();
                IsApplicationLaunched.Should().BeTrue("Application should still be running after refresh");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify F5 keyboard shortcut triggers refresh
    /// </summary>
    [Fact]
    public void MainForm_PressF5_TriggersRefresh()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Ensure main window has focus
                MainWindow?.SetForeground();
                WaitForCondition(() => MainWindow?.IsEnabled == true, ShortTimeoutMs);

                // Press F5 to refresh
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.F5);

                // Wait for refresh to complete using condition-based wait
                WaitForApplicationReady();

                // Verify the main window is still responsive
                MainWindow.Should().NotBeNull();
                IsApplicationLaunched.Should().BeTrue("Application should still be running after F5 refresh");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify header panel displays the correct title
    /// </summary>
    [Fact]
    public void MainForm_HeaderPanel_DisplaysCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Look for the header title
                var headerTitle = FindElementByName("🏛️ Wiley Widget Dashboard", timeoutMs: 5000);
                headerTitle.Should().NotBeNull("Dashboard header title should be displayed");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Recent Activity grid is present
    /// </summary>
    [Fact]
    public void MainForm_RecentActivity_GridIsPresent()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                LaunchApplication();
                WaitForApplicationReady();

                // Look for the Recent Activity header
                var recentActivityHeader = FindElementByName("📋 Recent Activity", timeoutMs: 5000);
                recentActivityHeader.Should().NotBeNull("Recent Activity section header should be displayed");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
