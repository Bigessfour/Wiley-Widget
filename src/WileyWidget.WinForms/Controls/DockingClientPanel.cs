using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Dedicated container used as the HostControl for Syncfusion DockingManager.
    /// Ensures theme is applied and propagates the active theme name to child Syncfusion controls
    /// so that SfSkinManager can reliably style all docked panels.
    /// </summary>
    // Rename to avoid collision with Syncfusion's own DockingClientPanel type
    public class DockingHostClientPanel : UserControl
    {
        public DockingHostClientPanel()
        {
            // Default appearance
            Dock = DockStyle.Fill;
            BorderStyle = BorderStyle.None;
            BackColor = Color.Transparent;

            // Apply current application theme if available
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(theme))
                {
                    // Ensure visual style is applied to this container
                    SfSkinManager.SetVisualStyle(this, theme);
                    // Propagate ThemeName to any existing child controls
                    PropagateThemeToChildren(theme);
                }
            }
            catch
            {
                // Swallow failures here - theming is best-effort
            }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            // Ensure newly added controls receive ThemeName if supported
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(theme) && e.Control != null)
                {
                    ApplyThemeToControl(e.Control, theme);
                }
            }
            catch
            {
                // Non-critical
            }
        }

        /// <summary>
        /// Apply the provided theme to this container and all child controls that expose a writable "ThemeName" property.
        /// </summary>
        public void PropagateThemeToChildren(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                return;
            }

            try
            {
                SfSkinManager.SetVisualStyle(this, themeName);

                foreach (Control child in Controls)
                {
                    ApplyThemeToControl(child, themeName);
                }
            }
            catch
            {
                // Best-effort only
            }
        }

        private static void ApplyThemeToControl(Control control, string themeName)
        {
            if (control == null || string.IsNullOrEmpty(themeName))
            {
                return;
            }

            try
            {
                // If a control exposes a ThemeName string property, set it.
                var prop = control.GetType().GetProperty("ThemeName", BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(control, themeName);
                }

                // Also apply SfSkinManager visual style for controls that require it
                SfSkinManager.SetVisualStyle(control, themeName);
            }
            catch
            {
                // Ignore failures - some controls don't support theme operations
            }
        }
    }
}
