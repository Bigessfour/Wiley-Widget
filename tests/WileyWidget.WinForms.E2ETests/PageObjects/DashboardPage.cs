using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System;
using System.Linq;

namespace WileyWidget.WinForms.E2ETests.PageObjects;

/// <summary>
/// Page Object for DashboardForm - provides access to dashboard grids, charts, and metrics.
/// </summary>
public class DashboardPage : BasePage
{
    public DashboardPage(AutomationBase automation, Window window)
        : base(automation, window)
    {
        WaitForWindowReady();
        WaitForBusyIndicator();
    }

    #region Toolbar Elements

    /// <summary>
    /// Get Load Dashboard button from toolbar.
    /// </summary>
    public AutomationElement? LoadButton =>
        FindElementByAutomationId("Toolbar_Load") ??
        FindElementByName("Load Dashboard");

    /// <summary>
    /// Get Refresh button from toolbar.
    /// </summary>
    public AutomationElement? RefreshButton =>
        FindElementByAutomationId("Toolbar_RefreshButton") ??
        FindElementByName("Refresh");

    /// <summary>
    /// Get Export button from toolbar.
    /// </summary>
    public AutomationElement? ExportButton =>
        FindElementByAutomationId("Toolbar_ExportButton") ??
        FindElementByName("Export");

    /// <summary>
    /// Get last updated label from status bar.
    /// </summary>
    public AutomationElement? LastUpdatedLabel =>
        FindElementByAutomationId("LastUpdatedLabel");

    #endregion

    #region Data Grid Elements (9 grids total)

    /// <summary>
    /// Get Metrics grid (main summary grid).
    /// </summary>
    public AutomationElement? MetricsGrid =>
        FindSfDataGrid("Dashboard_Grid_Metrics", DefaultTimeout);

    /// <summary>
    /// Get Funds grid.
    /// </summary>
    public AutomationElement? FundsGrid =>
        FindSfDataGrid("Dashboard_Grid_Funds", DefaultTimeout);

    /// <summary>
    /// Get Departments grid.
    /// </summary>
    public AutomationElement? DepartmentsGrid =>
        FindSfDataGrid("Dashboard_Grid_Departments", DefaultTimeout);

    /// <summary>
    /// Get Top Variances grid.
    /// </summary>
    public AutomationElement? TopVariancesGrid =>
        FindSfDataGrid("Dashboard_Grid_TopVariances", DefaultTimeout);

    /// <summary>
    /// Get Budget Analysis grid.
    /// </summary>
    public AutomationElement? BudgetAnalysisGrid =>
        FindSfDataGrid("Dashboard_Grid_BudgetAnalysis", DefaultTimeout);

    /// <summary>
    /// Get Analytics Metrics grid.
    /// </summary>
    public AutomationElement? AnalyticsMetricsGrid =>
        FindSfDataGrid("Dashboard_Grid_AnalyticsMetrics", DefaultTimeout);

    /// <summary>
    /// Get Scenario grid.
    /// </summary>
    public AutomationElement? ScenarioGrid =>
        FindSfDataGrid("Dashboard_Grid_Scenario", DefaultTimeout);

    #endregion

    #region Actions

    /// <summary>
    /// Click Load Dashboard button.
    /// </summary>
    public void ClickLoad()
    {
        var button = LoadButton ?? throw new InvalidOperationException("Load button not found");
        Click(button);
        WaitForBusyIndicator(TimeSpan.FromSeconds(15)); // Data loading may take longer
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
        WaitForBusyIndicator(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Get row count from Metrics grid.
    /// </summary>
    public int GetMetricsRowCount()
    {
        var grid = MetricsGrid ?? throw new InvalidOperationException("Metrics grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Get row count from Funds grid.
    /// </summary>
    public int GetFundsRowCount()
    {
        var grid = FundsGrid ?? throw new InvalidOperationException("Funds grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Get row count from Departments grid.
    /// </summary>
    public int GetDepartmentsRowCount()
    {
        var grid = DepartmentsGrid ?? throw new InvalidOperationException("Departments grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Get row count from Top Variances grid.
    /// </summary>
    public int GetTopVariancesRowCount()
    {
        var grid = TopVariancesGrid ?? throw new InvalidOperationException("Top Variances grid not found");
        return GetGridRowCount(grid);
    }

    /// <summary>
    /// Sort grid by column header.
    /// </summary>
    public void SortByColumn(AutomationElement grid, string columnHeader)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var headerCell = grid.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.HeaderItem)
              .And(cf.ByName(columnHeader)));

        if (headerCell == null)
            throw new InvalidOperationException($"Column header '{columnHeader}' not found");

        Click(headerCell);
        WaitForBusyIndicator(TimeSpan.FromSeconds(3)); // Wait for sort to complete
    }

    /// <summary>
    /// Get cell value from grid at specific row and column.
    /// </summary>
    public string GetCellValue(AutomationElement grid, int row, int column)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var cells = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Custom));
        if (cells.Length == 0) return string.Empty;

        var targetIndex = row * GetColumnCount(grid) + column;
        if (targetIndex >= cells.Length) return string.Empty;

        return GetText(cells[targetIndex]);
    }

    private int GetColumnCount(AutomationElement grid)
    {
        var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem));
        return headers.Length;
    }

    #endregion

    #region Tab Navigation

    /// <summary>
    /// Switch to Funds tab in details section.
    /// </summary>
    public void SwitchToFundsTab()
    {
        SwitchToTab("Funds");
    }

    /// <summary>
    /// Switch to Departments tab in details section.
    /// </summary>
    public void SwitchToDepartmentsTab()
    {
        SwitchToTab("Departments");
    }

    /// <summary>
    /// Switch to Top Variances tab in details section.
    /// </summary>
    public void SwitchToTopVariancesTab()
    {
        SwitchToTab("Top Variances");
    }

    /// <summary>
    /// Switch to Budget Analysis tab in details section.
    /// </summary>
    public void SwitchToBudgetAnalysisTab()
    {
        SwitchToTab("Budget Analysis");
    }

    /// <summary>
    /// Switch to Analytics tab in details section.
    /// </summary>
    public void SwitchToAnalyticsTab()
    {
        SwitchToTab("Analytics");
    }

    private void SwitchToTab(string tabName)
    {
        var tab = FindElementByTypeAndName(ControlType.TabItem, tabName);
        if (tab == null)
            throw new InvalidOperationException($"Tab '{tabName}' not found");

        Click(tab);
        WaitForBusyIndicator(TimeSpan.FromSeconds(2)); // Wait for tab to load
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Verify dashboard is loaded and grids are visible.
    /// </summary>
    public bool IsDashboardLoaded()
    {
        return MetricsGrid != null &&
               IsVisible(MetricsGrid) &&
               LoadButton != null;
    }

    /// <summary>
    /// Verify all toolbar buttons are present.
    /// </summary>
    public bool AreToolbarButtonsVisible()
    {
        return LoadButton != null &&
               RefreshButton != null &&
               ExportButton != null;
    }

    /// <summary>
    /// Verify all 7 main grids are accessible (not all may be visible simultaneously due to tabs).
    /// </summary>
    public bool AreAllGridsAccessible()
    {
        return MetricsGrid != null &&
               FundsGrid != null &&
               DepartmentsGrid != null &&
               TopVariancesGrid != null &&
               BudgetAnalysisGrid != null &&
               AnalyticsMetricsGrid != null &&
               ScenarioGrid != null;
    }

    /// <summary>
    /// Get last updated timestamp text.
    /// </summary>
    public string GetLastUpdatedText()
    {
        var label = LastUpdatedLabel;
        if (label == null) return string.Empty;
        return GetText(label);
    }

    #endregion
}
