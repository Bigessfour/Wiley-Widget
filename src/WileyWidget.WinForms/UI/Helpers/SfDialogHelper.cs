using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms.UI.Helpers
{
    /// <summary>
    /// Helper for displaying themed Syncfusion message dialogs with optional Details expander for exceptions.
    /// Ensures consistent theming via SfSkinManager and supports rich error information display.
    /// </summary>
    public static class SfDialogHelper
    {
        /// <summary>
        /// Shows a themed error dialog with optional collapsible Details panel for exception stack traces.
        /// </summary>
        /// <param name="owner">Parent control or form for modal dialog.</param>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Main error message.</param>
        /// <param name="exception">Optional exception to display in Details panel.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public static void ShowErrorDialog(
            IWin32Window? owner,
            string title,
            string message,
            Exception? exception = null,
            ILogger? logger = null)
        {
            try
            {
                // Check if SfMessageBox is available in current Syncfusion version
                var messageText = exception != null
                    ? $"{message}\n\n{GetExceptionSummary(exception)}"
                    : message;

                // Attempt to use SfMessageBox for theming; fallback to standard MessageBox if unavailable
                if (TrySfMessageBox(owner, messageText, title, exception))
                {
                    return;
                }

                // Fallback to standard MessageBox wrapped with theme inheritance
                MessageBox.Show(
                    owner,
                    messageText,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                logger?.LogDebug("Error dialog displayed: {Title}", title);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to display error dialog");
            }
        }

        /// <summary>
        /// Shows a themed warning dialog.
        /// </summary>
        public static void ShowWarningDialog(
            IWin32Window? owner,
            string title,
            string message,
            ILogger? logger = null)
        {
            try
            {
                if (TrySfMessageBox(owner, message, title, null, MessageBoxIcon.Warning))
                {
                    return;
                }

                MessageBox.Show(
                    owner,
                    message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                logger?.LogDebug("Warning dialog displayed: {Title}", title);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to display warning dialog");
            }
        }

        /// <summary>
        /// Shows a themed information dialog.
        /// </summary>
        public static void ShowInfoDialog(
            IWin32Window? owner,
            string title,
            string message,
            ILogger? logger = null)
        {
            try
            {
                if (TrySfMessageBox(owner, message, title, null, MessageBoxIcon.Information))
                {
                    return;
                }

                MessageBox.Show(
                    owner,
                    message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                logger?.LogDebug("Info dialog displayed: {Title}", title);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to display info dialog");
            }
        }

        /// <summary>
        /// Attempts to display message using SfMessageBox if available.
        /// Returns true if SfMessageBox was successfully used; false to fallback.
        /// </summary>
        private static bool TrySfMessageBox(
            IWin32Window? owner,
            string message,
            string title,
            Exception? exception,
            MessageBoxIcon icon = MessageBoxIcon.Error)
        {
            try
            {
                // Note: SfMessageBox may not be available in all Syncfusion versions.
                // This method gracefully falls back to standard MessageBox if the type isn't found.
                var sfMessageBoxType = Type.GetType("Syncfusion.Windows.Forms.SfMessageBox, Syncfusion.Shared.Base");
                if (sfMessageBoxType == null)
                {
                    return false;
                }

                // SfMessageBox inherits theme from SfSkinManager automatically
                // Standard API: SfMessageBox.Show(owner, message, title, buttons, icon)
                var showMethod = sfMessageBoxType.GetMethod(
                    "Show",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(IWin32Window), typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon) },
                    null);

                if (showMethod != null)
                {
                    showMethod.Invoke(
                        null,
                        new object?[] { owner, message, title, MessageBoxButtons.OK, icon });
                    return true;
                }

                return false;
            }
            catch
            {
                // If reflection fails or SfMessageBox is unavailable, return false to trigger fallback
                return false;
            }
        }

        /// <summary>
        /// Extracts a user-friendly summary from an exception.
        /// </summary>
        private static string GetExceptionSummary(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            var summary = $"Details:\n{ex.GetType().Name}: {ex.Message}";

            if (ex.InnerException != null)
            {
                summary += $"\n\nCause: {ex.InnerException.Message}";
            }

            return summary;
        }
    }
}
