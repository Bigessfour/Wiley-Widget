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
        if (_tabbedMdi != null)
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
            _tabbedMdi = new TabbedMDIManager
            {
                AttachedTo = this
            };

            // Configure appearance
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            _tabbedMdi.ThemeName = currentTheme;
            _tabbedMdi.ThemesEnabled = true;
            // Derive TabStyle from the active theme so the tab renderer matches Office2019 etc.
            // Office2016Colorful was hardcoded previously and caused a visual mismatch.
            _tabbedMdi.TabStyle = ResolveTabStyle(currentTheme);
            _tabbedMdi.ShowCloseButton = true;
            _tabbedMdi.CloseButtonVisible = true;

            // Tab behavior
            _tabbedMdi.AllowTabGroupCustomizing = true;
            _tabbedMdi.TabControlAdded += OnTabControlAdded;
            _tabbedMdi.TabControlRemoved += OnTabControlRemoved;

            // Apply theme
            SfSkinManager.SetVisualStyle(_tabbedMdi, currentTheme);

            _logger?.LogInformation("TabbedMDI Manager initialized successfully with theme {Theme}", currentTheme);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize TabbedMDI Manager");
            _tabbedMdi = null;
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
            // Get tab name from associated MDI child form or fallback to TabControl properties
            var tabName = GetTabName(e.TabControl);
            _logger?.LogDebug("Tab added: {TabText}", tabName);

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
            // Get tab name from associated MDI child form or fallback to TabControl properties
            var tabName = GetTabName(e.TabControl);
            _logger?.LogDebug("Tab removed: {TabText}", tabName);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error handling tab control removed");
        }
    }

    /// <summary>
    /// Gets the tab name from a TabControl, checking multiple sources.
    /// </summary>
    private string GetTabName(Control tabControl)
    {
        // Try TabControl.Text first
        if (!string.IsNullOrWhiteSpace(tabControl.Text))
        {
            return tabControl.Text;
        }

        // Try to find associated MDI child form by searching parent hierarchy
        var current = tabControl.Parent;
        while (current != null)
        {
            if (current is Form form && form.MdiParent == this)
            {
                return form.Text;
            }
            current = current.Parent;
        }

        // Fallback to control name or default
        return !string.IsNullOrWhiteSpace(tabControl.Name)
            ? tabControl.Name
            : "(unnamed)";
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
        var children = _tabbedMdi?.MdiChildren ?? this.MdiChildren;
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

        var children = _tabbedMdi?.MdiChildren ?? this.MdiChildren;
        foreach (var child in children.Where(c => c != activeChild).ToArray())
        {
            child.Close();
        }
    }

    /// <summary>
    /// Gets the count of open MDI documents.
    /// </summary>
    private int GetOpenDocumentCount() => _tabbedMdi?.MdiChildren?.Length ?? this.MdiChildren?.Length ?? 0;

    /// <summary>
    /// Gets all open document names.
    /// </summary>
    private IEnumerable<string> GetOpenDocumentNames()
    {
        return _tabbedMdi?.MdiChildren?.Select(c => c.Text) ?? this.MdiChildren?.Select(c => c.Text) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Activates the next MDI document (Ctrl+Tab navigation).
    /// </summary>
    private void ActivateNextDocument()
    {
        var children = _tabbedMdi?.MdiChildren ?? this.MdiChildren;
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
        var children = _tabbedMdi?.MdiChildren ?? this.MdiChildren;
        if (children == null || children.Length == 0) return;

        var activeIndex = Array.IndexOf(children, this.ActiveMdiChild);
        var prevIndex = activeIndex <= 0 ? children.Length - 1 : activeIndex - 1;
        children[prevIndex].Activate();
    }

    /// <summary>
    /// Maps the active SfSkinManager theme name to the closest available Syncfusion
    /// <see cref="TabbedMDIManager.TabStyle"/> renderer for visual consistency.
    /// The Syncfusion Tools assembly ships <see cref="TabRendererOffice2016Colorful"/> and
    /// <see cref="TabRendererMetro"/> in the version used here; Office2019/Dark themes use
    /// the Office2016 renderer which is the closest visual match.
    /// </summary>
    private static Type ResolveTabStyle(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return typeof(TabRendererOffice2016Colorful);

        // Normalise: "Office2019Dark" → "office2019dark"
        var key = themeName.Replace(" ", string.Empty, StringComparison.Ordinal)
                           .ToLowerInvariant();

        // Metro / HighContrast variants get the Metro renderer; everything else gets Office2016.
        return key switch
        {
            var t when t.StartsWith("metro", StringComparison.Ordinal) => typeof(TabRendererMetro),
            var t when t.StartsWith("highcontrast", StringComparison.Ordinal) => typeof(TabRendererMetro),
            _ => typeof(TabRendererOffice2016Colorful)
        };
    }
}
