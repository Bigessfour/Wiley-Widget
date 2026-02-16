using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Enhanced keyboard navigation for MainForm with Ctrl+Tab document switcher.
/// Professional keyboard shortcuts matching Visual Studio and Office patterns.
/// </summary>
public partial class MainForm
{
    private Form? _documentSwitcherDialog;
    private ListBox? _documentSwitcherList;
    private bool _switcherActive;

    /// <summary>
    /// Handles document switcher shortcuts for ProcessCmdKey.
    /// </summary>
    private bool HandleDocumentSwitcherCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            // Document navigation shortcuts
            if (keyData == (Keys.Control | Keys.Tab))
            {
                ActivateNextDocumentWithSwitcher();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                ActivatePreviousDocumentWithSwitcher();
                return true;
            }

            // Close current document
            if (keyData == (Keys.Control | Keys.W) || keyData == (Keys.Control | Keys.F4))
            {
                CloseActiveDocument();
                return true;
            }

            // Close all documents
            if (keyData == (Keys.Control | Keys.Shift | Keys.W))
            {
                CloseAllDocuments();
                return true;
            }

            // Jump to document by number (Alt+1 through Alt+9)
            if ((keyData & Keys.Alt) == Keys.Alt)
            {
                var digit = keyData & ~Keys.Alt;
                if (digit >= Keys.D1 && digit <= Keys.D9)
                {
                    var index = (int)(digit - Keys.D1);
                    ActivateDocumentByIndex(index);
                    return true;
                }
            }

            // Full screen mode
            if (keyData == Keys.F11)
            {
                ToggleFullScreen();
                return true;
            }

            // Global search
            if (keyData == (Keys.Control | Keys.K))
            {
                FocusGlobalSearch();
                return true;
            }

            // Show keyboard shortcuts help
            if (keyData == (Keys.Control | Keys.OemQuestion)) // Ctrl+?
            {
                ShowKeyboardShortcutsHelp();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error processing keyboard shortcut");
        }

        return false;
    }

    /// <summary>
    /// Shows the Ctrl+Tab document switcher UI.
    /// </summary>
    private void ActivateNextDocumentWithSwitcher()
    {
        if (!_switcherActive)
        {
            ShowDocumentSwitcher();
        }

        if (_documentSwitcherList != null && _documentSwitcherList.Items.Count > 0)
        {
            var nextIndex = (_documentSwitcherList.SelectedIndex + 1) % _documentSwitcherList.Items.Count;
            _documentSwitcherList.SelectedIndex = nextIndex;
        }
    }

    /// <summary>
    /// Navigates backwards in the Ctrl+Tab document switcher.
    /// </summary>
    private void ActivatePreviousDocumentWithSwitcher()
    {
        if (!_switcherActive)
        {
            ShowDocumentSwitcher();
        }

        if (_documentSwitcherList != null && _documentSwitcherList.Items.Count > 0)
        {
            var prevIndex = _documentSwitcherList.SelectedIndex <= 0 
                ? _documentSwitcherList.Items.Count - 1 
                : _documentSwitcherList.SelectedIndex - 1;
            _documentSwitcherList.SelectedIndex = prevIndex;
        }
    }

    /// <summary>
    /// Shows the document switcher dialog (Ctrl+Tab UI).
    /// </summary>
    private void ShowDocumentSwitcher()
    {
        if (_switcherActive || this.MdiChildren == null || this.MdiChildren.Length == 0)
        {
            return;
        }

        try
        {
            _switcherActive = true;

            // Create switcher dialog
            _documentSwitcherDialog = new Form
            {
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                Size = new Size(400, 300),
                BackColor = Color.White,
                Opacity = 0.95
            };

            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(_documentSwitcherDialog, currentTheme);

            // Title label
            var titleLabel = new Label
            {
                Text = "Open Documents (Use ↑↓ or Ctrl+Tab, release Ctrl to switch)",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            _documentSwitcherDialog.Controls.Add(titleLabel);

            // Document list
            _documentSwitcherList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12F),
                ItemHeight = 30
            };

            foreach (var child in this.MdiChildren)
            {
                _documentSwitcherList.Items.Add(child.Text);
            }

            if (_documentSwitcherList.Items.Count > 0)
            {
                _documentSwitcherList.SelectedIndex = 0;
            }

            _documentSwitcherDialog.Controls.Add(_documentSwitcherList);

            // Handle key release to activate selected document
            _documentSwitcherDialog.KeyUp += (s, e) =>
            {
                if (e.KeyCode == Keys.ControlKey && _switcherActive)
                {
                    ActivateSelectedDocumentAndCloseSwitcher();
                }
            };

            _documentSwitcherDialog.LostFocus += (s, e) => CloseDocumentSwitcher();

            _documentSwitcherDialog.Show(this);
            _documentSwitcherList.Focus();

            _logger?.LogDebug("Document switcher shown with {Count} documents", _documentSwitcherList.Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing document switcher");
            _switcherActive = false;
        }
    }

    /// <summary>
    /// Activates the selected document and closes the switcher.
    /// </summary>
    private void ActivateSelectedDocumentAndCloseSwitcher()
    {
        try
        {
            if (_documentSwitcherList != null && _documentSwitcherList.SelectedIndex >= 0)
            {
                var children = this.MdiChildren;
                if (children != null && _documentSwitcherList.SelectedIndex < children.Length)
                {
                    children[_documentSwitcherList.SelectedIndex].Activate();
                }
            }
        }
        finally
        {
            CloseDocumentSwitcher();
        }
    }

    /// <summary>
    /// Closes the document switcher dialog.
    /// </summary>
    private void CloseDocumentSwitcher()
    {
        if (_documentSwitcherDialog != null)
        {
            _documentSwitcherDialog.Close();
            _documentSwitcherDialog.Dispose();
            _documentSwitcherDialog = null;
        }

        _documentSwitcherList = null;
        _switcherActive = false;
    }

    /// <summary>
    /// Activates a document by index (Alt+1 through Alt+9).
    /// </summary>
    private void ActivateDocumentByIndex(int index)
    {
        var children = this.MdiChildren;
        if (children != null && index < children.Length)
        {
            children[index].Activate();
            _logger?.LogDebug("Activated document {Index}: {Name}", index + 1, children[index].Text);
        }
    }

    /// <summary>
    /// Toggles full screen mode (F11).
    /// </summary>
    private void ToggleFullScreen()
    {
        if (this.FormBorderStyle == FormBorderStyle.None)
        {
            // Exit full screen
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Normal;
            this.TopMost = false;
            _logger?.LogDebug("Exited full screen mode");
        }
        else
        {
            // Enter full screen
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            _logger?.LogDebug("Entered full screen mode");
        }
    }

    /// <summary>
    /// Focuses the global search box.
    /// </summary>
    private void FocusGlobalSearch()
    {
        ShowGlobalSearchDialog();
        _logger?.LogDebug("Opened global search dialog (Ctrl+K)");
    }

    /// <summary>
    /// Shows keyboard shortcuts help dialog.
    /// </summary>
    private void ShowKeyboardShortcutsHelp()
    {
        var help = @"Keyboard Shortcuts

Document Navigation:
  Ctrl+Tab          → Next document
  Ctrl+Shift+Tab    → Previous document
  Alt+1-9           → Jump to document 1-9
  Ctrl+W            → Close current document
  Ctrl+F4           → Close current document (alternate)
  Ctrl+Shift+W      → Close all documents

View:
  F11               → Toggle full screen
  Ctrl+K            → Global search

Help:
  Ctrl+?            → Show this help";

        MessageBoxAdv.Show(this, help, "Keyboard Shortcuts", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);

        _logger?.LogDebug("Displayed keyboard shortcuts help");
    }
}
