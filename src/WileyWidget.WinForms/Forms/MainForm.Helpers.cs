using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid;
using System.Collections;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;

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
        /// Gets the currently active or focused SfDataGrid control.
        /// Searches through the control hierarchy with caching.
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

                // 3. Recursive search for focused control
                if (foundGrid == null)
                {
                    Control? focused = FindFocusedControl(Controls);
                    if (focused is SfDataGrid fg && !fg.IsDisposed)
                    {
                        foundGrid = fg;
                    }
                }

                // 4. Deep recursive search for first visible grid (fallback)
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
                (grid, path, ct) => WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(grid, path, ct),
                cancellationToken);
        }

        /// <summary>
        /// Exports the active grid to a PDF file.
        /// Shows a SaveFileDialog for user to choose file location.
        /// </summary>
        /// <returns>Task representing the async export operation</returns>
        private async Task ExportActiveGridToPdf(CancellationToken cancellationToken = default)
        {
            await ExportActiveGridInternal("PDF Files (*.pdf)|*.pdf", "pdf", "GridExport.pdf",
                (grid, path, ct) => WileyWidget.WinForms.Services.ExportService.ExportGridToPdfAsync(grid, path, ct),
                cancellationToken);
        }

        /// <summary>
        /// Internal helper for grid exports with common dialog and error handling logic.
        /// </summary>
        private async Task ExportActiveGridInternal(
            string filter,
            string ext,
            string defaultName,
            Func<SfDataGrid, string, CancellationToken, Task> exportAction,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var grid = GetActiveGrid();
                if (grid == null || grid.IsDisposed)
                {
                    _logger?.LogDebug("Export: No active grid found");
                    ApplyStatus("No active grid available to export.");
                    ShowErrorDialog("Export Data", "Select a panel with a grid before exporting.");
                    return;
                }

                // Validate data before export
                if ((grid.View?.Records?.Count ?? 0) == 0)
                {
                    ApplyStatus("No data available to export.");
                    MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                    owner: this,
                    operationKey: $"{nameof(MainForm)}.ActiveGrid.{ext}",
                    dialogTitle: $"Export Grid to {ext.ToUpperInvariant()}",
                    filter: filter,
                    defaultExtension: ext,
                    defaultFileName: defaultName,
                    exportAction: (filePath, ct) => exportAction(grid, filePath, ct),
                    statusCallback: message => ApplyStatus(message),
                    logger: _logger,
                    cancellationToken: cancellationToken);

                if (result.IsSkipped)
                {
                    MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (result.IsCancelled)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    ApplyStatus("Export failed.");
                    MessageBox.Show(this, $"Failed to export grid: {result.ErrorMessage}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ApplyStatus($"Export complete: {System.IO.Path.GetFileName(result.FilePath)}");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Export dialog or setup failed");
                ApplyStatus("Export failed.");
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

                var isVisible = control.Visible && control.Parent?.Visible != false && control.FindForm()?.Visible != false;

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

        #endregion

        #region Status and error helpers

        /// <summary>Applies status text to the status bar in a UI-thread-safe manner.</summary>
        private void ApplyStatus(string text)
        {
            try
            {
                var statusText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

                void updateStatus()
                {
                    if (_statusTextPanel != null && !_statusTextPanel.IsDisposed)
                    {
                        _statusTextPanel.Text = statusText;
                    }

                    if (_statusLabel != null && !_statusLabel.IsDisposed)
                    {
                        _statusLabel.Text = string.IsNullOrWhiteSpace(statusText) ? "Ready" : statusText;
                    }

                    if (_statusBar != null && !_statusBar.IsDisposed)
                    {
                        _statusBar.Invalidate();
                        _statusBar.Update();
                    }
                }

                if (InvokeRequired)
                {
                    BeginInvoke((System.Action)updateStatus);
                }
                else
                {
                    updateStatus();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "ApplyStatus failed");
            }
        }

        private void ConfigureStatusProgressBinding()
        {
            if (_statusProgressService == null)
            {
                return;
            }

            _statusProgressChangedHandler ??= (_, update) => ApplyStatusProgress(update);
            _statusProgressService.ProgressChanged -= _statusProgressChangedHandler;
            _statusProgressService.ProgressChanged += _statusProgressChangedHandler;
        }

        private void ApplyStatusProgress(StatusProgressUpdate update)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            void Apply()
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(update.Message))
                    {
                        ApplyStatus(update.Message);
                    }

                    if (_statePanel != null && !_statePanel.IsDisposed)
                    {
                        _statePanel.Text = update.IsActive
                            ? update.IsIndeterminate
                                ? "Working..."
                                : $"{Math.Round(update.Percent ?? 0d):0}%"
                            : "Ready";
                    }

                    if (_progressBar != null && !_progressBar.IsDisposed)
                    {
                        var value = (int)Math.Max(0d, Math.Min(100d, update.Percent ?? (update.IsActive ? 0d : 100d)));
                        _progressBar.Minimum = 0;
                        _progressBar.Maximum = 100;
                        _progressBar.ProgressStyle = update.IsIndeterminate
                            ? ProgressBarStyles.WaitingGradient
                            : _progressBar.ProgressStyle;
                        _progressBar.Visible = update.IsActive;
                        _progressBar.Value = value;
                    }

                    _statusBar?.Invalidate();
                    _statusBar?.Update();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "ApplyStatusProgress failed");
                }
            }

            if (InvokeRequired)
            {
                BeginInvoke((System.Action)Apply);
            }
            else
            {
                Apply();
            }
        }

        /// <summary>Shows an error dialog and updates status text.</summary>
        private void ShowErrorDialog(string title, string message)
        {
            ApplyStatus(message);

            if (IsUiTestEnvironment() || _uiConfig.IsUiTestHarness)
            {
                _logger?.LogDebug("Suppressed error dialog in test mode: {Title} - {Message}", title, message);
                return;
            }

            UIHelper.ShowMessageOnUI(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error, _logger);
        }

        /// <summary>Shows an error dialog with exception logging and status update.</summary>
        private void ShowErrorDialog(string title, string message, Exception ex)
        {
            _logger?.LogError(ex, "{Title}: {Message}", title, message);
            ShowErrorDialog(title, message);
        }

        #endregion
    }
}
