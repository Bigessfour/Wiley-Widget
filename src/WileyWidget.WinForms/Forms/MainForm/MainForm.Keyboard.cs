using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using Panels = WileyWidget.WinForms.Controls.Panels;

using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Keyboard shortcuts and event handlers for MainForm.
/// Separated into partial to keep core MainForm focused on lifecycle orchestration.
///
/// Shortcut Summary:
/// - Ctrl+F: Focus global search box
/// - Ctrl+Shift+F: Find in active grid
/// - Ctrl+Shift+T: Toggle theme
/// - Ctrl+Shift+S: Save current layout
/// - Ctrl+Shift+R: Reset layout to default
/// - Ctrl+L: Lock/unlock panel docking
/// - Alt+D: Show Enterprise Vital Signs panel
/// - Alt+A: Show Accounts panel
/// - Alt+B: Show Budget panel
/// - Alt+C: Show Analytics Hub panel
/// - Alt+R: Show Reports panel
/// - Alt+S: Show Settings panel
/// - Alt+W: Show War Room panel
/// - Alt+Q: Show QuickBooks panel
/// - Alt+J: Switch to JARVIS Chat
/// - Enter: Focus global search (from text boxes)
/// - Escape: Clear search or dismiss status text
/// </summary>
public partial class MainForm
{
    /// <summary>
    /// ProcessCmdKey override: Handles keyboard shortcuts globally for the form.
    /// Delegates common shortcuts (Enter, Escape) to specific handlers.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleDocumentSwitcherCmdKey(ref msg, keyData))
        {
            return true;
        }

        // [PERF] Standard keyboard shortcuts
        if (keyData == Keys.Enter)
        {
            if (HandleEnterShortcut())
            {
                return true;
            }
        }

        if (keyData == Keys.Escape)
        {
            if (HandleEscapeShortcut())
            {
                return true;
            }
        }

        // [PERF] Ctrl+F: Focus global search box
        if (keyData == (Keys.Control | Keys.F))
        {
            try
            {
                var searchBox = GetGlobalSearchTextBox();
                if (searchBox != null)
                {
                    try
                    {
                        if (!searchBox.IsDisposed)
                        {
                            searchBox.Focus();
                            searchBox.SelectAll();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger?.LogDebug("Search box disposed during focus attempt");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error focusing search box");
            }
        }

        // [PERF] Ctrl+Shift+F: Find in active grid
        if (keyData == (Keys.Control | Keys.Shift | Keys.F))
        {
            try
            {
                var prompt = Microsoft.VisualBasic.Interaction.InputBox(
                    "Find in active grid:",
                    "Find in Grid",
                    string.Empty);

                if (prompt != null)
                {
                    SearchActiveGrid(prompt);
                    return true;
                }
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogWarning(invEx, "Find-in-grid prompt failed due to invalid operation");
                MessageBox.Show(
                    "Search prompt could not be displayed. The grid may not be ready.",
                    "Search Unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error showing find-in-grid prompt. Error: {ErrorType}", ex.GetType().Name);
                UIHelper.ShowErrorOnUI(this, "Failed to show grid search dialog.", "Search Error", _logger);
            }
        }

        // [PERF] Ctrl+Shift+T: Toggle theme
        if (keyData == (Keys.Control | Keys.Shift | Keys.T))
        {
            try
            {
                if (_ribbon != null)
                {
                    var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                    themeToggle?.PerformClick();
                    return true;
                }
            }
            catch (ObjectDisposedException dispEx)
            {
                _logger?.LogDebug(dispEx, "Theme toggle button was disposed");
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogWarning(invEx, "Error toggling theme - operation invalid");
                MessageBox.Show(
                    "Theme toggle is not available right now. Please try again.",
                    "Theme Toggle Unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        // [NEW] Layout Management Shortcuts
        // Ctrl+Shift+S: Save current layout
        if (keyData == (Keys.Control | Keys.Shift | Keys.S))
        {
            try
            {
                SaveCurrentLayout();
                return true;
            }
            catch (System.IO.IOException ioEx)
            {
                _logger?.LogError(ioEx, "Error saving layout - file system error");
                UIHelper.ShowErrorOnUI(this,
                    "Failed to save layout. The layout file may be locked or inaccessible.",
                    "Save Layout Error", _logger);
            }
            catch (UnauthorizedAccessException authEx)
            {
                _logger?.LogError(authEx, "Error saving layout - access denied");
                UIHelper.ShowErrorOnUI(this,
                    "Failed to save layout. You may not have permission to write to the layout file.",
                    "Save Layout Error", _logger);
            }
        }

        // Ctrl+Shift+R: Reset layout to default
        if (keyData == (Keys.Control | Keys.Shift | Keys.R))
        {
            try
            {
                ResetLayout();
                return true;
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogError(invEx, "Error resetting layout - invalid operation");
                UIHelper.ShowErrorOnUI(this,
                    "Failed to reset layout. The docking manager may not be ready.",
                    "Reset Layout Error", _logger);
            }
        }

        // Ctrl+L: Toggle panel locking
        if (keyData == (Keys.Control | Keys.L))
        {
            try
            {
                TogglePanelLocking();
                return true;
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogError(invEx, "Error toggling panel locking - invalid operation");
                UIHelper.ShowErrorOnUI(this,
                    "Failed to toggle panel locking. The docking manager may not be ready.",
                    "Panel Lock Error", _logger);
            }
        }

        // [PERF] Alt+Left/Right/Up/Down: Keyboard Navigation Support
        // Reserved for future keyboard navigation integration
        if ((keyData & Keys.Alt) == Keys.Alt &&
            (keyData & (Keys.Left | Keys.Right | Keys.Up | Keys.Down)) != 0)
        {
            _logger?.LogDebug("Keyboard navigation shortcut triggered: {KeyData}", keyData);
            return true;
        }

        // [PERF] Panel Navigation Shortcuts (Alt+key)
        if (keyData == (Keys.Alt | Keys.A))
        {
            return TryShowPanel<Panels.AccountsPanel>("Accounts", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.C))
        {
            return TryShowPanel<Panels.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.D))
        {
            return TryShowPanel<EnterpriseVitalSignsPanel>("Enterprise Vital Signs", DockingStyle.Fill);
        }

        if (keyData == (Keys.Alt | Keys.R))
        {
            return TryShowPanel<Panels.ReportsPanel>("Reports", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.S))
        {
            return TryShowPanel<Panels.SettingsPanel>("Settings", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.W))
        {
            return TryShowPanel<Panels.WarRoomPanel>("War Room", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.Q))
        {
            return TryShowPanel<Panels.QuickBooksPanel>("QuickBooks", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.J))
        {
            return TryShowPanel<JARVISChatUserControl>("JARVIS Chat", DockingStyle.Bottom);
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// Helper: Shows a panel safely, catching exceptions.
    /// </summary>
    private bool TryShowPanel<TPanel>(string panelName, DockingStyle style) where TPanel : UserControl
    {
        try
        {
            _panelNavigator?.ShowPanel<TPanel>(panelName, style, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error showing {PanelName} panel", panelName);
            return false;
        }
    }

    /// <summary>
    /// Helper: Shows a Form safely, catching exceptions.
    /// Mirrors TryShowPanel but for form-hosted dashboards and other forms.
    /// </summary>
    private bool TryShowForm<TForm>(string panelName, DockingStyle style) where TForm : Form
    {
        try
        {
            ShowForm<TForm>(panelName, style, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error showing {PanelName} form", panelName);
            return false;
        }
    }



    /// <summary>
    /// Handles Enter key: Focus global search from non-text-box controls.
    /// Skips if ActiveControl is a text box (allow normal Enter behavior).
    /// </summary>
    private bool HandleEnterShortcut()
    {
        if (ActiveControl is TextBoxBase || ActiveControl is MaskedTextBox)
        {
            return false;
        }

        return FocusGlobalSearchTextBox(selectAll: true);
    }

    /// <summary>
    /// Handles Escape key: Clear search text or dismiss status message.
    /// </summary>
    private bool HandleEscapeShortcut()
    {
        if (TryClearSearchText())
        {
            return true;
        }

        if (_statusTextPanel != null && !_statusTextPanel.IsDisposed && !string.IsNullOrWhiteSpace(_statusTextPanel.Text))
        {
            _statusTextPanel.Text = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Focuses the global search text box in the ribbon.
    /// </summary>
    private bool FocusGlobalSearchTextBox(bool selectAll)
    {
        var searchBox = GetGlobalSearchTextBox();
        if (searchBox != null)
        {
            try
            {
                if (!searchBox.IsDisposed)
                {
                    searchBox.Focus();
                    if (selectAll)
                    {
                        searchBox.SelectAll();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("Search box disposed during focus");
                return false;
            }
            catch (InvalidOperationException)
            {
                _logger?.LogDebug("Search box not available for focus (disposed or invalid state)");
                return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the global search text box if it contains text.
    /// </summary>
    private bool TryClearSearchText()
    {
        var searchBox = GetGlobalSearchTextBox();
        if (searchBox != null)
        {
            if (!string.IsNullOrEmpty(searchBox.Text))
            {
                searchBox.Clear();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets up a default cancel button to handle Escape and other keyboard shortcuts.
    /// Required for WinForms keyboard event routing.
    /// </summary>
    private void EnsureDefaultActionButtons()
    {
        if (_defaultCancelButton == null)
        {
            _defaultCancelButton = new Button
            {
                Name = "DefaultCancelButton",
                AccessibleName = "Cancel current action",
                AccessibleDescription = "Press Escape to clear search or dismiss status text",
                TabStop = false,
                Visible = false,
                Size = new System.Drawing.Size(1, 1),
                Location = new System.Drawing.Point(-1000, -1000)
            };
            _defaultCancelButton.Click += (s, e) => HandleEscapeShortcut();
            Controls.Add(_defaultCancelButton);
        }

        CancelButton = _defaultCancelButton;
    }

    /// <summary>
    /// Finds a ToolStripItem in the ribbon by name.
    /// Recursively searches all tabs and panels.
    /// </summary>
    private ToolStripItem? FindToolStripItem(RibbonControlAdv ribbon, string name)
    {
        foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
        {
            if (tab.Panel != null)
            {
                foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                {
                    var item = FindToolStripItemRecursive(panel.Items, name);
                    if (item != null) return item;
                }
            }
        }
        return null;
    }

    private ToolStripItem? FindToolStripItemRecursive(ToolStripItemCollection items, string name)
    {
        foreach (ToolStripItem item in items)
        {
            try
            {
                if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) return item;

                if (item is ToolStripPanelItem panelItem)
                {
                    var found = FindToolStripItemRecursive(panelItem.Items, name);
                    if (found != null) return found;
                }

                if (item is ToolStripDropDownItem dropDown)
                {
                    var found = FindToolStripItemRecursive(dropDown.DropDownItems, name);
                    if (found != null) return found;
                }
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("ToolStripItem {ItemName} was disposed during recursive search", item.Name);
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogDebug(invEx, "Invalid operation while searching ToolStripItem {ItemName}", item.Name);
            }
        }
        return null;
    }

    private ToolStripTextBox? FindToolStripTextBoxRecursive(ToolStripItemCollection items, string name)
    {
        foreach (ToolStripItem item in items)
        {
            try
            {
                if (item is ToolStripTextBox tb)
                {
                    if (string.Equals(tb.Name, name, StringComparison.OrdinalIgnoreCase)) return tb;
                }
                else if (item is ToolStripPanelItem panelItem)
                {
                    var found = FindToolStripTextBoxRecursive(panelItem.Items, name);
                    if (found != null) return found;
                }
                else if (item is ToolStripDropDownItem dropDown)
                {
                    var found = FindToolStripTextBoxRecursive(dropDown.DropDownItems, name);
                    if (found != null) return found;
                }
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogDebug("ToolStripItem {ItemName} was disposed during text box search", item.Name);
            }
            catch (InvalidOperationException invEx)
            {
                _logger?.LogDebug(invEx, "Invalid operation while searching for ToolStripTextBox {ItemName}", item.Name);
            }
        }
        return null;
    }

}
