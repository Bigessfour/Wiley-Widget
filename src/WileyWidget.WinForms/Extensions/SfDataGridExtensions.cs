using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WileyWidget.WinForms.Helpers;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.Data;

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
                try { grid.SafeInvoke(() => grid.DataSource = original); } catch { }
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
        /// Apply a simple text 'contains' filter against the grid using native FilterPredicates.
        /// This hides rows that don't match the criteria.
        /// </summary>
        public static void ApplyTextContainsFilter(this SfDataGrid grid, string columnName, string containsText)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(columnName)) return;

            var column = grid.Columns[columnName];
            if (column == null) return;

            // Clear existing predicates for this column
            column.FilterPredicates.Clear();

            if (!string.IsNullOrEmpty(containsText))
            {
                column.FilterPredicates.Add(new Syncfusion.Data.FilterPredicate
                {
                    FilterType = Syncfusion.Data.FilterType.Contains,
                    FilterValue = containsText
                });
            }
        }

        /// <summary>
        /// Clears all filters (FilterPredicates) from all columns in the grid.
        /// </summary>
        public static void ClearFilters(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            foreach (var column in grid.Columns)
            {
                column.FilterPredicates.Clear();
            }

            // Also check for DataSource swapping fallback from legacy code
            grid.RestoreOriginalDataSource();
            grid.ClearSavedDataSource();
        }
    }
}
