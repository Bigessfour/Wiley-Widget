using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// MainForm document management using Syncfusion TabbedMDIManager.
/// Provides Office-style tabbed document interface for panels.
/// 
/// SYNCFUSION API: TabbedMDIManager
/// Reference: https://help.syncfusion.com/windowsforms/tabbedmdi/overview
/// </summary>
public partial class MainForm
{
    private TabbedMDIManager? _mdiManager;
    private readonly Dictionary<string, Form> _openDocuments = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialize TabbedMDIManager for professional tabbed document interface.
    /// Replaces floating forms with Office-style tabs.
    /// 
    /// SYNCFUSION API PROPERTIES:
    /// - AttachedTo: Parent form
    /// - TabStyle: Visual style (Office2016Colorful, Metro, etc.)
    /// - CloseButtonEnabled: Show close button on tabs
    /// - ThemeName: Theme integration
    /// - AllowDragDrop: Enable tab reordering
    /// </summary>
    private void InitializeMDIManager()
    {
        if (_mdiManager != null)
        {
            _logger?.LogDebug("MDI Manager already initialized");
            return;
        }

        try
        {
            _logger?.LogInformation("Initializing TabbedMDI Manager for document tabs");

            // Set as MDI container
            this.IsMdiContainer = true;
            this.MdiChildrenMinimizedAnchorBottom = false;

            // Create TabbedMDI manager
            _mdiManager = new TabbedMDIManager
            {
                AttachedTo = this
            };

            // Configure appearance
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            _mdiManager.ThemeName = currentTheme;
            _mdiManager.ThemesEnabled = true;
            _mdiManager.TabStyle = typeof(TabRendererOffice2016Colorful);
            _mdiManager.ShowCloseButton = true;
            _mdiManager.CloseButtonVisible = true;

            // Tab behavior
            _mdiManager.AllowTabGroupCustomizing = true;
            _mdiManager.TabControlAdded += OnTabControlAdded;
            _mdiManager.TabControlRemoved += OnTabControlRemoved;

            // Apply theme
            SfSkinManager.SetVisualStyle(_mdiManager, currentTheme);

            _logger?.LogInformation("TabbedMDI Manager initialized successfully with theme {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize TabbedMDI Manager");
            _mdiManager = null;
        }
    }

    /// <summary>
    /// Opens a panel in a new MDI child tab.
    /// </summary>
    private Form CreateMDIDocument(UserControl panel, string documentName)
    {
        // Check if document already open
        if (_openDocuments.TryGetValue(documentName, out var existing) && !existing.IsDisposed)
        {
            existing.Activate();
            return existing;
        }

        var mdiChild = new Form
        {
            MdiParent = this,
            Text = documentName,
            FormBorderStyle = FormBorderStyle.Sizable,
            ShowIcon = false,
            WindowState = FormWindowState.Maximized,
            AutoScaleMode = AutoScaleMode.Dpi,
            MinimumSize = Size.Empty
        };

        // Add panel to MDI child
        panel.Dock = DockStyle.Fill;
        mdiChild.Controls.Add(panel);

        // Apply theme to MDI child
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(mdiChild, currentTheme);

        // Track open document
        _openDocuments[documentName] = mdiChild;

        // Clean up when closed
        mdiChild.FormClosed += (s, e) =>
        {
            _openDocuments.Remove(documentName);
            _logger?.LogDebug("MDI document closed: {DocumentName}", documentName);
        };

        // Force handle creation to trigger panel initialization
        if (!panel.IsHandleCreated)
        {
            _ = panel.Handle;
        }

        mdiChild.Show();
        mdiChild.Activate();

        _logger?.LogInformation("Opened MDI document: {DocumentName}", documentName);
        return mdiChild;
    }

    /// <summary>
    /// Called when a tab is added to TabbedMDI.
    /// </summary>
    private void OnTabControlAdded(object? sender, TabbedMDITabControlEventArgs e)
    {
        try
        {
            _logger?.LogDebug("Tab added: {TabText}", e.TabControl.Text);

            // Apply theme to new tab
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            e.TabControl.ThemeName = currentTheme;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error handling tab control added");
        }
    }

    /// <summary>
    /// Called when a tab is removed from TabbedMDI.
    /// </summary>
    private void OnTabControlRemoved(object? sender, TabbedMDITabControlEventArgs e)
    {
        try
        {
            _logger?.LogDebug("Tab removed: {TabText}", e.TabControl.Text);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error handling tab control removed");
        }
    }

    /// <summary>
    /// Closes the active MDI document.
    /// </summary>
    private void CloseActiveDocument()
    {
        var activeChild = this.ActiveMdiChild;
        if (activeChild != null)
        {
            activeChild.Close();
        }
    }

    /// <summary>
    /// Closes all MDI documents.
    /// </summary>
    private void CloseAllDocuments()
    {
        var children = _mdiManager?.MdiChildren ?? this.MdiChildren;
        foreach (var child in children.ToArray())
        {
            child.Close();
        }
    }

    /// <summary>
    /// Closes all MDI documents except the active one.
    /// </summary>
    private void CloseOtherDocuments()
    {
        var activeChild = this.ActiveMdiChild;
        if (activeChild == null) return;

        var children = _mdiManager?.MdiChildren ?? this.MdiChildren;
        foreach (var child in children.Where(c => c != activeChild).ToArray())
        {
            child.Close();
        }
    }

    /// <summary>
    /// Gets the count of open MDI documents.
    /// </summary>
    private int GetOpenDocumentCount() => _mdiManager?.MdiChildren?.Length ?? this.MdiChildren?.Length ?? 0;

    /// <summary>
    /// Gets all open document names.
    /// </summary>
    private IEnumerable<string> GetOpenDocumentNames()
    {
        return _mdiManager?.MdiChildren?.Select(c => c.Text) ?? this.MdiChildren?.Select(c => c.Text) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Activates the next MDI document (Ctrl+Tab navigation).
    /// </summary>
    private void ActivateNextDocument()
    {
        var children = _mdiManager?.MdiChildren ?? this.MdiChildren;
        if (children == null || children.Length == 0) return;

        var activeIndex = Array.IndexOf(children, this.ActiveMdiChild);
        var nextIndex = (activeIndex + 1) % children.Length;
        children[nextIndex].Activate();
    }

    /// <summary>
    /// Activates the previous MDI document (Ctrl+Shift+Tab navigation).
    /// </summary>
    private void ActivatePreviousDocument()
    {
        var children = _mdiManager?.MdiChildren ?? this.MdiChildren;
        if (children == null || children.Length == 0) return;

        var activeIndex = Array.IndexOf(children, this.ActiveMdiChild);
        var prevIndex = activeIndex <= 0 ? children.Length - 1 : activeIndex - 1;
        children[prevIndex].Activate();
    }
}
