using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Helpers;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Helper methods for MainForm: theme application, status updates, error dialogs, MRU management.
/// Separated into partial to keep core MainForm focused on lifecycle orchestration.
/// </summary>
public partial class MainForm
{
    /// <summary>
    /// Recursively applies theme to a control and all its children using breadth-first traversal.
    /// Skips disposed controls and handles errors per control to prevent one failure from blocking others.
    /// Supports all Syncfusion controls including GradientPanelExt, RibbonControlAdv, and nested containers.
    /// </summary>
    /// <param name="rootControl">The root control to apply theme to (typically 'this')</param>
    /// <param name="themeName">The theme name to apply (e.g., "Office2019Colorful")</param>
    private void ApplyThemeRecursive(Control rootControl, string themeName)
    {
        if (rootControl == null || rootControl.IsDisposed || string.IsNullOrWhiteSpace(themeName))
            return;

        // Validate theme name before applying
        themeName = AppThemeColors.ValidateTheme(themeName);

        try
        {
            // Use breadth-first queue-based traversal to avoid stack overflow on deep hierarchies
            var queue = new System.Collections.Generic.Queue<Control>();
            queue.Enqueue(rootControl);

            int appliedCount = 0;
            var processedControls = new System.Collections.Generic.HashSet<Control>();

            while (queue.Count > 0)
            {
                var control = queue.Dequeue();

                if (control == null || control.IsDisposed)
                    continue;

                // Skip if already processed (prevents re-application cycles)
                if (processedControls.Contains(control))
                    continue;

                processedControls.Add(control);

                // Apply theme to current control with per-control error handling
                try
                {
                    SfSkinManager.SetVisualStyle(control, themeName);
                    // Refresh for certain Syncfusion controls after theme applied
                    if (control is Syncfusion.WinForms.DataGrid.SfDataGrid ||
                        control.GetType().Name == "ChartControl" ||
                        control.GetType().Name == "RadialGauge")
                    {
                        control.Refresh();
                    }
                    appliedCount++;

                    // Log debug info for Syncfusion controls and custom panels
                    if (control is Syncfusion.WinForms.DataGrid.SfDataGrid or
                        Syncfusion.Windows.Forms.Tools.RibbonControlAdv or
                        WileyWidget.WinForms.Controls.GradientPanelExt)
                    {
                        _logger?.LogDebug("Applied theme '{Theme}' to {ControlType}: {ControlName}",
                            themeName, control.GetType().Name, control.Name ?? "<unnamed>");
                    }
                }
                catch (Exception ex)
                {
                    // Best-effort: log but continue with other controls
                    _logger?.LogDebug(ex, "Failed to apply theme to {ControlType} {ControlName} (non-fatal)",
                        control.GetType().Name, control.Name ?? "<unnamed>");
                }

                // Enqueue all non-null, non-disposed children for processing
                if (control.Controls != null && control.Controls.Count > 0)
                {
                    foreach (Control child in control.Controls)
                    {
                        if (child != null && !child.IsDisposed)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            _logger?.LogInformation("Applied theme '{Theme}' recursively to {ControlCount} controls",
                themeName, appliedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Recursive theme application failed for theme '{Theme}'", themeName);
        }
    }

    /// <summary>
    /// Apply the configured theme from SfSkinManager.ApplicationVisualTheme.
    /// Called post-initialization when docking panels are ready.
    /// Uses ApplyThemeRecursive to traverse the control tree and apply theme to all children,
    /// including dynamically created panels (GradientPanelExt) and Ribbon controls (RibbonControlAdv).
    /// </summary>
    private void ApplyTheme()
    {
        // Delay applying theme until the docking panels are set up to prevent NullReferenceExceptions
        if (_activityLogPanel == null || _leftDockPanel == null || _rightDockPanel == null)
        {
            _logger?.LogDebug("Theme apply skipped: Docking controls are not initialized yet");
            return;
        }

        var themeName = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

        try
        {
            // Apply theme recursively to form and all children
            ApplyThemeRecursive(this, themeName);
            _logger?.LogInformation("Applied SfSkinManager theme after initialization: {Theme}", themeName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Theme application failed after initialization");
        }
    }

    /// <summary>
    /// Thread-safe helper to update the status text panel from any thread.
    /// Automatically marshals to UI thread if needed.
    /// </summary>
    /// <param name="text">Status text to display.</param>
    private void ApplyStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Early disposal check before attempting any UI operations
        if (IsDisposed)
        {
            _logger?.LogDebug("ApplyStatus called on disposed form - ignoring");
            return;
        }

        try
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                try
                {
                    if (!IsDisposed)
                    {
                        this.SafeInvoke(() => ApplyStatus(text));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Handle destroyed or disposed during BeginInvoke
                    _logger?.LogDebug("BeginInvoke failed in ApplyStatus - form may be disposed");
                }
                catch { }
                return;
            }
        }
        catch { }

        try
        {
            if (_statusTextPanel != null && !_statusTextPanel.IsDisposed)
            {
                _statusTextPanel.Text = text;
                return;
            }

            if (_statusLabel != null && !_statusLabel.IsDisposed)
            {
                _statusLabel.Text = text;
                return;
            }
        }
        catch { }
    }

    /// <summary>
    /// Shows an error dialog to the user using themed SfMessageBox (if available) or standard MessageBox.
    /// Thread-safe: delegates to SfDialogHelper for UI marshaling and theme consistency.
    /// </summary>
    private void ShowErrorDialog(string title, string message)
    {
        UI.Helpers.SfDialogHelper.ShowErrorDialog(this, title, message, logger: _logger);
    }

    /// <summary>
    /// Shows an error dialog with exception details and collapsible stack trace.
    /// Logs the exception before displaying.
    /// Uses SfDialogHelper for themed dialog and optional exception details display.
    /// </summary>
    private void ShowErrorDialog(string title, string message, Exception ex)
    {
        _logger?.LogError(ex, "Error: {Message}", message);
        UI.Helpers.SfDialogHelper.ShowErrorDialog(this, title, message, exception: ex, logger: _logger);
    }

    /// <summary>
    /// Updates the MRU menu with the current MRU list.
    /// Each menu item opens the file when clicked.
    /// Validates file paths before adding to prevent orphaned entries.
    ///
    /// POLISH ENHANCEMENTS:
    /// - Displays short filename instead of full path to prevent truncation.
    /// - Full path shown in tooltip for reference without taking menu space.
    /// - Ellipsis handling for very long filenames.
    /// </summary>
    private void UpdateMruMenu(ToolStripMenuItem menu)
    {
        if (menu == null)
        {
            _logger?.LogWarning("UpdateMruMenu called with null menu");
            return;
        }

        menu.DropDownItems.Clear();

        // Validate and filter MRU list before adding to menu
        var validFiles = new System.Collections.Generic.List<string>();
        foreach (var file in _mruList)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;

            // Validate file exists before adding to menu
            if (!System.IO.File.Exists(file))
            {
                _logger?.LogDebug("MRU file no longer exists, skipping: {File}", file);
                continue;
            }

            validFiles.Add(file);

            // POLISH: Use short filename for display instead of full path
            var displayName = ShortenPathForDisplay(file, maxLength: 60);
            var item = new ToolStripMenuItem(displayName)
            {
                ToolTipText = file  // POLISH: Full path in tooltip
            };

            item.Click += async (s, e) =>
            {
                try
                {
                    // Re-check file existence before import (could have been deleted since menu opened)
                    if (!System.IO.File.Exists(file))
                    {
                        ShowErrorDialog("File Not Found", $"The file no longer exists:\n{file}");
                        return;
                    }

                    var result = await _fileImportService.ImportDataAsync<System.Collections.Generic.Dictionary<string, object>>(file);
                    HandleImportResult(file, result);
                }
                catch (System.IO.FileNotFoundException fnfEx)
                {
                    _logger?.LogWarning(fnfEx, "MRU file not found during import: {File}", file);
                    ShowErrorDialog("File Not Found", $"The file could not be found:\n{file}");
                }
                catch (System.IO.IOException ioEx)
                {
                    _logger?.LogError(ioEx, "IO error importing MRU file: {File}", file);
                    ShowErrorDialog("Import Error", $"Failed to read file:\n{file}\n\nError: {ioEx.Message}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error importing MRU file: {File}", file);
                    ShowErrorDialog("Import Error", $"Failed to import file:\n{System.IO.Path.GetFileName(file)}\n\nError: {ex.Message}");
                }
            };
            menu.DropDownItems.Add(item);
        }

        // Sync validated list back to _mruList if any files were removed
        if (validFiles.Count != _mruList.Count)
        {
            _mruList.Clear();
            _mruList.AddRange(validFiles);
            _logger?.LogDebug("MRU list cleaned: {Removed} invalid entries removed", _mruList.Count - validFiles.Count);
        }
    }

    /// <summary>
    /// POLISH: Shortens a file path for menu display, showing filename and optionally parent directory.
    /// Falls back to ellipsis if filename exceeds max length.
    /// </summary>
    /// <param name="filePath">Full file path.</param>
    /// <param name="maxLength">Maximum characters for display (default 60).</param>
    /// <returns>Shortened path suitable for menu display.</returns>
    private static string ShortenPathForDisplay(string filePath, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        try
        {
            var fileName = System.IO.Path.GetFileName(filePath);

            // If filename alone fits, return it
            if (fileName.Length <= maxLength)
            {
                return fileName;
            }

            // If filename is very long, use ellipsis
            if (fileName.Length > maxLength - 3)
            {
                return fileName.Substring(0, maxLength - 3) + "...";
            }

            // Try to include parent directory
            var directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                var parentDir = System.IO.Path.GetFileName(directory);
                var combined = $"{parentDir}\\{fileName}";

                if (combined.Length <= maxLength)
                {
                    return combined;
                }
            }

            // Fallback to truncated filename
            return fileName.Length > maxLength
                ? fileName.Substring(0, maxLength - 3) + "..."
                : fileName;
        }
        catch
        {
            // If any path operation fails, return the original
            return filePath;
        }
    }

    /// <summary>
    /// Clears the MRU list from both memory and persistent storage.
    /// </summary>
    private void ClearMruList()
    {
        _mruList.Clear();
        _windowStateService.ClearMru();
        UpdateMruMenu(_recentFilesMenu!);
    }

    /// <summary>
    /// Recursively collects all controls from a parent control.
    /// Used to find ribbon buttons for gating/enabling navigation.
    /// </summary>
    private void CollectAllControls(Control parent, List<Control> collected)
    {
        try
        {
            foreach (Control child in parent.Controls)
            {
                collected.Add(child);
                CollectAllControls(child, collected);
            }
        }
        catch
        {
            // Safe to ignore - collection errors don't block initialization
        }
    }
}
