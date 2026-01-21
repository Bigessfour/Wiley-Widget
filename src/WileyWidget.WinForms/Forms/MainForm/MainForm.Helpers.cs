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
    /// Apply the configured theme from SfSkinManager.ApplicationVisualTheme.
    /// Called post-initialization when docking panels are ready.
    /// Note: Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally.
    /// No need to call SetVisualStyle here - it cascades automatically from the global setting.
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
            SfSkinManager.SetVisualStyle(this, themeName);
            SfSkinManager.SetVisualStyle(_activityLogPanel, themeName);
            SfSkinManager.SetVisualStyle(_leftDockPanel, themeName);
            SfSkinManager.SetVisualStyle(_rightDockPanel, themeName);
            SfSkinManager.SetVisualStyle(_centralDocumentPanel, themeName);
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
