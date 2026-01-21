using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Thread-safe UI helper for cross-thread operations.
    /// Wraps UI operations with InvokeRequired checks to prevent cross-thread exceptions.
    /// Essential for async/await continuations that may land on worker threads.
    /// </summary>
    public static class UIHelper
    {
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
            if (control == null)
                 // If control is null, fallback to non-owned MessageBox
                 return MessageBox.Show(message, title, buttons, icon);

            if (control.IsDisposed || !control.IsHandleCreated)
                return DialogResult.None;

            if (control.InvokeRequired)
            {
                try
                {
                    return (DialogResult)control.Invoke(new Func<DialogResult>(() => 
                        MessageBox.Show(control, message, title, buttons, icon)));
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to show message box on UI thread.");
                    return DialogResult.None;
                }
            }
            else
            {
                return MessageBox.Show(control, message, title, buttons, icon);
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
             
             if (panel.InvokeRequired)
             {
                 panel.Invoke(new System.Action(() => UpdateStatus(panel, message, icon)));
                 return;
             }
             
             panel.Text = message;
        }

        /// <summary>
        /// Applies theme to form using SfSkinManager rules via ThemeColors helper.
        /// Thread-safe.
        /// </summary>
        public static void ApplyTheme(Form form, string themeName)
        {
             if (form == null || form.IsDisposed) return;
             
             if (form.InvokeRequired)
             {
                 form.Invoke(new System.Action(() => ApplyTheme(form, themeName)));
                 return;
             }

             ThemeColors.ApplyTheme(form, themeName);
        }
    }
}
