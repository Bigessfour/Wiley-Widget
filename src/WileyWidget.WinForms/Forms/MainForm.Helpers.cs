using System.Threading;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// MainForm helper methods for grid operations, export, and panel navigation.
    /// Provides utility methods for working with Syncfusion SfDataGrid controls and panel management.
    /// </summary>
    public partial class MainForm
    {
        #region Internal Methods for RibbonFactory

        /// <summary>
        /// Shows a panel of specified type with the given name.
        /// Delegates to PanelNavigationService for centralized panel management.
        /// </summary>
        /// <typeparam name="TPanel">Type of panel control to show (must derive from UserControl)</typeparam>
        /// <param name="panelName">Name identifier for the panel</param>
        public void ShowPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                if (string.IsNullOrWhiteSpace(panelName))
                {
                    _logger?.LogWarning("ShowPanel called with null or empty panelName");
                    return;
                }

                // Delegate to PanelNavigationService for consistent panel management
                _panelNavigator?.ShowPanel<TPanel>(panelName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ShowPanel<{PanelType}>({PanelName}) failed", typeof(TPanel).Name, panelName);
            }
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
        private void SortActiveGridByFirstSortableColumn(bool descending)
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
        /// Applies a test filter to the active grid based on first row data.
        /// Useful for UI testing and demonstration purposes.
        /// </summary>
        private void ApplyTestFilterToActiveGrid()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ApplyTestFilter: No active grid found");
                    return;
                }

                // Try to pick a reasonable column and value to filter on.
                // Use first column with a non-null first-row value and filter by a substring of it.
                if (!(grid.DataSource is IEnumerable src))
                {
                    _logger?.LogDebug("ApplyTestFilter: Grid has no data source");
                    return;
                }

                var items = src.Cast<object?>().ToList();
                if (items.Count == 0)
                {
                    _logger?.LogDebug("ApplyTestFilter: Grid data source is empty");
                    return;
                }

                var first = items.FirstOrDefault(i => i != null);
                if (first == null)
                {
                    _logger?.LogDebug("ApplyTestFilter: No non-null items in data source");
                    return;
                }

                if (grid.Columns == null)
                {
                    _logger?.LogDebug("ApplyTestFilter: Grid has no columns");
                    return;
                }

                foreach (var col in grid.Columns.OfType<GridColumnBase>())
                {
                    try
                    {
                        if (col == null || string.IsNullOrWhiteSpace(col.MappingName)) continue;

                        var prop = first.GetType().GetProperty(col.MappingName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (prop == null) continue;
                        var val = prop.GetValue(first)?.ToString();
                        if (string.IsNullOrEmpty(val)) continue;

                        // Take a short substring to increase chance of matching multiple rows
                        var substr = val.Length <= 4 ? val : val.Substring(0, 4);
                        grid.ApplyTextContainsFilter(col.MappingName, substr);
                        _logger?.LogDebug("Applied test filter to column {Column} with value '{Value}'", col.MappingName, substr);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "ApplyTestFilter: Failed to process column {Column}", col?.MappingName ?? "<unknown>");
                    }
                }

                _logger?.LogDebug("ApplyTestFilter: No suitable column found for filtering");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ApplyTestFilterToActiveGrid failed");
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
        /// Exports the active grid to an Excel file.
        /// Shows a SaveFileDialog for user to choose file location.
        /// In UI test harness mode, creates a fake Excel file for testing.
        /// </summary>
        /// <returns>Task representing the async export operation</returns>
        private async Task ExportActiveGridToExcel(CancellationToken cancellationToken = default)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("ExportActiveGridToExcel: No active grid found");
                    return;
                }

                // Test harness mode: create fake Excel file
                if (_uiConfig.IsUiTestHarness)
                {
                    using var uiTestDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv",
                        DefaultExt = "xlsx",
                        FileName = "GridExport.xlsx"
                    };

                    if (uiTestDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            var path = uiTestDialog.FileName;
                            if (string.IsNullOrWhiteSpace(path)) return;

                            var dir = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            await File.WriteAllTextAsync(path, "%XLSX-FAKE\n");
                            _logger?.LogDebug("Created test harness fake Excel file: {Path}", path);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "ExportActiveGridToExcel (test harness) failed");
                        }
                    }

                    return;
                }

                // Normal operation: use ExportService
                using var save = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "xlsx",
                    FileName = "GridExport.xlsx"
                };

                if (save.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(save.FileName)) return;
                        await WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(grid, save.FileName);
                        _logger?.LogInformation("Exported grid to Excel: {Path}", save.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "ExportActiveGridToExcel failed");
                        if (_uiConfig != null && !_uiConfig.IsUiTestHarness && !IsDisposed)
                        {
                            try
                            {
                                MessageBox.Show(this, $"Failed to export grid: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            catch (ObjectDisposedException)
                            {
                                // Form disposed during error display - safe to ignore
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ExportActiveGridToExcel failed");
            }
        }
    }
    #endregion
}
