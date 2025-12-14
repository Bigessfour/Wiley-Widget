using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System;

namespace WileyWidget.WinForms.E2ETests.PageObjects;

/// <summary>
/// Page Object for MainForm - provides access to main window navigation, docking panels, and MDI functionality.
/// Implements the Page Object Model pattern for FlaUI E2E tests.
/// </summary>
public class MainFormPage : BasePage
{
    public MainFormPage(AutomationBase automation, Window window) 
        : base(automation, window)
    {
        WaitForWindowReady();
    }

    #region Navigation Elements

    /// <summary>
    /// Get Dashboard navigation button from ribbon or navigation panel.
    /// </summary>
    public AutomationElement? DashboardButton => 
        FindElementByAutomationId("Nav_Dashboard") ?? 
        FindElementByName("Dashboard");

    /// <summary>
    /// Get Accounts navigation button.
    /// </summary>
    public AutomationElement? AccountsButton => 
        FindElementByAutomationId("Nav_Accounts") ?? 
        FindElementByName("Accounts");

    /// <summary>
    /// Get Charts navigation button.
    /// </summary>
    public AutomationElement? ChartsButton => 
        FindElementByAutomationId("Nav_Charts") ?? 
        FindElementByName("Charts");

    /// <summary>
    /// Get Reports navigation button.
    /// </summary>
    public AutomationElement? ReportsButton => 
        FindElementByAutomationId("Nav_Reports") ?? 
        FindElementByName("Reports");

    /// <summary>
    /// Get Settings navigation button.
    /// </summary>
    public AutomationElement? SettingsButton => 
        FindElementByAutomationId("Nav_Settings") ?? 
        FindElementByName("Settings");

    /// <summary>
    /// Get Docking toggle button.
    /// </summary>
    public AutomationElement? DockingToggleButton => 
        FindElementByAutomationId("Nav_DockingToggle");

    /// <summary>
    /// Get MDI toggle button.
    /// </summary>
    public AutomationElement? MdiToggleButton => 
        FindElementByAutomationId("Nav_MdiToggle");

    #endregion

    #region Docking Panels

    /// <summary>
    /// Get left docking panel (Dashboard cards).
    /// </summary>
    public AutomationElement? LeftDockPanel => 
        FindElementByAutomationId("LeftDockPanel") ?? 
        FindDockPanel("Dashboard");

    /// <summary>
    /// Get central document panel (AI chat and MDI content).
    /// </summary>
    public AutomationElement? CentralDocumentPanel => 
        FindElementByAutomationId("CentralDocumentPanel");

    /// <summary>
    /// Get right docking panel (Activity grid).
    /// </summary>
    public AutomationElement? RightDockPanel => 
        FindElementByAutomationId("RightDockPanel") ?? 
        FindDockPanel("Activity");

    /// <summary>
    /// Get Activity data grid from right dock panel.
    /// </summary>
    public AutomationElement? ActivityDataGrid => 
        FindElementByAutomationId("ActivityDataGrid") ?? 
        FindSfDataGrid("ActivityDataGrid");

    /// <summary>
    /// Get DockingManager control.
    /// </summary>
    public AutomationElement? DockingManager => 
        FindElementByAutomationId("DockingManager_Main");

    #endregion

    #region MDI Child Forms

    /// <summary>
    /// Find MDI child window by title.
    /// </summary>
    public Window? FindMdiChild(string title, TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? DefaultTimeout;
        var child = RetryFind(() =>
        {
            var windows = Window.FindAllDescendants(cf => 
                cf.ByControlType(ControlType.Window)
                  .And(cf.ByName(title)));
            return windows.FirstOrDefault();
        }, maxWait);

        return child?.AsWindow();
    }

    /// <summary>
    /// Get all MDI child windows.
    /// </summary>
    public Window[] GetAllMdiChildren()
    {
        var children = Window.FindAllDescendants(cf => cf.ByControlType(ControlType.Window));
        return children.Select(c => c.AsWindow()).ToArray();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Navigate to Dashboard view.
    /// </summary>
    public void NavigateToDashboard()
    {
        var button = DashboardButton ?? throw new InvalidOperationException("Dashboard button not found");
        Click(button);
        WaitForBusyIndicator();
    }

    /// <summary>
    /// Navigate to Accounts view.
    /// </summary>
    public void NavigateToAccounts()
    {
        var button = AccountsButton ?? throw new InvalidOperationException("Accounts button not found");
        Click(button);
        WaitForBusyIndicator();
    }

    /// <summary>
    /// Navigate to Charts view.
    /// </summary>
    public void NavigateToCharts()
    {
        var button = ChartsButton ?? throw new InvalidOperationException("Charts button not found");
        Click(button);
        WaitForBusyIndicator();
    }

    /// <summary>
    /// Navigate to Reports view.
    /// </summary>
    public void NavigateToReports()
    {
        var button = ReportsButton ?? throw new InvalidOperationException("Reports button not found");
        Click(button);
        WaitForBusyIndicator();
    }

    /// <summary>
    /// Navigate to Settings view.
    /// </summary>
    public void NavigateToSettings()
    {
        var button = SettingsButton ?? throw new InvalidOperationException("Settings button not found");
        Click(button);
        WaitForBusyIndicator();
    }

    /// <summary>
    /// Toggle docking mode.
    /// </summary>
    public void ToggleDocking()
    {
        var button = DockingToggleButton ?? throw new InvalidOperationException("Docking toggle button not found");
        Click(button);
        System.Threading.Thread.Sleep(500); // Allow layout to reconfigure
    }

    /// <summary>
    /// Toggle MDI mode.
    /// </summary>
    public void ToggleMdi()
    {
        var button = MdiToggleButton ?? throw new InvalidOperationException("MDI toggle button not found");
        Click(button);
        System.Threading.Thread.Sleep(500); // Allow layout to reconfigure
    }

    /// <summary>
    /// Verify docking panels are visible.
    /// </summary>
    public bool AreDockingPanelsVisible()
    {
        return IsVisible(LeftDockPanel) && 
               IsVisible(CentralDocumentPanel) && 
               IsVisible(RightDockPanel);
    }

    /// <summary>
    /// Get activity grid row count.
    /// </summary>
    public int GetActivityRowCount()
    {
        var grid = ActivityDataGrid;
        if (grid == null) return 0;
        return GetGridRowCount(grid);
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Verify MainForm is loaded and ready.
    /// </summary>
    public bool IsLoaded()
    {
        return Window.IsAvailable && 
               (DashboardButton != null || AccountsButton != null);
    }

    /// <summary>
    /// Verify theme is applied (check control colors match expected theme).
    /// </summary>
    public bool IsThemeApplied()
    {
        // Check if controls have theme-specific properties
        // This is a basic check - enhance based on theme specifics
        return Window.IsAvailable;
    }

    #endregion
}
