using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System;
using System.Linq;

namespace WileyWidget.WinForms.E2ETests.PageObjects;

/// <summary>
/// Page Object for CustomersForm - Utility Customer Management view.
/// Provides access to customer grid, CRUD buttons, search, and detail tabs.
/// </summary>
public class CustomersPage : BasePage
{
    public CustomersPage(AutomationBase automation, Window window)
        : base(automation, window)
    {
        WaitForWindowReady();
    }

    #region Grid Elements

    /// <summary>
    /// Get Customers data grid (SfDataGrid).
    /// </summary>
    public AutomationElement? CustomersGrid =>
        FindSfDataGrid("Customers_DataGrid", DefaultTimeout);

    #endregion

    #region Toolbar Elements

    /// <summary>
    /// Get Load button from toolbar.
    /// </summary>
    public AutomationElement? LoadButton =>
        FindElementByName("Load") ??
        FindElementByAutomationId("Toolbar_Load");

    /// <summary>
    /// Get New button from toolbar (creates new customer record).
    /// </summary>
    public AutomationElement? NewButton =>
        FindElementByName("New") ??
        FindElementByAutomationId("Toolbar_New");

    /// <summary>
    /// Get Save button from toolbar.
    /// </summary>
    public AutomationElement? SaveButton =>
        FindElementByName("Save") ??
        FindElementByAutomationId("Toolbar_Save");

    /// <summary>
    /// Get Delete button from toolbar.
    /// </summary>
    public AutomationElement? DeleteButton =>
        FindElementByName("Delete") ??
        FindElementByAutomationId("Toolbar_Delete");

    /// <summary>
    /// Get Refresh button from toolbar.
    /// </summary>
    public AutomationElement? RefreshButton =>
        FindElementByName("Refresh") ??
        FindElementByAutomationId("Toolbar_Refresh");

    /// <summary>
    /// Get Export button from toolbar.
    /// </summary>
    public AutomationElement? ExportButton =>
        FindElementByName("Export") ??
        FindElementByAutomationId("Toolbar_Export");

    /// <summary>
    /// Get Search textbox from toolbar.
    /// </summary>
    public AutomationElement? SearchBox =>
        FindElementByTypeAndId(ControlType.Edit, "SearchBox") ??
        FindElementByTypeAndName(ControlType.Edit, "Search");

    #endregion

    #region Detail Tabs

    /// <summary>
    /// Get tab control for customer details.
    /// </summary>
    public AutomationElement? DetailTabControl =>
        FindElementByTypeAndId(ControlType.Tab, "CustomerDetailTabs") ??
        Window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));

    /// <summary>
    /// Get Basic Info tab.
    /// </summary>
    public AutomationElement? BasicInfoTab =>
        FindElementByTypeAndName(ControlType.TabItem, "Basic");

    /// <summary>
    /// Get Service Address tab.
    /// </summary>
    public AutomationElement? ServiceAddressTab =>
        FindElementByTypeAndName(ControlType.TabItem, "Service");

    /// <summary>
    /// Get Mailing Address tab.
    /// </summary>
    public AutomationElement? MailingAddressTab =>
        FindElementByTypeAndName(ControlType.TabItem, "Mailing");

    /// <summary>
    /// Get Account Details tab.
    /// </summary>
    public AutomationElement? AccountDetailsTab =>
        FindElementByTypeAndName(ControlType.TabItem, "Account");

    #endregion

    #region Status Bar Elements

    /// <summary>
    /// Get status bar (shows record count).
    /// </summary>
    public AutomationElement? StatusBar =>
        FindElementByTypeAndName(ControlType.StatusBar, string.Empty);

    #endregion

    #region Actions

    /// <summary>
    /// Click Load button and wait for data to load.
    /// </summary>
    public void ClickLoad()
    {
        var button = LoadButton ?? throw new InvalidOperationException("Load button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Click New button to create a new customer record.
    /// </summary>
    public void ClickNew()
    {
        var button = NewButton ?? throw new InvalidOperationException("New button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(5)); // Wait for form to reset
    }

    /// <summary>
    /// Click Save button to save current customer record.
    /// </summary>
    public void ClickSave()
    {
        var button = SaveButton ?? throw new InvalidOperationException("Save button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(5)); // Wait for save to complete
    }

    /// <summary>
    /// Click Delete button to delete selected customer.
    /// </summary>
    public void ClickDelete()
    {
        var button = DeleteButton ?? throw new InvalidOperationException("Delete button not found");
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
    /// Click Export button.
    /// </summary>
    public void ClickExport()
    {
        var button = ExportButton ?? throw new InvalidOperationException("Export button not found");
        Click(button);
    }

    /// <summary>
    /// Set search text and trigger search.
    /// </summary>
    public void Search(string searchText)
    {
        var textBox = SearchBox ?? throw new InvalidOperationException("Search box not found");
        SetText(textBox, searchText);
        WaitForBusyIndicator(TimeSpan.FromSeconds(5)); // Wait for search to execute
    }

    /// <summary>
    /// Switch to Basic Info tab.
    /// </summary>
    public void SwitchToBasicInfoTab()
    {
        var tab = BasicInfoTab ?? throw new InvalidOperationException("Basic Info tab not found");
        Click(tab);
        WaitForBusyIndicator(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Switch to Service Address tab.
    /// </summary>
    public void SwitchToServiceAddressTab()
    {
        var tab = ServiceAddressTab ?? throw new InvalidOperationException("Service Address tab not found");
        Click(tab);
        WaitForBusyIndicator(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Switch to Mailing Address tab.
    /// </summary>
    public void SwitchToMailingAddressTab()
    {
        var tab = MailingAddressTab ?? throw new InvalidOperationException("Mailing Address tab not found");
        Click(tab);
        WaitForBusyIndicator(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Switch to Account Details tab.
    /// </summary>
    public void SwitchToAccountDetailsTab()
    {
        var tab = AccountDetailsTab ?? throw new InvalidOperationException("Account Details tab not found");
        Click(tab);
        WaitForBusyIndicator(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Get row count from customers grid.
    /// </summary>
    public int GetCustomersRowCount()
    {
        var grid = CustomersGrid ?? throw new InvalidOperationException("Customers grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Get status bar text (should contain "Records: X").
    /// </summary>
    public string GetStatusText()
    {
        var status = StatusBar;
        if (status == null) return string.Empty;
        return GetText(status);
    }

    /// <summary>
    /// Select first row in customers grid.
    /// </summary>
    public void SelectFirstRow()
    {
        var grid = CustomersGrid ?? throw new InvalidOperationException("Customers grid not found");
        Click(grid); // Click on grid to select first item
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Verify customers grid is loaded and visible.
    /// </summary>
    public bool IsCustomersGridLoaded()
    {
        return CustomersGrid != null && IsVisible(CustomersGrid);
    }

    /// <summary>
    /// Verify all toolbar buttons are accessible.
    /// </summary>
    public bool AreToolbarButtonsVisible()
    {
        return LoadButton != null &&
               NewButton != null &&
               SaveButton != null &&
               DeleteButton != null &&
               RefreshButton != null &&
               ExportButton != null;
    }

    /// <summary>
    /// Verify detail tabs are present.
    /// </summary>
    public bool AreDetailTabsVisible()
    {
        var tabs = DetailTabControl?.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
        return tabs != null && tabs.Length >= 4;
    }

    /// <summary>
    /// Check if Save button is enabled (indicates edit mode or new record).
    /// </summary>
    public bool IsSaveButtonEnabled()
    {
        var button = SaveButton;
        return button != null && button.IsEnabled;
    }

    /// <summary>
    /// Check if data has been loaded (status bar contains "Records:").
    /// </summary>
    public bool IsDataLoaded()
    {
        var statusText = GetStatusText();
        return statusText.Contains("Records:", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
