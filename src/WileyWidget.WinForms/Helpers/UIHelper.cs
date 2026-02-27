using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Thread-safe UI helper for cross-thread operations.
    /// Wraps UI operations with InvokeRequired checks to prevent cross-thread exceptions.
    /// Essential for async/await continuations that may land on worker threads.
    /// </summary>
    public static class UIHelper
    {
        private const string UiTestEnvVar = "WILEYWIDGET_UI_TESTS";
        private const string TestEnvVar = "WILEYWIDGET_TESTS";

        private static bool IsTruthyEnvironmentVariable(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUiTestMode()
        {
            return IsTruthyEnvironmentVariable(UiTestEnvVar)
                || IsTruthyEnvironmentVariable(TestEnvVar);
        }

        /// <summary>
        /// Shows a message box on the UI thread, with automatic thread marshalling via Invoke.
        /// Safe to call from background threads or async continuations.
        /// </summary>
        public static DialogResult ShowMessageOnUI(
            Control control,
            string message,
            string title,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None,
            ILogger? logger = null)
        {
            if (IsUiTestMode())
            {
                logger?.LogDebug("UI test mode: suppressed message box {Title}", title);
                return DialogResult.None;
            }

            if (control == null)
                // If control is null, fallback to non-owned MessageBox
                return MessageBox.Show(message, title, buttons, icon);

            if (control.IsDisposed || !control.IsHandleCreated)
                return DialogResult.None;

            try
            {
                return control.ExecuteOnUIThread(
                    () => MessageBox.Show(control, message, title, buttons, icon),
                    logger);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to show message box on UI thread.");
                return DialogResult.None;
            }
        }

        /// <summary>
        /// Shows an error message box on the UI thread.
        /// </summary>
        public static void ShowErrorOnUI(Control control, string message, string title = "Error", ILogger? logger = null)
        {
            ShowMessageOnUI(control, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error, logger);
        }

        /// <summary>
        /// Updates a Syncfusion StatusBarAdvPanel on the UI thread.
        /// </summary>
        public static void UpdateStatus(StatusBarAdvPanel panel, string message, Image? icon = null)
        {
            if (panel == null || panel.IsDisposed) return;
            panel.InvokeIfRequired(() => panel.Text = message);
        }

        /// <summary>
        /// Applies theme to form using SfSkinManager rules via ThemeColors helper.
        /// Thread-safe.
        /// </summary>
        public static void ApplyTheme(Form form, string themeName)
        {
            if (form == null || form.IsDisposed) return;
            form.InvokeIfRequired(() => ThemeColors.ApplyTheme(form, themeName));
        }

        /// <summary>
        /// POLISH: Styles a label as a fallback data indicator with visual distinction.
        /// Applies italic font, gray text color, and optional warning icon to indicate fallback data.
        /// </summary>
        /// <param name="label">The label control to style.</param>
        /// <param name="isFallback">True to apply fallback styling, false to remove it.</param>
        public static void StyleAsFallbackIndicator(Label label, bool isFallback = true)
        {
            if (label == null || label.IsDisposed)
                return;

            try
            {
                if (isFallback)
                {
                    // POLISH: Visual distinction for fallback data
                    label.ForeColor = Color.Gray;  // Gray text to indicate fallback/deprecated data
                    label.Font = new Font(label.Font.FontFamily, label.Font.Size, FontStyle.Italic);  // Italics to indicate fallback
                }
                else
                {
                    // Restore normal styling
                    label.ForeColor = SystemColors.ControlText;
                    label.Font = new Font(label.Font.FontFamily, label.Font.Size, FontStyle.Regular);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error styling fallback indicator: {ex.Message}");
            }
        }

        /// <summary>
        /// POLISH: Styles a panel header to indicate fallback data with visual cue.
        /// Useful for DockingHostFactory and similar panel headers.
        /// </summary>
        /// <param name="control">The control to style (typically a panel header).</param>
        /// <param name="isFallback">True to apply fallback styling, false to remove it.</param>
        public static void StyleHeaderAsFallbackIndicator(Control control, bool isFallback = true)
        {
            if (control == null || control.IsDisposed)
                return;

            try
            {
                if (isFallback && control is Label label)
                {
                    StyleAsFallbackIndicator(label, true);
                }
                else if (isFallback && control is not Label)
                {
                    // Removed manual BackColor to respect SfSkinManager theme cascade.
                    // Apply Syncfusion theme to the control as a best-effort alternative for dynamically-created controls.
                    try
                    {
                        control.ApplySyncfusionTheme(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);
                    }
                    catch
                    {
                        // Best-effort: do not throw if theming fails
                    }
                }
                else
                {
                    // Restore theme-driven styling
                    try
                    {
                        control.ApplySyncfusionTheme(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);
                    }
                    catch
                    {
                        // Best-effort: do not throw if theming fails
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error styling header fallback indicator: {ex.Message}");
            }
        }
    }
}
