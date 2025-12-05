using System.Linq;
using System.Threading;
using FluentAssertions;
using WileyWidget.UI.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// FlaUI-based UI automation tests for SettingsForm.
/// Tests verify settings tabs, controls, and save/cancel operations.
/// </summary>
[Trait("Category", "UI")]
[Trait("Category", "FlaUI")]
[Collection("UITests")]
public class SettingsFormFlaUITests : FlaUITestBase
{
    /// <summary>
    /// Navigate to the Settings form from the main form with retry logic.
    /// </summary>
    private void NavigateToSettingsForm()
    {
        ExecuteWithRetry(() =>
        {
            LaunchApplication();
            WaitForApplicationReady();

            // Dump initial state for debugging
            LogDebug("NavigateToSettingsForm: Initial state");
            LogAllTopLevelWindows();
            DumpAllElements(MainWindow, "MainWindow elements");

            // Open Settings form via menu - check if click succeeded
            var menuClicked = ClickMenuItem("Tools", "Settings");
            menuClicked.Should().BeTrue("Menu 'Tools > Settings' should be clickable");

            // Wait for the app to process the menu click
            WaitForInputIdle();

            // Wait a moment for the form to open
            Thread.Sleep(500);

            // Wait for Settings form to appear
            var settingsWindow = WaitForWindow("Settings", ChildWindowTimeout);

            if (settingsWindow == null)
            {
                // Debug: List all elements after menu click
                LogDebug("NavigateToSettingsForm: Settings window not found, dumping state");
                DumpAllElements(MainWindow, "After menu click");
                LogAllTopLevelWindows();
            }

            settingsWindow.Should().NotBeNull($"Settings form should open after clicking Tools > Settings menu. Available windows: {GetAvailableWindowTitles()}");
        }, maxRetries: 3);
    }

    /// <summary>
    /// Click on a tab by name
    /// </summary>
    private bool ClickTab(string tabName, FlaUI.Core.AutomationElements.AutomationElement? searchContext, int timeout = 5000)
    {
        LogDebug($"ClickTab: Looking for tab '{tabName}'");
        var tab = FindElementByName(tabName, searchContext, timeout);
        if (tab == null)
        {
            LogDebug($"ClickTab: Tab '{tabName}' not found");
            DumpAllElements(searchContext, $"Looking for tab '{tabName}'");
            return false;
        }

        WaitForElementEnabled(tab);
        ClickElementWithMouse(tab);
        WaitForInputIdle();
        LogDebug($"ClickTab: Clicked tab '{tabName}'");
        return true;
    }

    /// <summary>
    /// Verify Settings form has the correct title
    /// </summary>
    [Fact]
    public void SettingsForm_OnOpen_HasCorrectTitle()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");
                settingsWindow.Should().NotBeNull();
                settingsWindow!.Title.Should().Be("Settings");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the General tab is present
    /// </summary>
    [Fact]
    public void SettingsForm_TabControl_HasGeneralTab()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for General tab
                var generalTab = FindElementByName("General", settingsWindow, 5000);
                generalTab.Should().NotBeNull("General tab should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Connections tab is present
    /// </summary>
    [Fact]
    public void SettingsForm_TabControl_HasConnectionsTab()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Connections tab
                var connectionsTab = FindElementByName("Connections", settingsWindow, 5000);
                connectionsTab.Should().NotBeNull("Connections tab should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the QuickBooks tab is present
    /// </summary>
    [Fact]
    public void SettingsForm_TabControl_HasQuickBooksTab()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for QuickBooks tab
                var quickBooksTab = FindElementByName("QuickBooks", settingsWindow, 5000);
                quickBooksTab.Should().NotBeNull("QuickBooks tab should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Appearance tab is present
    /// </summary>
    [Fact]
    public void SettingsForm_TabControl_HasAppearanceTab()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Appearance tab
                var appearanceTab = FindElementByName("Appearance", settingsWindow, 5000);
                appearanceTab.Should().NotBeNull("Appearance tab should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Advanced tab is present
    /// </summary>
    [Fact]
    public void SettingsForm_TabControl_HasAdvancedTab()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Advanced tab
                var advancedTab = FindElementByName("Advanced", settingsWindow, 5000);
                advancedTab.Should().NotBeNull("Advanced tab should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Save Settings button is present
    /// </summary>
    [Fact]
    public void SettingsForm_ButtonPanel_HasSaveButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Save Settings button
                var saveButton = FindElementByName("Save Settings", settingsWindow, 5000);
                saveButton.Should().NotBeNull("Save Settings button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Cancel button is present
    /// </summary>
    [Fact]
    public void SettingsForm_ButtonPanel_HasCancelButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Cancel button
                var cancelButton = FindElementByName("Cancel", settingsWindow, 5000);
                cancelButton.Should().NotBeNull("Cancel button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify the Reset to Defaults button is present
    /// </summary>
    [Fact]
    public void SettingsForm_ButtonPanel_HasResetButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Reset to Defaults button
                var resetButton = FindElementByName("Reset to Defaults", settingsWindow, 5000);
                resetButton.Should().NotBeNull("Reset to Defaults button should be present");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking Cancel button closes the form
    /// </summary>
    [Fact]
    public void SettingsForm_ClickCancelButton_ClosesForm()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");
                settingsWindow.Should().NotBeNull();

                // Click Cancel button
                var cancelClicked = ClickButton("Cancel", settingsWindow);
                cancelClicked.Should().BeTrue("Cancel button should be clickable");

                // Wait for the window to close using condition-based wait
                var windowClosed = WaitForWindowClosed("Settings", 5000);
                windowClosed.Should().BeTrue("Settings form should close after clicking Cancel");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify General tab has Company Name field
    /// </summary>
    [Fact]
    public void SettingsForm_GeneralTab_HasCompanyNameField()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Company Name label
                var companyNameLabel = FindElementByName("Company Name:", settingsWindow, 5000);
                companyNameLabel.Should().NotBeNull("Company Name field should be present in General tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify General tab has Auto-save Interval field
    /// </summary>
    [Fact]
    public void SettingsForm_GeneralTab_HasAutoSaveField()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Auto-save Interval label
                var autoSaveLabel = FindElementByName("Auto-save Interval (min):", settingsWindow, 5000);
                autoSaveLabel.Should().NotBeNull("Auto-save Interval field should be present in General tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify General tab has Enable Logging checkbox
    /// </summary>
    [Fact]
    public void SettingsForm_GeneralTab_HasEnableLoggingCheckbox()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Look for Enable Logging label
                var enableLoggingLabel = FindElementByName("Enable Logging:", settingsWindow, 5000);
                enableLoggingLabel.Should().NotBeNull("Enable Logging field should be present in General tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify clicking QuickBooks tab shows QuickBooks settings
    /// </summary>
    [Fact]
    public void SettingsForm_ClickQuickBooksTab_ShowsQuickBooksSettings()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click QuickBooks tab
                var tabClicked = ClickTab("QuickBooks", settingsWindow);
                tabClicked.Should().BeTrue("QuickBooks tab should be clickable");

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Connection Status:", settingsWindow, 1000) != null, 5000);

                // Look for Connection Status label
                var connectionStatusLabel = FindElementByName("Connection Status:", settingsWindow, 5000);
                connectionStatusLabel.Should().NotBeNull("Connection Status field should be visible on QuickBooks tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify QuickBooks tab has Sync Now button
    /// </summary>
    [Fact]
    public void SettingsForm_QuickBooksTab_HasSyncNowButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click QuickBooks tab first
                ClickTab("QuickBooks", settingsWindow);

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Sync Now", settingsWindow, 1000) != null, 5000);

                // Look for Sync Now button
                var syncNowButton = FindElementByName("Sync Now", settingsWindow, 5000);
                syncNowButton.Should().NotBeNull("Sync Now button should be present on QuickBooks tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Appearance tab has Theme selector
    /// </summary>
    [Fact]
    public void SettingsForm_AppearanceTab_HasThemeSelector()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click Appearance tab first
                ClickTab("Appearance", settingsWindow);

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Theme:", settingsWindow, 1000) != null, 5000);

                // Look for Theme label
                var themeLabel = FindElementByName("Theme:", settingsWindow, 5000);
                themeLabel.Should().NotBeNull("Theme selector should be present on Appearance tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Appearance tab has Dark Mode checkbox
    /// </summary>
    [Fact]
    public void SettingsForm_AppearanceTab_HasDarkModeCheckbox()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click Appearance tab first
                ClickTab("Appearance", settingsWindow);

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Dark Mode:", settingsWindow, 1000) != null, 5000);

                // Look for Dark Mode label
                var darkModeLabel = FindElementByName("Dark Mode:", settingsWindow, 5000);
                darkModeLabel.Should().NotBeNull("Dark Mode checkbox should be present on Appearance tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Connections tab has Test Connection button
    /// </summary>
    [Fact]
    public void SettingsForm_ConnectionsTab_HasTestConnectionButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click Connections tab first
                ClickTab("Connections", settingsWindow);

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Test Connection", settingsWindow, 1000) != null, 5000);

                // Look for Test Connection button
                var testConnectionButton = FindElementByName("Test Connection", settingsWindow, 5000);
                testConnectionButton.Should().NotBeNull("Test Connection button should be present on Connections tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }

    /// <summary>
    /// Verify Advanced tab has Clear Cache button
    /// </summary>
    [Fact]
    public void SettingsForm_AdvancedTab_HasClearCacheButton()
    {
        ExecuteWithScreenshotOnFailure(() =>
        {
            try
            {
                NavigateToSettingsForm();

                var settingsWindow = WaitForWindow("Settings");

                // Click Advanced tab first
                ClickTab("Advanced", settingsWindow);

                // Wait for tab content to load
                WaitForCondition(() => FindElementByName("Clear Cache", settingsWindow, 1000) != null, 5000);

                // Look for Clear Cache button
                var clearCacheButton = FindElementByName("Clear Cache", settingsWindow, 5000);
                clearCacheButton.Should().NotBeNull("Clear Cache button should be present on Advanced tab");
            }
            finally
            {
                CloseApplication();
            }
        });
    }
}
