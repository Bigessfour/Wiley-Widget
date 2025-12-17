using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Base class for MDI-aware child forms.
/// Provides automatic menu merging and MDI child form behaviors.
///
/// Usage:
/// 1. Inherit from MdiChildFormBase instead of Form
/// 2. Add MenuStrip with MergeAction configured
/// 3. Call base.OnLoad(e) to enable automatic menu merging
///
/// Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/multiple-document-interface-mdi-applications
/// </summary>
public abstract class MdiChildFormBase : Form
{
    private ILogger? _logger;
    private MenuStrip? _childMenuStrip;

    /// <summary>
    /// Gets whether this form is currently an MDI child.
    /// </summary>
    [Browsable(false)]
    protected bool IsInMdiMode => IsMdiChild;

    /// <summary>
    /// Gets the MDI parent as MainForm if available.
    /// </summary>
    [Browsable(false)]
    protected MainForm? MdiMainForm => MdiParent as MainForm;

    /// <summary>
    /// Constructor for MDI child form base.
    /// </summary>
    protected MdiChildFormBase()
    {
        // Default settings for MDI child forms
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = true;
        MaximizeBox = true;
    }

    /// <summary>
    /// Set the logger for this form (optional, but recommended).
    /// </summary>
    protected void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure menu merging for this MDI child form.
    /// Call this after creating your MenuStrip.
    /// </summary>
    protected void ConfigureMenuMerging(MenuStrip childMenuStrip)
    {
        if (childMenuStrip == null)
        {
            _logger?.LogWarning("Cannot configure menu merging: MenuStrip is null");
            return;
        }

        _childMenuStrip = childMenuStrip;
        _childMenuStrip.AllowMerge = true;

        // In MDI mode, hide the child's menu strip (it will merge with parent)
        // In modal mode, show it
        UpdateMenuVisibility();

        _logger?.LogDebug("Menu merging configured for {FormType}", GetType().Name);
    }

    /// <summary>
    /// Update menu strip visibility based on MDI mode.
    /// </summary>
    private void UpdateMenuVisibility()
    {
        if (_childMenuStrip == null) return;

        // Hide child menu when in MDI mode (it merges with parent)
        // Show it when used as modal dialog
        _childMenuStrip.Visible = !IsInMdiMode;
    }

    /// <summary>
    /// Called when the form loads. Override to add initialization logic.
    /// IMPORTANT: Always call base.OnLoad(e) when overriding.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Update menu visibility based on MDI status
        UpdateMenuVisibility();

        // If we're an MDI child and have a menu, configure merging with parent
        if (IsInMdiMode && _childMenuStrip != null && MdiMainForm != null)
        {
            try
            {
                MdiMainForm.ConfigureChildMenuMerging(_childMenuStrip);
                _logger?.LogDebug("MDI menu merging enabled for {FormType}", GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to configure MDI menu merging for {FormType}", GetType().Name);
            }
        }

        // Apply theme if MDI parent has one
        if (IsInMdiMode)
        {
            ApplyParentTheme();
        }
    }

    /// <summary>
    /// Apply the MDI parent's theme to this child form.
    /// </summary>
    private void ApplyParentTheme()
    {
        // Note: Theme inheritance now handled by SfSkinManager cascade from MainForm
        // ThemeColors.ApplyTheme(this) is called by each form's constructor
        _logger?.LogDebug("Theme inheritance managed by SfSkinManager for {FormType}", GetType().Name);
    }

    /// <summary>
    /// Called when MDI parent changes. Override to respond to MDI mode changes.
    /// </summary>
    protected override void OnMdiChildActivate(EventArgs e)
    {
        base.OnMdiChildActivate(e);
        UpdateMenuVisibility();
    }

    /// <summary>
    /// Helper to create a properly configured MenuStrip for MDI child forms.
    /// </summary>
    protected MenuStrip CreateMdiChildMenuStrip()
    {
        var menuStrip = new MenuStrip
        {
            AllowMerge = true,
            Dock = DockStyle.Top
        };

        ConfigureMenuMerging(menuStrip);
        return menuStrip;
    }

    /// <summary>
    /// Helper to create a menu item with proper merge settings.
    /// </summary>
    /// <param name="text">Menu item text</param>
    /// <param name="mergeAction">How this item merges with parent menu (default: Append)</param>
    /// <param name="mergeIndex">Position where item merges (-1 for end)</param>
    protected static ToolStripMenuItem CreateMergeMenuItem(
        string text,
        MergeAction mergeAction = MergeAction.Append,
        int mergeIndex = -1)
    {
        return new ToolStripMenuItem
        {
            Text = text,
            MergeAction = mergeAction,
            MergeIndex = mergeIndex
        };
    }

    /// <summary>
    /// Set the MDI child form's icon (shows in Window menu list).
    /// </summary>
    protected void SetMdiChildIcon(Icon icon)
    {
        Icon = icon;
        ShowIcon = true;
    }
}

/// <summary>
/// Helper class for MDI-related extension methods.
/// </summary>
public static class MdiExtensions
{
    /// <summary>
    /// Get all MDI child forms of a specific type from a parent form.
    /// </summary>
    public static T[] GetMdiChildrenOfType<T>(this Form parentForm) where T : Form
    {
        if (parentForm == null || !parentForm.IsMdiContainer)
            return Array.Empty<T>();

        return parentForm.MdiChildren.OfType<T>().ToArray();
    }

    /// <summary>
    /// Check if an MDI child form of specific type is already open.
    /// </summary>
    public static bool HasMdiChildOfType<T>(this Form parentForm) where T : Form
    {
        return parentForm.GetMdiChildrenOfType<T>().Any();
    }

    /// <summary>
    /// Get the active MDI child form of a specific type (if it's the active one).
    /// </summary>
    public static T? GetActiveMdiChildOfType<T>(this Form parentForm) where T : Form
    {
        if (parentForm == null || !parentForm.IsMdiContainer)
            return null;

        return parentForm.ActiveMdiChild as T;
    }

    /// <summary>
    /// Activate (bring to front) the first MDI child of specific type.
    /// </summary>
    public static bool ActivateMdiChildOfType<T>(this Form parentForm) where T : Form
    {
        var child = parentForm.GetMdiChildrenOfType<T>().FirstOrDefault();
        if (child != null && !child.IsDisposed)
        {
            child.Activate();
            return true;
        }
        return false;
    }
}
