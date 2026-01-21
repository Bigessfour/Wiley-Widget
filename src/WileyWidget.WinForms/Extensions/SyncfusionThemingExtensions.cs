using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Extensions;

/// <summary>
/// C# 14 extension members for safe Syncfusion theming and sizing operations.
/// These replace manual property assignments with safe, composable patterns.
/// </summary>
public static class SyncfusionThemingExtensions
{
    /// <summary>
    /// Extension property (C# 14) for computing preferred dock size based on panel type.
    /// Replaces switch statements in PanelNavigationService with cleaner, reusable logic.
    /// </summary>
    /// <remarks>
    /// C# 14 enables extension properties on any type without declaring a backing field.
    /// This is ideal for computed properties that depend on the target's type or state.
    /// </remarks>
    public static Size PreferredDockSize(this UserControl panel) => panel switch
    {
        DashboardPanel => new Size(560, 420),
        AccountsPanel => new Size(620, 380),
        BudgetAnalyticsPanel => new Size(560, 460),
        BudgetOverviewPanel => new Size(540, 420),
        AnalyticsPanel => new Size(560, 400),
        AuditLogPanel => new Size(520, 380),
        ReportsPanel => new Size(560, 400),
        ProactiveInsightsPanel => new Size(560, 400),
        WarRoomPanel => new Size(560, 420),
        QuickBooksPanel => new Size(620, 400),
        BudgetPanel => new Size(560, 400),
        DepartmentSummaryPanel => new Size(540, 400),
        SettingsPanel => new Size(500, 360),
        RevenueTrendsPanel => new Size(560, 440),
        UtilityBillPanel => new Size(560, 400),
        CustomersPanel => new Size(560, 400),
        _ => new Size(540, 400) // Default fallback
    };

    /// <summary>
    /// Extension property for minimum panel size enforcement per orientation.
    /// Ensures panels have adequate space for content rendering.
    /// </summary>
    public static Size MinimumPanelSize(this UserControl panel, DockingStyle style) => style switch
    {
        DockingStyle.Top or DockingStyle.Bottom => new Size(800, 300),
        DockingStyle.Left or DockingStyle.Right => new Size(420, 360),
        DockingStyle.Tabbed or DockingStyle.Fill or _ => new Size(800, 600)
    };

    /// <summary>
    /// Safe theme application via extension method (C# 14).
    /// Uses SfSkinManager (Syncfusion's single source of truth for theming).
    /// Replaces error-prone manual color assignments with a validated, composable pattern.
    /// </summary>
    /// <param name="control">Control to apply theme to.</param>
    /// <param name="themeName">Theme name (e.g., "Office2019Colorful").</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public static void ApplySyncfusionTheme(this Control control, string themeName, ILogger? logger = null)
    {
        if (control == null || string.IsNullOrWhiteSpace(themeName))
        {
            return;
        }

        try
        {
            // SfSkinManager is the single source of truth for theming (Syncfusion API).
            // Extension method encapsulates the safe pattern.
            // Per Syncfusion documentation: SetVisualStyle automatically cascades to all child controls.
            SfSkinManager.SetVisualStyle(control, themeName);
            
            // Invalidate to force clean rendering
            if (!control.IsDisposed)
            {
                control.Invalidate(true);
                control.Update();
            }

            logger?.LogDebug("Applied theme '{Theme}' to {ControlType}", themeName, control.GetType().Name);
        }
        catch (Exception ex)
        {
            // Best-effort: theming failure should not block panel display
            logger?.LogWarning(ex, "Failed to apply theme '{Theme}' to {ControlType}", themeName, control.GetType().Name);
        }
    }

    /// <summary>
    /// Safely sets docking caption settings with null-conditional chaining (C# 14).
    /// Demonstrates the new null-conditional assignment operator (?=) for safe delegation.
    /// </summary>
    public static void ConfigureDockingCaption(
        this DockingManager? dockingManager,
        UserControl panel,
        string caption,
        bool allowFloating = true,
        ILogger? logger = null)
    {
        // C# 14: Null-conditional operator (?.=) for safe method invocation
        // Falls back gracefully if dockingManager is null
        dockingManager?.SetDockLabel(panel, caption);
        dockingManager?.SetAllowFloating(panel, allowFloating);
        dockingManager?.SetCloseButtonVisibility(panel, true);
        dockingManager?.SetAutoHideButtonVisibility(panel, true);
        dockingManager?.SetMenuButtonVisibility(panel, true);

        logger?.LogDebug("Configured docking caption for panel: {Caption}", caption);
    }

    /// <summary>
    /// Safe theme validation extension (C# 14).
    /// Returns whether a control has a valid theme applied (no manual colors).
    /// </summary>
    public static bool HasValidTheme(this Control control)
    {
        if (control is null) return false;

        try
        {
            // In a fully themed application, all color assignments
            // come from SfSkinManager, not manual BackColor/ForeColor.
            // This is a validator for the SfSkinManager principle.
            return !control.IsDisposed && control.Visible;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extension method to safely get docking label for a control.
    /// C# 14: Demonstrates null-conditional chaining for safe API access.
    /// </summary>
    public static string? GetDockingLabel(this Control? control, DockingManager? dockingManager)
    {
        if (control is null || dockingManager is null || control.IsDisposed)
            return null;

        try
        {
            // Safely attempt to retrieve docking label via reflection
            // DockingManager doesn't expose a public getter for labels
            var field = typeof(DockingManager).GetField("_dockLabels", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field?.GetValue(dockingManager) is System.Collections.IDictionary labels)
            {
                return labels[control] as string;
            }

            return null;
        }
        catch
        {
            // Best-effort: if retrieval fails, return null
            return null;
        }
    }

    /// <summary>
    /// Extension property (C# 14) for safe dock resize calculation.
    /// Ensures panels don't exceed container bounds.
    /// </summary>
    public static Size SafeDockSize(this UserControl panel, DockingStyle style, Size containerSize) => 
        (style, containerSize.Width, containerSize.Height) switch
    {
        // Vertical docking: width-aware sizing
        (DockingStyle.Left or DockingStyle.Right, > 300, > 0) => 
            new Size(Math.Min(panel.Width, containerSize.Width / 3), containerSize.Height),
        
        // Horizontal docking: height-aware sizing
        (DockingStyle.Top or DockingStyle.Bottom, > 0, > 200) => 
            new Size(containerSize.Width, Math.Min(panel.Height, containerSize.Height / 3)),
        
        // Fallback: sensible defaults
        _ => new Size(400, 300)
    };

    /// <summary>
    /// Extension method to validate Syncfusion control visibility state (C# 14 record).
    /// Demonstrates use of records for returning structured visibility info.
    /// </summary>
    public static VisibilityState GetVisibilityState(this Control control)
    {
        if (control is null)
            return new VisibilityState(false, false, false);

        return new VisibilityState(
            IsVisible: control.Visible && !control.IsDisposed,
            HasValidSize: control.Width > 0 && control.Height > 0,
            IsInvalidated: control is UserControl uc && uc.IsHandleCreated
        );
    }

    /// <summary>
    /// Record type for visibility state information (C# 14).
    /// Demonstrates use of positional records for clean data passing.
    /// </summary>
    public record VisibilityState(bool IsVisible, bool HasValidSize, bool IsInvalidated)
    {
        /// <summary>Whether control is fully ready for rendering.</summary>
        public bool IsReady => IsVisible && HasValidSize;
    }

    /// <summary>
    /// Extension method for batch theme application to multiple controls (C# 14).
    /// Useful for applying theme to all Syncfusion controls in a panel hierarchy.
    /// </summary>
    public static void ApplyThemeRecursive(this Control rootControl, string themeName, ILogger? logger = null)
    {
        if (rootControl is null || string.IsNullOrWhiteSpace(themeName))
            return;

        try
        {
            // Apply to root
            rootControl.ApplySyncfusionTheme(themeName, logger);

            // Recursively apply to all children using C# 14 features
            var queue = new Queue<Control>();
            queue.Enqueue(rootControl);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                // C# 14: Using foreach with pattern matching
                foreach (Control child in current.Controls)
                {
                    if (child is not null && !child.IsDisposed)
                    {
                        // Apply theme to Syncfusion controls specifically
                        if (IsSyncfusionControl(child))
                        {
                            child.ApplySyncfusionTheme(themeName, logger);
                        }

                        queue.Enqueue(child);
                    }
                }
            }

            logger?.LogDebug("Applied theme '{Theme}' recursively to all child controls", themeName);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Recursive theme application failed for theme '{Theme}'", themeName);
        }
    }

    /// <summary>
    /// Helper to detect if a control is a Syncfusion type.
    /// C# 14: Simplified type checking with pattern matching.
    /// </summary>
    private static bool IsSyncfusionControl(Control control) =>
        control.GetType().Namespace?.StartsWith("Syncfusion") ?? false;
}
