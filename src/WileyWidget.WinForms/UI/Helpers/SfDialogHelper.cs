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
        private const string UiTestEnvVar = "WILEYWIDGET_UI_TESTS";
        private const string TestEnvVar = "WILEYWIDGET_TESTS";

        private static bool IsUiTestMode()
        {
            return string.Equals(Environment.GetEnvironmentVariable(UiTestEnvVar), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Environment.GetEnvironmentVariable(TestEnvVar), "true", StringComparison.OrdinalIgnoreCase);
        }

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
                if (IsUiTestMode())
                {
                    logger?.LogDebug("UI test mode: suppressed error dialog {Title}", title);
                    return;
                }

                // Check if SfMessageBox is available in current Syncfusion version
                var messageText = exception != null
                    ? $"{message}\n\n{GetExceptionSummary(exception)}"
                    : message;

                ShowDialog(owner, title, messageText, MessageBoxButtons.OK, MessageBoxIcon.Error);

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
                if (IsUiTestMode())
                {
                    logger?.LogDebug("UI test mode: suppressed warning dialog {Title}", title);
                    return;
                }

                ShowDialog(owner, title, message, MessageBoxButtons.OK, MessageBoxIcon.Warning);

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
                if (IsUiTestMode())
                {
                    logger?.LogDebug("UI test mode: suppressed info dialog {Title}", title);
                    return;
                }

                ShowDialog(owner, title, message, MessageBoxButtons.OK, MessageBoxIcon.Information);

                logger?.LogDebug("Info dialog displayed: {Title}", title);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to display info dialog");
            }
        }

        /// <summary>
        /// Shows a themed yes/no confirmation dialog and returns true when the affirmative action is selected.
        /// </summary>
        public static bool ShowConfirmationDialog(
            IWin32Window? owner,
            string title,
            string message,
            ILogger? logger = null,
            bool defaultResult = false)
        {
            try
            {
                if (IsUiTestMode())
                {
                    logger?.LogDebug("UI test mode: suppressed confirmation dialog {Title}", title);
                    return defaultResult;
                }

                var result = ShowDialog(owner, title, message, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                logger?.LogDebug("Confirmation dialog displayed: {Title} => {Result}", title, result);
                return result == DialogResult.Yes;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to display confirmation dialog {Title}", title);
                return defaultResult;
            }
        }

        /// <summary>
        /// Shows a themed dialog and returns the selected dialog result.
        /// </summary>
        public static DialogResult ShowDialogResult(
            IWin32Window? owner,
            string title,
            string message,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            var sfResult = TrySfMessageBox(owner, message, title, buttons, icon, defaultButton);
            if (sfResult.HasValue)
            {
                return sfResult.Value;
            }

            return System.Windows.Forms.MessageBox.Show(owner, message, title, buttons, icon, defaultButton);
        }

        private static DialogResult ShowDialog(
            IWin32Window? owner,
            string title,
            string message,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            return ShowDialogResult(owner, title, message, buttons, icon);
        }

        /// <summary>
        /// Attempts to display message using SfMessageBox if available.
        /// Returns the result if SfMessageBox was successfully used; null to fallback.
        /// </summary>
        private static DialogResult? TrySfMessageBox(
            IWin32Window? owner,
            string message,
            string title,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            try
            {
                // Note: SfMessageBox may not be available in all Syncfusion versions.
                // This method gracefully falls back to standard MessageBox if the type isn't found.
                var sfMessageBoxType = Type.GetType("Syncfusion.Windows.Forms.SfMessageBox, Syncfusion.Shared.Base");
                if (sfMessageBoxType == null)
                {
                    return null;
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
                    var result = showMethod.Invoke(
                        null,
                        new object?[] { owner, message, title, buttons, icon });
                    return result is DialogResult dialogResult ? dialogResult : DialogResult.OK;
                }

                showMethod = sfMessageBoxType.GetMethod(
                    "Show",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(IWin32Window), typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon), typeof(MessageBoxDefaultButton) },
                    null);

                if (showMethod != null)
                {
                    var result = showMethod.Invoke(
                        null,
                        new object?[] { owner, message, title, buttons, icon, defaultButton });
                    return result is DialogResult dialogResult ? dialogResult : DialogResult.OK;
                }

                return null;
            }
            catch
            {
                // If reflection fails or SfMessageBox is unavailable, return false to trigger fallback
                return null;
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
