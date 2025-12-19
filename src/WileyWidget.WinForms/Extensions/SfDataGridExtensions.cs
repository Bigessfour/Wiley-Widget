using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Styles;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Helper extensions for working with <see cref="SfDataGrid"/> in a testable, defensive manner.
    /// Provides programmatic sorting, simple text filtering (data-source based fallback),
    /// and convenient export wiring to <see cref="Services.ExportService"/>.
    /// </summary>
    public static class SfDataGridExtensions
    {
        private static readonly ConditionalWeakTable<SfDataGrid, object> _originalSources = new();

        public static void SaveOriginalDataSource(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            _originalSources.GetValue(grid, _ => grid.DataSource ?? Array.Empty<object>());
        }

        public static void RestoreOriginalDataSource(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (_originalSources.TryGetValue(grid, out var original))
            {
                try { grid.DataSource = original; } catch { }
            }
        }

        public static void ClearSavedDataSource(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            try { _originalSources.Remove(grid); } catch { }
        }

        public static void SortByColumn(this SfDataGrid grid, string columnName, bool descending = false)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("columnName cannot be null or empty", nameof(columnName));

            grid.SortColumnDescriptions.Clear();
            grid.SortColumnDescriptions.Add(new SortColumnDescription
            {
                ColumnName = columnName,
                SortDirection = descending ? ListSortDirection.Descending : ListSortDirection.Ascending
            });
        }

        public static void ClearSort(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            grid.SortColumnDescriptions.Clear();
        }

        /// <summary>
        /// Apply a simple text 'contains' filter against the grid's current data source.
        /// This is intentionally non-invasive: it saves the original data source so callers
        /// may call <see cref="RestoreOriginalDataSource"/> to revert.
        /// This approach is a pragmatic fallback for programmatic filtering when Syncfusion
        /// filter predicate APIs are not convenient to use from tests.
        /// </summary>
        public static void ApplyTextContainsFilter(this SfDataGrid grid, string columnName, string containsText)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("columnName cannot be null or empty", nameof(columnName));

            grid.SaveOriginalDataSource();

            if (!(grid.DataSource is IEnumerable source))
                return;

            var items = source.Cast<object?>().ToList();
            if (items.Count == 0)
                return;

            var filtered = items.Where(item =>
            {
                try
                {
                    var prop = item?.GetType().GetProperty(columnName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null) return false;
                    var val = prop.GetValue(item)?.ToString();
                    return !string.IsNullOrEmpty(val) && val.IndexOf(containsText ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch
                {
                    return false;
                }
            }).ToList();

            grid.DataSource = filtered;
        }

        public static void ClearFilters(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            // Prefer Syncfusion's native clearing if available
            try
            {
                grid.SortColumnDescriptions.Clear();
            }
            catch { }

            // Restore original data if we saved it earlier
            grid.RestoreOriginalDataSource();
            grid.ClearSavedDataSource();
        }
    }
}
