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
    /// Cleaned up from MainForm.Extensions.cs - removed all MDI dependencies.
    /// </summary>
    public partial class MainForm
    {
        /// <summary>
        /// Minimal bridge for panels expecting ShowPanel. Delegates to PanelNavigationService.
        /// </summary>
        public void ShowPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                if (string.IsNullOrWhiteSpace(panelName)) return;

                // Delegate to PanelNavigationService
                _panelNavigator?.ShowPanel<TPanel>(panelName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ShowPanel<{PanelType}>({PanelName}) failed", typeof(TPanel).Name, panelName);
            }
        }

        private SfDataGrid? GetActiveGrid()
        {
            try
            {
                // Defensive: check for null and disposed state
                if (IsDisposed || Controls == null) return null;

                if (ActiveControl is SfDataGrid ac && !ac.IsDisposed)
                    return ac;

                // Find focused control recursively
                Control? focused = FindFocusedControl(Controls);
                if (focused is SfDataGrid fg && !fg.IsDisposed)
                    return fg;

                // Find first visible SfDataGrid in controls
                foreach (Control c in Controls)
                {
                    if (c == null || c.IsDisposed) continue;
                    var grid = c.Controls.OfType<SfDataGrid>()
                        .FirstOrDefault(g => g != null && !g.IsDisposed && g.Visible);
                    if (grid != null) return grid;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Control? FindFocusedControl(Control.ControlCollection? controls)
        {
            if (controls == null) return null;

            foreach (Control c in controls)
            {
                try
                {
                    // Defensive: check for null and disposed state
                    if (c == null || c.IsDisposed) continue;
                    if (c.Focused) return c;

                    var nested = FindFocusedControl(c.Controls);
                    if (nested != null) return nested;
                }
                catch { }
            }
            return null;
        }

        private void SortActiveGridByFirstSortableColumn(bool descending)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed) return;

                var col = grid.Columns?.OfType<GridColumnBase>().FirstOrDefault(c => c != null && c.AllowSorting);
                if (col == null || string.IsNullOrWhiteSpace(col.MappingName)) return;

                grid.SortByColumn(col.MappingName, descending);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SortActiveGrid failed");
            }
        }

        private void ApplyTestFilterToActiveGrid()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed) return;

                // Try to pick a reasonable column and value to filter on.
                // Use first column with a non-null first-row value and filter by a substring of it.
                if (!(grid.DataSource is IEnumerable src)) return;
                var items = src.Cast<object?>().ToList();
                if (items.Count == 0) return;

                var first = items.FirstOrDefault(i => i != null);
                if (first == null) return;

                if (grid.Columns == null) return;

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
                        return;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ApplyTestFilterToActiveGrid failed");
            }
        }

        private void ClearActiveGridFilter()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed) return;

                grid.ClearFilters();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ClearActiveGridFilter failed");
            }
        }

        private async Task ExportActiveGridToExcel()
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed) return;

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
}
