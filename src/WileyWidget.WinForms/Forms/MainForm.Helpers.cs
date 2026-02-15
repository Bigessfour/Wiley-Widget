using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid;
using System.Collections;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// MainForm helper methods for grid operations, export, and panel navigation.
    /// Provides utility methods for working with Syncfusion SfDataGrid controls and panel management.
    /// </summary>
    public partial class MainForm
    {
        #region Internal Methods for Ribbon

        /// <summary>
        /// Shows a panel of specified type with the given name.
        /// Delegates to PanelNavigationService for centralized panel management.
        /// </summary>
        /// <typeparam name="TPanel">Type of panel control to show (must derive from UserControl)</typeparam>
        /// <param name="panelName">Name identifier for the panel</param>
        public void ShowPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            ShowPanel<TPanel>(panelName, preferredStyle: DockingStyle.Right, allowFloating: true);
        }

        /// <summary>
        /// Gets the currently active or focused SfDataGrid control.
        /// Searches through the control hierarchy with caching and specialized docking support.
        /// </summary>
        /// <returns>The active SfDataGrid, or null if none found</returns>
        private SfDataGrid? GetActiveGrid()
        {
            try
            {
                // Defensive: check for null/disposed state and empty collections
                if (IsDisposed || Controls == null || Controls.Count == 0) return null;

                // 1. Check Cache (TTL 500ms)
                if (_lastActiveGrid != null && !_lastActiveGrid.IsDisposed && _lastActiveGrid.Visible &&
                    (DateTime.Now - _lastActiveGridTime) < _activeGridCacheTtl)
                {
                    return _lastActiveGrid;
                }

                SfDataGrid? foundGrid = null;

                // 2. Check Form ActiveControl
                if (ActiveControl is SfDataGrid ac && !ac.IsDisposed)
                {
                    foundGrid = ac;
                }

                // 3. Check DockingManager ActiveControl (high priority in docking apps)
                if (foundGrid == null && _dockingManager?.ActiveControl != null)
                {
                    var activeDoc = _dockingManager.ActiveControl;
                    if (activeDoc is SfDataGrid dg && !dg.IsDisposed)
                    {
                        foundGrid = dg;
                    }
                    else
                    {
                        foundGrid = FindVisibleGridRecursive(activeDoc.Controls);
                    }
                }

                // 4. Recursive search for focused control
                if (foundGrid == null)
                {
                    Control? focused = FindFocusedControl(Controls);
                    if (focused is SfDataGrid fg && !fg.IsDisposed)
                    {
                        foundGrid = fg;
                    }
                }

                // 5. Deep recursive search for first visible grid (fallback)
                if (foundGrid == null)
                {
                    foundGrid = FindVisibleGridRecursive(Controls);
                }

                // Update Cache
                if (foundGrid != null)
                {
                    _lastActiveGrid = foundGrid;
                    _lastActiveGridTime = DateTime.Now;
                }

                return foundGrid;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "GetActiveGrid failed");
                return null;
            }
        }

        /// <summary>
        /// Invalidates the active grid discovery cache. Called on focus or docking changes.
        /// </summary>
        private void InvalidateActiveGridCache()
        {
            _lastActiveGrid = null;
            _lastActiveGridTime = DateTime.MinValue;
        }

        /// <summary>
        /// Recursively searches for the first visible SfDataGrid in a control collection.
        /// </summary>
        /// <param name="controls">Collection to search</param>
        /// <param name="depth">Current recursion depth (limit: 10)</param>
        /// <returns>The first visible SfDataGrid found, or null</returns>
        private SfDataGrid? FindVisibleGridRecursive(Control.ControlCollection? controls, int depth = 0)
        {
            if (controls == null || controls.Count == 0 || depth > 10) return null;

            foreach (Control c in controls)
            {
                if (c == null || c.IsDisposed) continue;

                if (c is SfDataGrid grid && grid.Visible)
                    return grid;

                if (c.Controls != null && c.Controls.Count > 0)
                {
                    var nested = FindVisibleGridRecursive(c.Controls, depth + 1);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for the currently focused control in a control collection.
        /// </summary>
        /// <param name="controls">Collection of controls to search</param>
        /// <param name="depth">Current recursion depth (limit: 10)</param>
        /// <returns>The focused control, or null if none found</returns>
        private Control? FindFocusedControl(Control.ControlCollection? controls, int depth = 0)
        {
            if (controls == null || controls.Count == 0 || depth > 10) return null;

            foreach (Control c in controls)
            {
                try
                {
                    if (c == null || c.IsDisposed) continue;
                    if (c.Focused) return c;

                    var nested = FindFocusedControl(c.Controls, depth + 1);
                    if (nested != null) return nested;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "FindFocusedControl iteration failed for control {ControlName}", c?.Name ?? "<unknown>");
                }
            }
            return null;
        }

        /// <summary>
        /// Sorts the active grid by its first sortable column.
        /// </summary>
        /// <param name="descending">If true, sorts in descending order; otherwise ascending</param>
        public void SortActiveGridByFirstSortableColumn(bool descending)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("SortActiveGrid: No active grid found");
                    return;
                }

                var col = grid.Columns?.OfType<GridColumnBase>().FirstOrDefault(c => c != null && c.AllowSorting);
                if (col == null || string.IsNullOrWhiteSpace(col.MappingName))
                {
                    _logger?.LogDebug("SortActiveGrid: No sortable column found");
                    return;
                }

                grid.SortByColumn(col.MappingName, descending);
                _logger?.LogDebug("Sorted grid by column {Column} ({Direction})", col.MappingName, descending ? "desc" : "asc");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SortActiveGrid failed");
            }
        }

        /// <summary>
        /// Clears all filters from the active grid.
        /// </summary>
        private void ClearActiveGridFilter()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ClearActiveGridFilter: No active grid found");
                    return;
                }

                grid.ClearFilters();
                _logger?.LogDebug("Cleared filters from active grid");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ClearActiveGridFilter failed");
            }
        }

        /// <summary>
        /// Applies a 'Contains' filter to a column in the active grid.
        /// </summary>
        /// <param name="columnName">Name of the column to filter</param>
        /// <param name="filterValue">Value to filter for (Contains search)</param>
        private void ApplyActiveGridFilter(string columnName, string filterValue)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ApplyActiveGridFilter: No active grid found");
                    return;
                }

                grid.ApplyTextContainsFilter(columnName, filterValue);
                _logger?.LogDebug("Applied filter '{Value}' to column {Column}", filterValue, columnName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ApplyActiveGridFilter failed for column {Column}", columnName);
            }
        }

        /// <summary>
        /// Clears all sorting from the active grid.
        /// </summary>
        private void ClearActiveGridSort()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ClearActiveGridSort: No active grid found");
                    return;
                }

                grid.ClearSort();
                _logger?.LogDebug("Cleared sorting from active grid");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ClearActiveGridSort failed");
            }
        }

        /// <summary>
        /// Auto-fits all columns in the active grid based on their content.
        /// </summary>
        private void AutoFitActiveGridColumns()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("AutoFitActiveGridColumns: No active grid found");
                    return;
                }

                grid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCells;
                _logger?.LogDebug("Auto-fitted columns in active grid");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "AutoFitActiveGridColumns failed");
            }
        }

        /// <summary>
        /// Exports the active grid to an Excel file.
        /// Shows a SaveFileDialog for user to choose file location.
        /// </summary>
        /// <returns>Task representing the async export operation</returns>
        private async Task ExportActiveGridToExcel(CancellationToken cancellationToken = default)
        {
            await ExportActiveGridInternal("Excel Files (*.xlsx)|*.xlsx", "xlsx", "GridExport.xlsx",
                (grid, path) => WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(grid, path, cancellationToken));
        }

        /// <summary>
        /// Exports the active grid to a PDF file.
        /// Shows a SaveFileDialog for user to choose file location.
        /// </summary>
        /// <returns>Task representing the async export operation</returns>
        private async Task ExportActiveGridToPdf(CancellationToken cancellationToken = default)
        {
            await ExportActiveGridInternal("PDF Files (*.pdf)|*.pdf", "pdf", "GridExport.pdf",
                (grid, path) => WileyWidget.WinForms.Services.ExportService.ExportGridToPdfAsync(grid, path, cancellationToken));
        }

        /// <summary>
        /// Internal helper for grid exports with common dialog and error handling logic.
        /// </summary>
        private async Task ExportActiveGridInternal(string filter, string ext, string defaultName, Func<SfDataGrid, string, Task> exportAction)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("Export: No active grid found");
                    return;
                }

                // Validate data before export
                if (grid.View.Records?.Count == 0)
                {
                    MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var save = new SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = ext,
                    FileName = defaultName
                };

                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(save.FileName)) return;

                    try
                    {
                        await exportAction(grid, save.FileName);
                        _logger?.LogInformation("Exported grid to {Ext}: {Path}", ext.ToUpperInvariant(), save.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Export to {Ext} failed", ext);
                        MessageBox.Show(this, $"Failed to export grid: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Export dialog or setup failed");
            }
        }

        /// <summary>
        /// Groups the active grid by the specified column.
        /// Enables grouping if not already enabled.
        /// </summary>
        /// <param name="columnName">Name of the column to group by</param>
        private void GroupActiveGridByColumn(string columnName)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("GroupActiveGridByColumn: No active grid found");
                    return;
                }

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    _logger?.LogDebug("GroupActiveGridByColumn: Column name is empty");
                    return;
                }

                // Enable grouping if not already enabled
                if (!grid.AllowGrouping)
                {
                    grid.AllowGrouping = true;
                }

                // Check if already grouped by this column
                var existingGroup = grid.GroupColumnDescriptions.FirstOrDefault(g => g.ColumnName == columnName);
                if (existingGroup != null)
                {
                    _logger?.LogDebug("GroupActiveGridByColumn: Already grouped by {Column}", columnName);
                    return;
                }

                // Add group description
                var groupDesc = new Syncfusion.WinForms.DataGrid.GroupColumnDescription
                {
                    ColumnName = columnName
                };
                grid.GroupColumnDescriptions.Add(groupDesc);

                _logger?.LogDebug("Grouped grid by column {Column}", columnName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "GroupActiveGridByColumn failed for column {Column}", columnName);
            }
        }

        /// <summary>
        /// Removes grouping from the active grid for the specified column.
        /// </summary>
        /// <param name="columnName">Name of the column to ungroup</param>
        private void UngroupActiveGridByColumn(string columnName)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("UngroupActiveGridByColumn: No active grid found");
                    return;
                }

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    _logger?.LogDebug("UngroupActiveGridByColumn: Column name is empty");
                    return;
                }

                var groupDesc = grid.GroupColumnDescriptions.FirstOrDefault(g => g.ColumnName == columnName);
                if (groupDesc == null)
                {
                    _logger?.LogDebug("UngroupActiveGridByColumn: Not grouped by {Column}", columnName);
                    return;
                }

                grid.GroupColumnDescriptions.Remove(groupDesc);
                _logger?.LogDebug("Ungrouped grid by column {Column}", columnName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "UngroupActiveGridByColumn failed for column {Column}", columnName);
            }
        }

        /// <summary>
        /// Clears all grouping from the active grid.
        /// </summary>
        private void ClearActiveGridGrouping()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ClearActiveGridGrouping: No active grid found");
                    return;
                }

                grid.GroupColumnDescriptions.Clear();
                _logger?.LogDebug("Cleared all grouping from active grid");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ClearActiveGridGrouping failed");
            }
        }

        /// <summary>
        /// Searches for text in the active grid.
        /// </summary>
        /// <param name="searchText">Text to search for</param>
        private void SearchActiveGrid(string searchText)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("SearchActiveGrid: No active grid found");
                    return;
                }

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Clear search
                    grid.SearchController.ClearSearch();
                    _logger?.LogDebug("Cleared search from active grid");
                    return;
                }

                // Perform search
                grid.SearchController.Search(searchText);
                _logger?.LogDebug("Searched grid for '{Text}'", searchText);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SearchActiveGrid failed for text '{Text}'", searchText);
            }
        }

        /// <summary>
        /// Refreshes the data in the active grid.
        /// </summary>
        private void RefreshActiveGrid()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("RefreshActiveGrid: No active grid found");
                    return;
                }

                grid.Refresh();
                _logger?.LogDebug("Refreshed active grid");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "RefreshActiveGrid failed");
            }
        }

        /// <summary>
        /// Notifies a panel's ViewModel of visibility changes to trigger lazy data loading.
        /// If the panel or its ViewModel implements ILazyLoadViewModel, calls OnVisibilityChangedAsync.
        /// </summary>
        /// <param name="control">The panel control to notify.</param>
        private async Task NotifyPanelVisibilityChangedAsync(Control control)
        {
            try
            {
                if (control == null || control.IsDisposed)
                {
                    return;
                }

                var isVisible = _dockingManager?.GetDockVisibility(control) ?? control.Visible;

                // First check if the control itself implements ILazyLoadViewModel (e.g., WarRoomPanel)
                if (control is WileyWidget.Abstractions.ILazyLoadViewModel controlLazyViewModel)
                {
                    _logger?.LogDebug("Notifying {PanelType} (control) visibility changed: isVisible={IsVisible}", control.GetType().Name, isVisible);
                    await controlLazyViewModel.OnVisibilityChangedAsync(isVisible).ConfigureAwait(true);
                    return;
                }

                // Check if control's DataContext (ViewModel) implements ILazyLoadViewModel
                var dataContext = control.GetType().GetProperty("DataContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(control);
                if (dataContext == null)
                {
                    // Try ViewModel property instead
                    dataContext = control.GetType().GetProperty("ViewModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(control);
                }

                // Check if ViewModel implements ILazyLoadViewModel
                if (dataContext is WileyWidget.Abstractions.ILazyLoadViewModel lazyViewModel)
                {
                    _logger?.LogDebug("Notifying {PanelType} (ViewModel) visibility changed: isVisible={IsVisible}", control.GetType().Name, isVisible);
                    await lazyViewModel.OnVisibilityChangedAsync(isVisible).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to notify panel visibility change for {ControlName}", control?.Name ?? "unknown");
            }
        }
    }
    #endregion
}
