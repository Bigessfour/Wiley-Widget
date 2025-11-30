using System.Drawing;
using System.Text.Json;
using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms.Theming;

/// <summary>
/// Centralized theme management for the application.
/// Uses Syncfusion SkinManager for Syncfusion controls and provides
/// consistent styling for standard WinForms controls.
///
/// Syncfusion WinForms Theme Implementation:
/// - SkinManager.ApplicationVisualTheme: App-wide theme (set BEFORE form init)
/// - SkinManager component per form: skinManager.Controls = this; skinManager.VisualTheme = ...
/// - Individual control: control.ThemeName = "Office2016Black"
///
/// Available themes: Office2016Black (dark), Office2016Colorful (light),
/// Office2019Colorful, HighContrastBlack
/// </summary>
public static class ThemeManager
{
    // Theme persistence is now handled by IThemeService/ThemeService.

    /// <summary>
    /// Current application theme
    /// </summary>
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>
    /// Event fired when theme changes
    /// </summary>
    public static event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>
    /// Syncfusion theme name for dark mode (prefer Fluent dark skin when available)
    /// </summary>
    public const string SyncfusionDarkTheme = "FluentDark";

    /// <summary>
    /// Syncfusion theme name for light mode (prefer Fluent light skin when available)
    /// </summary>
    public const string SyncfusionLightTheme = "FluentLight";

    /// <summary>
    /// Initialize theme system for forms. Theme selection and persistence is managed
    /// by an external ThemeService; this method ensures visual theme application logic
    /// is ready for use by callers.
    /// </summary>
    public static void Initialize() => ApplySyncfusionApplicationTheme();

    /// <summary>
    /// Apply the Syncfusion theme at application level.
    /// This must be called before forms are initialized for best results.
    /// </summary>
    private static void ApplySyncfusionApplicationTheme()
    {
        try
        {
            // Set application-wide Syncfusion theme
            // This applies to all Syncfusion controls created after this call
            SkinManager.ApplicationVisualTheme = GetSyncfusionThemeName();
            Serilog.Log.Information("Applied Syncfusion application theme: {Theme}", SkinManager.ApplicationVisualTheme);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to apply Syncfusion application theme");
        }
    }

    /// <summary>
    /// Get the Syncfusion theme name for the current theme
    /// </summary>
    public static string GetSyncfusionThemeName() => CurrentTheme switch
    {
        AppTheme.Dark => SyncfusionDarkTheme,
        AppTheme.Light => SyncfusionLightTheme,
        _ => SyncfusionLightTheme
    };

    /// <summary>
    /// Get the Syncfusion VisualTheme enum for the current theme
    /// </summary>
    public static VisualTheme GetSyncfusionVisualTheme() => CurrentTheme switch
    {
        AppTheme.Dark => VisualTheme.Office2016Black,
        AppTheme.Light => VisualTheme.Office2016Colorful,
        _ => VisualTheme.Office2016Colorful
    };

    /// <summary>
    /// Get colors for the current theme
    /// </summary>
    public static ThemeColors Colors => CurrentTheme == AppTheme.Dark ? DarkColors : LightColors;

    /// <summary>
    /// Set the application theme (effective theme) and notify listeners.
    /// Persistence is intentionally handled by ThemeService to centralize storage.
    /// Note: Changing theme at runtime may require restarting some Syncfusion controls
    /// to fully update — standard WinForms controls will update immediately.
    /// </summary>
    public static void SetTheme(AppTheme theme)
    {
        var previousTheme = CurrentTheme;
        CurrentTheme = theme;
        // Update Syncfusion application theme
        ApplySyncfusionApplicationTheme();

        ThemeChanged?.Invoke(null, theme);

        if (previousTheme != theme)
        {
            Serilog.Log.Information("Theme changed from {Old} to {New}", previousTheme, theme);
        }
    }

    /// <summary>
    /// Apply theme to a form using SkinManager and style standard controls.
    /// For Syncfusion controls, uses the SkinManager component.
    /// For standard WinForms controls, applies colors manually.
    /// </summary>
    public static void ApplyTheme(Form form)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));

        var colors = Colors;
        form.BackColor = colors.Background;
        form.ForeColor = colors.TextPrimary;

        // Apply Syncfusion theme to form using SkinManager
        ApplySyncfusionThemeToForm(form);

        // Apply theme to all controls (standard WinForms controls)
        ApplyThemeToControls(form.Controls, colors);
    }

    /// <summary>
    /// Apply Syncfusion theme to a form using SkinManager component.
    /// </summary>
    private static void ApplySyncfusionThemeToForm(Form form)
    {
        if (form == null) return;

        try
        {
            // Create a container for the SkinManager (required by Syncfusion)
            var components = new System.ComponentModel.Container();
            var skinManager = new SkinManager(components);
            skinManager.Controls = form;
            skinManager.VisualTheme = GetSyncfusionVisualTheme();

            // Ensure the created disposable instances are cleaned up when the form is disposed.
            // Some versions of SkinManager require the IContainer lifetime; we'll dispose both when the form is disposed.
            form.Disposed += (s, e) =>
            {
                try
                {
                    skinManager?.Dispose();
                    components?.Dispose();
                }
                catch { }
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to apply SkinManager theme to form {FormName}", form.Name);
        }
    }

    /// <summary>
    /// Apply theme to a control collection recursively
    /// </summary>
    public static void ApplyThemeToControls(Control.ControlCollection controls, ThemeColors? colors = null)
    {
        if (controls == null) throw new ArgumentNullException(nameof(controls));
        colors ??= Colors;

        foreach (Control control in controls)
        {
            ApplyThemeToControl(control, colors);

            if (control.HasChildren)
            {
                ApplyThemeToControls(control.Controls, colors);
            }
        }
    }

    /// <summary>
    /// Apply theme to a single control
    /// </summary>
    public static void ApplyThemeToControl(Control control, ThemeColors? colors = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        colors ??= Colors;

        switch (control)
        {
            case Button btn:
                StyleButton(btn, colors);
                break;

            case Panel panel:
                panel.BackColor = colors.Surface;
                panel.ForeColor = colors.TextPrimary;
                break;

            case Label label:
                label.ForeColor = colors.TextPrimary;
                break;

            case TextBox textBox:
                StyleTextBox(textBox, colors);
                break;

            case ComboBox comboBox:
                StyleComboBox(comboBox, colors);
                break;

            case NumericUpDown numeric:
                StyleNumericUpDown(numeric, colors);
                break;

            case CheckBox checkBox:
                checkBox.ForeColor = colors.TextPrimary;
                checkBox.BackColor = colors.Surface;
                break;

            case MenuStrip menuStrip:
                StyleMenuStrip(menuStrip, colors);
                break;

            case ToolStrip toolStrip:
                toolStrip.BackColor = colors.Surface;
                toolStrip.ForeColor = colors.TextPrimary;
                // Ensure ToolStripItems (labels, buttons) are styled as well — ToolStrip items are not Controls and
                // therefore not visited by ApplyThemeToControls, so set their ForeColor/BackColor explicitly.
                try
                {
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        try
                        {
                            item.ForeColor = colors.TextPrimary;
                            item.BackColor = colors.Surface;
                            if (item is ToolStripButton tsb)
                            {
                                tsb.ImageTransparentColor = Color.Magenta; // default safe transparent color
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                break;

            case GroupBox groupBox:
                groupBox.BackColor = colors.Surface;
                groupBox.ForeColor = colors.TextPrimary;
                break;

            case ListView listView:
                listView.BackColor = colors.InputBackground;
                listView.ForeColor = colors.TextPrimary;
                break;

            default:
                // For Syncfusion controls, try to set ThemeName property
                // This is a fallback for controls not caught by SkinManager
                TrySetSyncfusionTheme(control);

                // Only set BackColor/ForeColor for non-Syncfusion controls
                // Syncfusion controls manage their own colors via ThemeName
                if (!IsSyncfusionControl(control))
                {
                    control.BackColor = colors.Surface;
                    control.ForeColor = colors.TextPrimary;
                }
                break;
        }
    }

    /// <summary>
    /// Check if a control is a Syncfusion control (to avoid overriding theme colors)
    /// </summary>
    private static bool IsSyncfusionControl(Control control)
    {
        var typeName = control.GetType().FullName ?? "";
        return typeName.StartsWith("Syncfusion.", StringComparison.OrdinalIgnoreCase);
    }

    private static void StyleButton(Button btn, ThemeColors colors)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = colors.Border;
        btn.Cursor = Cursors.Hand;
        btn.Font = new Font("Segoe UI", 9, FontStyle.Regular);

        // Primary/accent button (Add)
        if (btn.Name.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
            btn.Name.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
            btn.Name.Contains("Primary", StringComparison.OrdinalIgnoreCase))
        {
            btn.BackColor = colors.Accent;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = colors.Accent;
            btn.FlatAppearance.MouseOverBackColor = colors.AccentHover;
        }
        // Danger button (Delete)
        else if (btn.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                 btn.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        {
            btn.BackColor = colors.Danger;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = colors.Danger;
            btn.FlatAppearance.MouseOverBackColor = colors.DangerHover;
        }
        // Standard button
        else
        {
            btn.BackColor = colors.ButtonBackground;
            btn.ForeColor = colors.TextPrimary;
            btn.FlatAppearance.MouseOverBackColor = colors.ButtonHover;
        }
    }

    private static void StyleTextBox(TextBox textBox, ThemeColors colors)
    {
        textBox.BackColor = colors.InputBackground;
        textBox.ForeColor = colors.TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleComboBox(ComboBox comboBox, ThemeColors colors)
    {
        comboBox.BackColor = colors.InputBackground;
        comboBox.ForeColor = colors.TextPrimary;
        comboBox.FlatStyle = FlatStyle.Flat;
    }

    private static void StyleNumericUpDown(NumericUpDown numeric, ThemeColors colors)
    {
        numeric.BackColor = colors.InputBackground;
        numeric.ForeColor = colors.TextPrimary;
        numeric.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleMenuStrip(MenuStrip menuStrip, ThemeColors colors)
    {
        menuStrip.BackColor = colors.Surface;
        menuStrip.ForeColor = colors.TextPrimary;
        menuStrip.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(colors));

        foreach (ToolStripItem item in menuStrip.Items)
        {
            item.ForeColor = colors.TextPrimary;
            item.BackColor = colors.Surface;

            if (item is ToolStripMenuItem menuItem)
            {
                StyleMenuItems(menuItem, colors);
            }
        }
    }

    private static void StyleMenuItems(ToolStripMenuItem menuItem, ThemeColors colors)
    {
        menuItem.ForeColor = colors.TextPrimary;
        menuItem.BackColor = colors.Surface;

        foreach (ToolStripItem subItem in menuItem.DropDownItems)
        {
            subItem.ForeColor = colors.TextPrimary;
            subItem.BackColor = colors.Surface;

            if (subItem is ToolStripMenuItem subMenuItem)
            {
                StyleMenuItems(subMenuItem, colors);
            }
        }
    }

    private static void TrySetSyncfusionTheme(Control control)
    {
        var themeNameProp = control.GetType().GetProperty("ThemeName");
        if (themeNameProp != null && themeNameProp.CanWrite)
        {
            try
            {
                themeNameProp.SetValue(control, GetSyncfusionThemeName());
            }
            catch
            {
                // Ignore - not all controls support all themes
            }
        }
    }

    // Persistence responsibilities are now provided by ThemeService.

    // Dark theme colors (Fluent Dark inspired)
    private static readonly ThemeColors DarkColors = new()
    {
        Background = Color.FromArgb(32, 32, 32),
        Surface = Color.FromArgb(43, 43, 43),
        SurfaceVariant = Color.FromArgb(55, 55, 55),
        TextPrimary = Color.FromArgb(255, 255, 255),
        TextSecondary = Color.FromArgb(180, 180, 180),
        TextDisabled = Color.FromArgb(120, 120, 120),
        TextOnAccent = Color.FromArgb(255, 255, 255),
        Accent = Color.FromArgb(0, 120, 212),
        AccentHover = Color.FromArgb(26, 140, 232),
        Danger = Color.FromArgb(196, 43, 28),
        DangerHover = Color.FromArgb(216, 63, 48),
        Success = Color.FromArgb(16, 124, 16),
        Warning = Color.FromArgb(255, 185, 0),
        Border = Color.FromArgb(70, 70, 70),
        BorderFocused = Color.FromArgb(0, 120, 212),
        InputBackground = Color.FromArgb(45, 45, 45),
        ButtonBackground = Color.FromArgb(60, 60, 60),
        ButtonHover = Color.FromArgb(75, 75, 75),
        GridHeader = Color.FromArgb(50, 50, 50),
        GridRow = Color.FromArgb(37, 37, 37),
        GridRowAlt = Color.FromArgb(44, 44, 44),
        GridRowSelected = Color.FromArgb(0, 90, 158)
    };

    // Light theme colors (Fluent Light inspired)
    private static readonly ThemeColors LightColors = new()
    {
        Background = Color.FromArgb(249, 249, 249),
        Surface = Color.FromArgb(255, 255, 255),
        SurfaceVariant = Color.FromArgb(243, 243, 243),
        TextPrimary = Color.FromArgb(32, 32, 32),
        TextSecondary = Color.FromArgb(96, 96, 96),
        TextDisabled = Color.FromArgb(160, 160, 160),
        TextOnAccent = Color.FromArgb(255, 255, 255),
        Accent = Color.FromArgb(0, 120, 212),
        AccentHover = Color.FromArgb(0, 100, 180),
        Danger = Color.FromArgb(196, 43, 28),
        DangerHover = Color.FromArgb(176, 23, 8),
        Success = Color.FromArgb(16, 124, 16),
        Warning = Color.FromArgb(255, 185, 0),
        Border = Color.FromArgb(225, 225, 225),
        BorderFocused = Color.FromArgb(0, 120, 212),
        InputBackground = Color.FromArgb(255, 255, 255),
        ButtonBackground = Color.FromArgb(251, 251, 251),
        ButtonHover = Color.FromArgb(240, 240, 240),
        GridHeader = Color.FromArgb(243, 243, 243),
        GridRow = Color.FromArgb(255, 255, 255),
        GridRowAlt = Color.FromArgb(250, 250, 250),
        GridRowSelected = Color.FromArgb(204, 232, 255)
    };

    // No longer store user settings here.
}

/// <summary>
/// Available application themes
/// </summary>
public enum AppTheme
{
    Dark,
    Light,
    /// <summary>
    /// Follow the operating system preference (Windows light/dark setting).
    /// ThemeService will resolve this to an effective Dark/Light value at runtime.
    /// </summary>
    System
}

/// <summary>
/// Theme color definitions
/// </summary>
public class ThemeColors
{
    public Color Background { get; init; }
    public Color Surface { get; init; }
    public Color SurfaceVariant { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextDisabled { get; init; }
    public Color TextOnAccent { get; init; }
    public Color Accent { get; init; }
    public Color AccentHover { get; init; }
    public Color Danger { get; init; }
    public Color DangerHover { get; init; }
    public Color Success { get; init; }
    public Color Warning { get; init; }
    public Color Border { get; init; }
    public Color BorderFocused { get; init; }
    public Color InputBackground { get; init; }
    public Color ButtonBackground { get; init; }
    public Color ButtonHover { get; init; }
    public Color GridHeader { get; init; }
    public Color GridRow { get; init; }
    public Color GridRowAlt { get; init; }
    public Color GridRowSelected { get; init; }
}

/// <summary>
/// Custom color table for ToolStrip/MenuStrip theming
/// </summary>
public class ThemeColorTable : ProfessionalColorTable
{
    private readonly ThemeColors _colors;

    public ThemeColorTable(ThemeColors colors)
    {
        _colors = colors;
        UseSystemColors = false;
    }

    public override Color MenuItemSelected => _colors.SurfaceVariant;
    public override Color MenuItemSelectedGradientBegin => _colors.SurfaceVariant;
    public override Color MenuItemSelectedGradientEnd => _colors.SurfaceVariant;
    public override Color MenuItemPressedGradientBegin => _colors.Accent;
    public override Color MenuItemPressedGradientEnd => _colors.Accent;
    public override Color MenuItemBorder => _colors.Border;
    public override Color MenuBorder => _colors.Border;
    public override Color MenuStripGradientBegin => _colors.Surface;
    public override Color MenuStripGradientEnd => _colors.Surface;
    public override Color ToolStripDropDownBackground => _colors.Surface;
    public override Color ImageMarginGradientBegin => _colors.Surface;
    public override Color ImageMarginGradientMiddle => _colors.Surface;
    public override Color ImageMarginGradientEnd => _colors.Surface;
    public override Color SeparatorDark => _colors.Border;
    public override Color SeparatorLight => _colors.Surface;
}
