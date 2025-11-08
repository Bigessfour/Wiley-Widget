using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.Definitions;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Helper methods for interacting with Syncfusion SfDataGrid controls via FlaUI.
/// </summary>
public static class SyncfusionHelpers
{
    /// <summary>
    /// Gets the row count from a Syncfusion SfDataGrid.
    /// </summary>
    public static int GetDataGridRowCount(FlaUI.Core.AutomationElements.AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        // SfDataGrid exposes rows as DataItem controls in UI Automation
        var rows = dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        return rows.Length;
    }

    /// <summary>
    /// Gets all visible row elements from the data grid.
    /// </summary>
    public static FlaUI.Core.AutomationElements.AutomationElement[] GetAllRows(FlaUI.Core.AutomationElements.AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        return dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
    }

    /// <summary>
    /// Gets cell text from a specific row and column index.
    /// </summary>
    public static string? GetCellText(FlaUI.Core.AutomationElements.AutomationElement row, int columnIndex)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));

        var cells = row.FindAllChildren(cf => cf.ByControlType(ControlType.Text));
        if (columnIndex < 0 || columnIndex >= cells.Length)
        {
            return null;
        }

        return cells[columnIndex].Name;
    }

    /// <summary>
    /// Gets all cell values from a specific column across all rows.
    /// </summary>
    public static List<string> GetColumnValues(FlaUI.Core.AutomationElements.AutomationElement dataGrid, int columnIndex)
    {
        var values = new List<string>();
        var rows = GetAllRows(dataGrid);

        foreach (var row in rows)
        {
            var cellText = GetCellText(row, columnIndex);
            if (!string.IsNullOrEmpty(cellText))
            {
                values.Add(cellText);
            }
        }

        return values;
    }

    /// <summary>
    /// Counts rows matching a specific filter condition.
    /// </summary>
    public static int CountRowsWhere(FlaUI.Core.AutomationElements.AutomationElement dataGrid, Func<FlaUI.Core.AutomationElements.AutomationElement, bool> predicate)
    {
        var rows = GetAllRows(dataGrid);
        return rows.Count(predicate);
    }

    /// <summary>
    /// Applies a filter to the data grid by typing in a filter textbox.
    /// Assumes Syncfusion filter box is above the grid with Name="Filter".
    /// </summary>
    public static void ApplyFilter(FlaUI.Core.AutomationElements.AutomationElement dataGrid, string filterText)
    {
        if (dataGrid == null)
        {
            throw new ArgumentNullException(nameof(dataGrid));
        }

        // Syncfusion filter boxes are typically TextBox controls above the grid
        var filterBox = dataGrid.Parent.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Edit).And(cf.ByName("Filter")));

        if (filterBox != null)
        {
            filterBox.AsTextBox().Text = filterText;
            System.Threading.Thread.Sleep(500); // Wait for filter to apply
        }
    }

    /// <summary>
    /// Gets the header text from a Syncfusion SfDataGrid.
    /// </summary>
    public static List<string> GetColumnHeaders(FlaUI.Core.AutomationElements.AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        var headers = dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header));
        return headers.Select(h => h.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
    }

    /// <summary>
    /// Clicks a specific row by index (0-based).
    /// </summary>
    public static void ClickRow(FlaUI.Core.AutomationElements.AutomationElement dataGrid, int rowIndex)
    {
        var rows = GetAllRows(dataGrid);
        if (rowIndex < 0 || rowIndex >= rows.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), $"Row index {rowIndex} out of range (0-{rows.Length - 1})");
        }

        rows[rowIndex].Click();
    }

    /// <summary>
    /// Gets the selected row index (returns -1 if no selection).
    /// </summary>
    public static int GetSelectedRowIndex(FlaUI.Core.AutomationElements.AutomationElement dataGrid)
    {
        var rows = GetAllRows(dataGrid);
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].Patterns.SelectionItem.Pattern.IsSelected)
            {
                return i;
            }
        }

        return -1;
    }
}
