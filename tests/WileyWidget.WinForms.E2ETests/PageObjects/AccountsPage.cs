using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System;

namespace WileyWidget.WinForms.E2ETests.PageObjects;

/// <summary>
/// Page Object for AccountsForm - Municipal Accounts management view.
/// Provides access to accounts grid, filtering controls, and toolbar actions.
/// </summary>
public class AccountsPage : BasePage
{
    public AccountsPage(AutomationBase automation, Window window)
        : base(automation, window)
    {
        WaitForWindowReady();
    }

    #region Grid Elements

    /// <summary>
    /// Get Accounts data grid (SfDataGrid).
    /// </summary>
    public AutomationElement? AccountsGrid =>
        FindSfDataGrid("dataGridAccounts", DefaultTimeout);

    #endregion

    #region Toolbar Elements

    /// <summary>
    /// Get Load Accounts button from toolbar.
    /// </summary>
    public AutomationElement? LoadButton =>
        FindElementByName("Load Accounts") ??
        FindElementByAutomationId("Toolbar_Load");

    /// <summary>
    /// Get Apply Filters button from toolbar.
    /// </summary>
    public AutomationElement? ApplyFiltersButton =>
        FindElementByName("Apply Filters") ??
        FindElementByAutomationId("Toolbar_ApplyFilters");

    /// <summary>
    /// Get Allow Editing toggle button from toolbar.
    /// </summary>
    public AutomationElement? AllowEditingToggle =>
        FindElementByName("Allow Editing") ??
        FindElementByAutomationId("Toolbar_AllowEditing");

    /// <summary>
    /// Get Export button from toolbar.
    /// </summary>
    public AutomationElement? ExportButton =>
        FindElementByName("Export") ??
        FindElementByAutomationId("Toolbar_Export");

    /// <summary>
    /// Get Refresh button from toolbar.
    /// </summary>
    public AutomationElement? RefreshButton =>
        FindElementByName("Refresh") ??
        FindElementByAutomationId("Toolbar_Refresh");

    #endregion

    #region Filter Controls

    /// <summary>
    /// Get Fund filter combo box.
    /// </summary>
    public AutomationElement? FundFilterComboBox =>
        FindElementByTypeAndId(ControlType.ComboBox, "FundFilter") ??
        FindElementByTypeAndName(ControlType.ComboBox, "Fund");

    /// <summary>
    /// Get Department filter combo box.
    /// </summary>
    public AutomationElement? DepartmentFilterComboBox =>
        FindElementByTypeAndId(ControlType.ComboBox, "DepartmentFilter") ??
        FindElementByTypeAndName(ControlType.ComboBox, "Department");

    /// <summary>
    /// Get Account Type filter combo box.
    /// </summary>
    public AutomationElement? AccountTypeFilterComboBox =>
        FindElementByTypeAndId(ControlType.ComboBox, "AccountTypeFilter") ??
        FindElementByTypeAndName(ControlType.ComboBox, "Type");

    #endregion

    #region Status Bar Elements

    /// <summary>
    /// Get status bar (shows record count and load status).
    /// </summary>
    public AutomationElement? StatusBar =>
        FindElementByTypeAndName(ControlType.StatusBar, string.Empty);

    #endregion

    #region Actions

    /// <summary>
    /// Click Load Accounts button and wait for data to load.
    /// </summary>
    public void ClickLoad()
    {
        var button = LoadButton ?? throw new InvalidOperationException("Load Accounts button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Click Apply Filters button.
    /// </summary>
    public void ClickApplyFilters()
    {
        var button = ApplyFiltersButton ?? throw new InvalidOperationException("Apply Filters button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Toggle editing mode on/off.
    /// </summary>
    public void ToggleEditing()
    {
        var button = AllowEditingToggle ?? throw new InvalidOperationException("Allow Editing toggle not found");
        Click(button);
    }

    /// <summary>
    /// Click Export button.
    /// </summary>
    public void ClickExport()
    {
        var button = ExportButton ?? throw new InvalidOperationException("Export button not found");
        Click(button);
    }

    /// <summary>
    /// Click Refresh button.
    /// </summary>
    public void ClickRefresh()
    {
        var button = RefreshButton ?? throw new InvalidOperationException("Refresh button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Set fund filter value.
    /// </summary>
    public void SetFundFilter(string fundName)
    {
        var combo = FundFilterComboBox?.AsComboBox() ?? throw new InvalidOperationException("Fund filter combo box not found");
        combo.Select(fundName);
    }

    /// <summary>
    /// Get row count from accounts grid.
    /// </summary>
    public int GetAccountsRowCount()
    {
        var grid = AccountsGrid ?? throw new InvalidOperationException("Accounts grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Get status bar text.
    /// </summary>
    public string GetStatusText()
    {
        var status = StatusBar;
        if (status == null) return string.Empty;
        return GetText(status);
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Verify accounts grid is loaded and visible.
    /// </summary>
    public bool IsAccountsGridLoaded()
    {
        return AccountsGrid != null && IsVisible(AccountsGrid);
    }

    /// <summary>
    /// Verify all toolbar buttons are accessible.
    /// </summary>
    public bool AreToolbarButtonsVisible()
    {
        return LoadButton != null &&
               ApplyFiltersButton != null &&
               AllowEditingToggle != null;
    }

    /// <summary>
    /// Verify filter controls are present.
    /// </summary>
    public bool AreFiltersVisible()
    {
        return FundFilterComboBox != null;
    }

    /// <summary>
    /// Check if data has been loaded (status bar shows account count format: "N accounts loaded").
    /// Checks for numeric pattern and "account(s)" keyword to ensure data is actually loaded.
    /// </summary>
    public bool IsDataLoaded()
    {
        var statusText = GetStatusText();
        if (string.IsNullOrEmpty(statusText))
        {
            return false;
        }

        // Check for "N accounts loaded" pattern or "N accounts | ...\" pattern
        var lowerStatus = statusText.ToLowerInvariant();
        if (!lowerStatus.Contains("account", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Must contain a number before "account(s)" to be valid
        // Pattern: "0 accounts" or "10 accounts loaded" or "5 accounts | Total"
        var hasNumberBeforeAccount = System.Text.RegularExpressions.Regex.IsMatch(
            statusText,
            @"\d+\s+accounts?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return hasNumberBeforeAccount;
    }

    #endregion
}
