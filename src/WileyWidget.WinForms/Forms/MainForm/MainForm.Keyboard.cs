using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Keyboard shortcuts and event handlers for MainForm.
/// Separated into partial to keep core MainForm focused on lifecycle orchestration.
///
/// Shortcut Summary:
/// - Ctrl+F: Focus global search box
/// - Ctrl+Shift+T: Toggle theme
/// - Alt+D: Show Dashboard panel
/// - Alt+A: Show Accounts panel
/// - Alt+B: Show Budget panel
/// - Alt+C: Show Charts panel
/// - Alt+R: Show Reports panel
/// - Alt+S: Show Settings panel
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
            if (_ribbon == null)
            {
                _logger?.LogDebug("Ctrl+F pressed but ribbon is null - chrome may not be initialized");
                return false;
            }

            try
            {
                // Robust ribbon search: search each tab/panel and recursively inspect ToolStrip items
                ToolStripTextBox? searchBox = null;
                foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
                {
                    if (tab.Panel == null) continue;
                    foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                    {
                        searchBox = FindToolStripTextBoxRecursive(panel.Items, "GlobalSearch");
                        if (searchBox != null) break;
                    }
                    if (searchBox != null) break;
                }

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

            // Fallback: search whole form for named ToolStripTextBox (helps test harness or minimal ribbon)
            try
            {
                var fb = FindToolStripItem(this, "GlobalSearch") as ToolStripTextBox;
                if (fb != null && !fb.IsDisposed)
                {
                    fb.Focus();
                    fb.SelectAll();
                    return true;
                }
            }
            catch { }
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling theme");
            }
        }

        // [PERF] Alt+Left/Right/Up/Down: Keyboard Navigation Support
        // Reserved for future DockingKeyboardNavigator integration
        if ((keyData & Keys.Alt) == Keys.Alt &&
            (keyData & (Keys.Left | Keys.Right | Keys.Up | Keys.Down)) != 0)
        {
            _logger?.LogDebug("Keyboard navigation shortcut triggered: {KeyData}", keyData);
            return true;
        }

        // [PERF] Panel Navigation Shortcuts (Alt+key)
        if (keyData == (Keys.Alt | Keys.A))
        {
            return TryShowPanel<Controls.AccountsPanel>("Accounts", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.B))
        {
            return TryShowPanel<Controls.BudgetPanel>("Budget", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.C))
        {
            return TryShowPanel<Controls.BudgetAnalyticsPanel>("Charts", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.D))
        {
            return TryShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top);
        }

        if (keyData == (Keys.Alt | Keys.R))
        {
            return TryShowPanel<Controls.ReportsPanel>("Reports", DockingStyle.Right);
        }

        if (keyData == (Keys.Alt | Keys.S))
        {
            return TryShowPanel<Controls.SettingsPanel>("Settings", DockingStyle.Right);
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
        if (_ribbon == null)
        {
            return false;
        }

        if (FindToolStripItem(_ribbon, "GlobalSearch") is ToolStripTextBox searchBox)
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
        if (_ribbon == null)
        {
            return false;
        }

        if (FindToolStripItem(_ribbon, "GlobalSearch") is ToolStripTextBox searchBox)
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
            catch { }
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
            catch { }
        }
        return null;
    }
}
