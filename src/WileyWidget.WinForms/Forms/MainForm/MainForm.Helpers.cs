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

        try
        {
            // Use breadth-first queue-based traversal to avoid stack overflow on deep hierarchies
            var queue = new System.Collections.Generic.Queue<Control>();
            queue.Enqueue(rootControl);

            int appliedCount = 0;
            while (queue.Count > 0)
            {
                var control = queue.Dequeue();

                if (control == null || control.IsDisposed)
                    continue;

                // Apply theme to current control with per-control error handling
                try
                {
                    SfSkinManager.SetVisualStyle(control, themeName);
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

        try
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                try { this.BeginInvoke(new System.Action(() => ApplyStatus(text))); } catch { }
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
    /// Shows an error dialog to the user.
    /// Thread-safe: delegates to UIHelper for UI marshaling.
    /// </summary>
    private void ShowErrorDialog(string title, string message)
    {
        UIHelper.ShowErrorOnUI(this, message, title, _logger);
    }

    /// <summary>
    /// Shows an error dialog with exception details.
    /// Logs the exception before displaying.
    /// </summary>
    private void ShowErrorDialog(string title, string message, Exception ex)
    {
        _logger?.LogError(ex, "Error: {Message}", message);
        UIHelper.ShowErrorOnUI(this, message, title, _logger);
    }

    /// <summary>
    /// Updates the MRU menu with the current MRU list.
    /// Each menu item opens the file when clicked.
    /// </summary>
    private void UpdateMruMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        foreach (var file in _mruList)
        {
            var item = new ToolStripMenuItem(file);
            item.Click += async (s, e) =>
            {
                var result = await _fileImportService.ImportDataAsync<System.Collections.Generic.Dictionary<string, object>>(file);
                HandleImportResult(file, result);
            };
            menu.DropDownItems.Add(item);
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
}
