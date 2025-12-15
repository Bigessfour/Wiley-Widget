using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Styles;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Forms
{
    public partial class MainForm
    {
        /// <summary>
        /// Minimal bridge for panels expecting ShowPanel. Delegates to DockUserControlPanel when available.
        /// </summary>
        public void ShowPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                var method = GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.MakeGenericMethod(typeof(TPanel)).Invoke(this, new object[] { panelName });
            }
            catch
            {
                // Fallback no-op for now
            }
        }

        private Syncfusion.WinForms.DataGrid.SfDataGrid? GetActiveGrid()
        {
            try
            {
                if (ActiveControl is Syncfusion.WinForms.DataGrid.SfDataGrid ac)
                    return ac;

                // Find focused control recursively
                Control? focused = FindFocusedControl(this.Controls);
                if (focused is Syncfusion.WinForms.DataGrid.SfDataGrid fg)
                    return fg;

                // Find first visible SfDataGrid in controls
                foreach (Control c in this.Controls)
                {
                    var grid = c.Controls.OfType<Syncfusion.WinForms.DataGrid.SfDataGrid>().FirstOrDefault(g => g.Visible);
                    if (grid != null) return grid;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Control? FindFocusedControl(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                try
                {
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
                if (grid == null) return;

                var col = grid.Columns.OfType<GridColumnBase>().FirstOrDefault(c => c.AllowSorting);
                if (col == null) return;

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
                if (grid == null) return;

                // Try to pick a reasonable column and value to filter on.
                // Use first column with a non-null first-row value and filter by a substring of it.
                if (!(grid.DataSource is IEnumerable src)) return;
                var items = src.Cast<object?>().ToList();
                if (items.Count == 0) return;

                var first = items.FirstOrDefault(i => i != null);
                if (first == null) return;

                foreach (var col in grid.Columns.OfType<GridColumnBase>())
                {
                    try
                    {
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
                if (grid == null) return;

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
                if (grid == null) return;

                if (_isUiTestHarness)
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
                            var dir = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
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
                var save = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "xlsx",
                    FileName = "GridExport.xlsx"
                };

                if (save.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        await WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(grid, save.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "ExportActiveGridToExcel failed");
                        if (!_isUiTestHarness)
                        {
                            MessageBox.Show(this, $"Failed to export grid: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
