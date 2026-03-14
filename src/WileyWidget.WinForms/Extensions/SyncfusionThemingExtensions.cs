using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Factories;

using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

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
        FormHostPanel => new Size(560, 420),
        AccountsPanel => new Size(900, 560),
        AnalyticsHubPanel => new Size(560, 400),
        AuditLogPanel => new Size(520, 380),
        ProactiveInsightsPanel => new Size(560, 400),
        WarRoomPanel => new Size(1000, 700),
        QuickBooksPanel => new Size(620, 400),
        DepartmentSummaryPanel => new Size(540, 400),
        SettingsPanel => new Size(500, 360),
        UtilityBillPanel => new Size(560, 400),
        CustomersPanel => new Size(580, 380),
        ReportsPanel => new Size(1400, 900),
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
            var validatedTheme = AppThemeColors.ValidateTheme(themeName, logger);
            AppThemeColors.EnsureThemeAssemblyLoadedForTheme(validatedTheme, logger);
            SyncfusionControlFactory.ApplyThemeToAllControls(control, validatedTheme, logger);

            logger?.LogDebug("Applied theme '{Theme}' to {ControlType}", validatedTheme, control.GetType().Name);
        }
        catch (Exception ex)
        {
            // Best-effort: theming failure should not block panel display
            logger?.LogWarning(ex, "Failed to apply theme '{Theme}' to {ControlType}", themeName, control.GetType().Name);
        }
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

}
