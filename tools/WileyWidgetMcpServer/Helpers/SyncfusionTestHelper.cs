using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;

namespace WileyWidget.McpServer.Helpers;

/// <summary>
/// Helper utilities for validating Syncfusion WinForms controls in tests.
/// </summary>
public static class SyncfusionTestHelper
{
    /// <summary>
    /// Validates that a form uses the expected theme via SfSkinManager.
    /// Checks Syncfusion controls for proper theme application.
    /// </summary>
    public static bool ValidateTheme(Form form, string expectedTheme)
    {
        if (form == null || form.IsDisposed)
            return false;

        // Check if any Syncfusion controls have the expected theme
        var syncfusionControls = GetAllSyncfusionControls(form);

        // If no Syncfusion controls, form passes by default
        if (syncfusionControls.Count == 0)
            return true;

        // Check if controls have proper theme name or rely on default
        // Note: ThemeName may be empty if using SfSkinManager cascade
        foreach (var control in syncfusionControls)
        {
            // Try to get ThemeName property via reflection (available on most Syncfusion controls)
            var themeNameProp = control.GetType().GetProperty("ThemeName");
            if (themeNameProp != null)
            {
                var themeName = themeNameProp.GetValue(control) as string;
                // Empty theme name means using parent/SfSkinManager cascade (valid)
                // Otherwise, must match expected theme
                if (!string.IsNullOrEmpty(themeName) && themeName != expectedTheme)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gets all Syncfusion controls recursively from a control tree.
    /// </summary>
    private static List<Control> GetAllSyncfusionControls(Control control)
    {
        var syncfusionControls = new List<Control>();

        // Check if this control is a Syncfusion control
        if (control.GetType().Namespace?.StartsWith("Syncfusion", StringComparison.Ordinal) == true)
        {
            syncfusionControls.Add(control);
        }

        // Recursively check children
        foreach (Control child in control.Controls)
        {
            syncfusionControls.AddRange(GetAllSyncfusionControls(child));
        }

        return syncfusionControls;
    }

    /// <summary>
    /// Validates that no controls have manual BackColor/ForeColor assignments.
    /// Allows semantic status colors (Red/Green/Orange) as exceptions.
    /// </summary>
    public static List<string> ValidateNoManualColors(Control control, string path = "")
    {
        ArgumentNullException.ThrowIfNull(control);
        var violations = new List<string>();
        var currentPath = string.IsNullOrEmpty(path) ? control.Name ?? control.GetType().Name : $"{path}.{control.Name ?? control.GetType().Name}";

        // Allowed colors (semantic status indicators)
        var allowedSemanticColors = new[]
        {
            Color.Red,
            Color.Green,
            Color.Orange,
            Color.Yellow,
            Color.LimeGreen,
            Color.DarkRed,
            Color.DarkGreen,
            Color.OrangeRed
        };

        // Allowed system/default colors (including common WinForms defaults)
        var allowedSystemColors = new[]
        {
            SystemColors.Control,
            SystemColors.Window,
            SystemColors.ControlLight,
            SystemColors.ControlDark,
            SystemColors.ControlText,
            Color.Transparent,
            Color.White,
            Color.Empty,
            Color.FromArgb(243, 243, 243), // Common WinForms default light gray background
            Color.FromArgb(240, 240, 240), // Alternative default background
            Color.FromArgb(68, 68, 68)      // Common WinForms default dark gray text
        };

        // Check BackColor
        if (!allowedSystemColors.Contains(control.BackColor) &&
            !allowedSemanticColors.Contains(control.BackColor) &&
            control.BackColor.A > 0) // Ignore fully transparent
        {
            // Special case: Syncfusion controls may have themed colors
            var isSyncfusionControl = control.GetType().Namespace?.StartsWith("Syncfusion", StringComparison.Ordinal) == true;
            if (!isSyncfusionControl)
            {
                violations.Add($"{currentPath}.BackColor = {ColorToString(control.BackColor)} (manual color - use SfSkinManager instead)");
            }
        }

        // Check ForeColor - also allow standard WinForms default text colors
        var allowedForeColors = allowedSystemColors.Concat(new[] { Color.Black }).ToArray();
        if (!allowedForeColors.Contains(control.ForeColor) &&
            !allowedSemanticColors.Contains(control.ForeColor) &&
            control.ForeColor.A > 0) // Ignore fully transparent
        {
            var isSyncfusionControl = control.GetType().Namespace?.StartsWith("Syncfusion", StringComparison.Ordinal) == true;
            if (!isSyncfusionControl)
            {
                violations.Add($"{currentPath}.ForeColor = {ColorToString(control.ForeColor)} (manual color - use SfSkinManager instead)");
            }
        }

        // Recursively check children
        foreach (Control child in control.Controls)
        {
            violations.AddRange(ValidateNoManualColors(child, currentPath));
        }

        return violations;
    }

    /// <summary>
    /// Converts a color to a readable string format.
    /// </summary>
    private static string ColorToString(Color color)
    {
        if (color.IsNamedColor)
            return color.Name;
        return $"RGB({color.R}, {color.G}, {color.B})";
    }

    /// <summary>
    /// Finds the first SfDataGrid control on a form.
    /// </summary>
    public static SfDataGrid? FindSfDataGrid(Control control, string? gridName = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control is SfDataGrid grid)
        {
            if (gridName == null || control.Name == gridName)
            {
                return grid;
            }
        }

        foreach (Control child in control.Controls)
        {
            var found = FindSfDataGrid(child, gridName);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Validates that an SfDataGrid is properly configured.
    /// </summary>
    public static bool ValidateSfDataGrid(SfDataGrid grid)
    {
        // Basic validation: grid exists, has columns
        return grid != null && grid.Columns.Count > 0;
    }

    /// <summary>
    /// Tries to load a form by showing/hiding it to trigger component initialization.
    /// </summary>
    public static bool TryLoadForm(Form form, int waitMs = 500)
    {
        ArgumentNullException.ThrowIfNull(form);
        try
        {
            form.Show();
            Application.DoEvents();
            Thread.Sleep(waitMs);
            form.Hide();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
