using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Logging;
using Syncfusion.Data;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Enums;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Extension methods for Syncfusion SfDataGrid to prevent common runtime errors and improve reliability.
    /// </summary>
    public static class SfDataGridExtensions
    {
        /// <summary>
        /// Prevents System.InvalidOperationException from relational operators on string columns.
        ///
        /// Modern Syncfusion versions (2024+) typically hide relational operators in the UI for string columns,
        /// but this provides defense-in-depth for programmatic filters, custom UI, or older versions.
        ///
        /// The SfDataGrid UI can allow relational filters (>, >=, <, <=) to be applied to string columns,
        /// but LINQ expression trees do not support these operators on strings, causing:
        /// "The binary operator GreaterThan is not defined for the types 'System.String' and 'System.String'"
        ///
        /// Call this method once during grid initialization to automatically prevent these invalid filters.
        /// </summary>
        /// <param name="grid">SfDataGrid instance to protect.</param>
        /// <param name="logger">Optional logger for debugging filter prevention.</param>
        /// <param name="stringColumns">
        /// Optional explicit list of string column names. If null/empty, all columns will be checked dynamically.
        /// For best performance, specify string columns explicitly when known.
        /// </param>
        /// <returns>The same grid instance for fluent chaining.</returns>
        /// <example>
        /// <code>
        /// _myGrid = new SfDataGrid { Dock = DockStyle.Fill }
        ///     .PreventStringRelationalFilters(_logger, "Name", "Status", "Category");
        /// </code>
        /// </example>
        public static SfDataGrid PreventStringRelationalFilters(
            this SfDataGrid grid,
            ILogger? logger = null,
            params string[] stringColumns)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            // Create handler once for idempotent subscription
            FilterChangingEventHandler handler = (sender, e) =>
            {
                // Handle null safety
                if (e?.Column?.MappingName == null || e.FilterPredicates == null)
                {
                    return;
                }

                // Determine if this is a string column
                bool isStringColumn;
                if (stringColumns != null && stringColumns.Length > 0)
                {
                    // Use explicit list if provided (fastest)
                    isStringColumn = stringColumns.Contains(e.Column.MappingName, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    // Auto-detect by checking GridColumn type or data type
                    isStringColumn = IsStringColumn(grid, e.Column);
                }

                if (!isStringColumn)
                {
                    return;
                }

                // Check if any predicate uses relational operators
                var hasRelationalPredicate = e.FilterPredicates.Any(p =>
                    p.FilterType == FilterType.GreaterThan ||
                    p.FilterType == FilterType.GreaterThanOrEqual ||
                    p.FilterType == FilterType.LessThan ||
                    p.FilterType == FilterType.LessThanOrEqual);

                if (hasRelationalPredicate)
                {
                    // Cancel the filter and log
                    e.Cancel = true;
                    logger?.LogDebug(
                        "SfDataGrid: Prevented invalid relational filter on string column '{ColumnName}'. " +
                        "Relational operators (>, >=, <, <=) are not supported on string columns.",
                        e.Column.MappingName);
                }
            };

            // Unsubscribe first to ensure idempotent behavior (safe to call even if not subscribed)
            grid.FilterChanging -= handler;
            grid.FilterChanging += handler;

            return grid;
        }

        /// <summary>
        /// Attempts to determine if a column contains string data.
        /// Uses fast type checks first, then DataTable inspection, then reflection.
        /// </summary>
        private static bool IsStringColumn(SfDataGrid grid, GridColumn column)
        {
            // Fast path: Check column type directly (no reflection needed)
            if (column is GridTextColumn)
            {
                return true;
            }

            // Fast path: Explicitly non-string column types
            if (column is GridNumericColumn ||
                column is GridDateTimeColumn ||
                column is GridCheckBoxColumn ||
                column is GridImageColumn ||
                column is GridHyperlinkColumn)
            {
                return false;
            }

            // Check if it's an unbound column with string data
            if (column is GridUnboundColumn unboundColumn)
            {
                // Unbound columns with expressions may return strings
                return unboundColumn.Expression?.Contains("ToString", StringComparison.Ordinal) == true ||
                       unboundColumn.Format?.Contains("0", StringComparison.Ordinal) == false; // Non-numeric format hints string
            }

            // Medium path: Check DataTable column type (common scenario)
            if (grid.DataSource is DataTable dataTable)
            {
                if (dataTable.Columns.Contains(column.MappingName))
                {
                    return dataTable.Columns[column.MappingName]!.DataType == typeof(string);
                }
            }

            // Slow path: Use reflection to check the mapped property type from the data source
            if (grid.View != null && grid.View.SourceCollection != null)
            {
                try
                {
                    var sourceType = grid.View.SourceCollection.GetType();
                    var genericArgs = sourceType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var itemType = genericArgs[0];
                        var property = itemType.GetProperty(column.MappingName);
                        if (property != null)
                        {
                            return property.PropertyType == typeof(string);
                        }
                    }
                }
                catch
                {
                    // Fall through to default behavior if reflection fails
                }
            }

            // Default: assume non-string unless we can confirm otherwise
            // This is safer than assuming string, as false negatives (missing protection)
            // are less harmful than false positives (blocking valid numeric filters)
            return false;
        }

        /// <summary>
        /// Convenience method for when explicit string columns are not known.
        /// This will auto-detect string columns at runtime, but may have performance overhead.
        /// Prefer the overload that accepts explicit column names when possible.
        /// </summary>
        public static SfDataGrid PreventStringRelationalFilters(
            this SfDataGrid grid,
            ILogger? logger = null)
        {
            return PreventStringRelationalFilters(grid, logger, Array.Empty<string>());
        }

        /// <summary>
        /// Sorts the grid by a specific column.
        /// </summary>
        /// <param name="grid">The SfDataGrid to sort.</param>
        /// <param name="columnName">The name of the column to sort by.</param>
        /// <param name="descending">True to sort descending, false for ascending.</param>
        public static void SortByColumn(this SfDataGrid grid, string columnName, bool descending = false)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("Column name cannot be empty", nameof(columnName));

            grid.SortColumnDescriptions.Clear();
            grid.SortColumnDescriptions.Add(new SortColumnDescription
            {
                ColumnName = columnName,
                SortDirection = descending ? ListSortDirection.Descending : ListSortDirection.Ascending
            });
        }

        /// <summary>
        /// Applies a 'Contains' text filter to the grid view.
        /// Note: This uses View.Filter for programmatic filtering, not column-level UI filters.
        /// </summary>
        /// <param name="grid">The SfDataGrid to filter.</param>
        /// <param name="columnName">The name of the column to filter.</param>
        /// <param name="filterValue">The text value to search for (case-insensitive contains).</param>
        public static void ApplyTextContainsFilter(this SfDataGrid grid, string columnName, string filterValue)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (grid.View == null) return;
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("Column name cannot be empty", nameof(columnName));

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                grid.View.Filter = null;
                grid.View.RefreshFilter();
                return;
            }

            grid.View.Filter = item =>
            {
                if (item == null) return false;

                var property = item.GetType().GetProperty(columnName);
                if (property == null) return false;

                var value = property.GetValue(item);
                if (value == null) return false;

                var stringValue = value.ToString();
                if (string.IsNullOrEmpty(stringValue)) return false;

                return stringValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase);
            };
            grid.View.RefreshFilter();
        }

        /// <summary>
        /// Clears all sorting from the grid.
        /// </summary>
        /// <param name="grid">The SfDataGrid to clear sorting from.</param>
        public static void ClearSort(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            grid.SortColumnDescriptions.Clear();
        }

        /// <summary>
        /// Clears all programmatic filters from the grid.
        /// Note: This clears View.Filter, not column-level UI filters.
        /// </summary>
        /// <param name="grid">The SfDataGrid to clear filters from.</param>
        public static void ClearFilters(this SfDataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (grid.View != null)
            {
                grid.View.Filter = null;
                grid.View.RefreshFilter();
            }
        }
    }
}
