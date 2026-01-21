using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

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
        /// <param name="control">The control to invoke on (typically the main form)</param>
        /// <param name="message">The message to display</param>
        /// <param name="title">The dialog title</param>
        /// <param name="buttons">MessageBox buttons to display (default: OK)</param>
        /// <param name="icon">MessageBox icon to display (default: None)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>The DialogResult from the message box</returns>
        public static DialogResult ShowMessageOnUI(
            Control control,
            string message,
            string title,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None,
            ILogger? logger = null)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Title cannot be null or empty", nameof(title));

            try
            {
                // Check if we're on the UI thread
                if (control.InvokeRequired)
                {
                    // We're on a background thread; marshal to UI thread
                    logger?.LogDebug("UIHelper: Marshalling MessageBox to UI thread (InvokeRequired=true)");
                    var result = control.Invoke(
                        new Func<DialogResult>(() =>
                            MessageBox.Show(control, message, title, buttons, icon)));
                    return (DialogResult)result;
                }
                else
                {
                    // Already on UI thread; show directly
                    return MessageBox.Show(control, message, title, buttons, icon);
                }
            }
            catch (ObjectDisposedException ex)
            {
                // Control has been disposed; log and return Cancel as safe fallback
                logger?.LogWarning(ex, "UIHelper: Cannot show message - control disposed");
                return DialogResult.Cancel;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "UIHelper: Failed to show message '{Title}': {Message}", title, message);
                throw;
            }
        }

        /// <summary>
        /// Shows an error message on the UI thread with automatic thread marshalling.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="message">The error message</param>
        /// <param name="title">The dialog title (default: "Error")</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void ShowErrorOnUI(
            Control control,
            string message,
            string title = "Error",
            ILogger? logger = null)
        {
            ShowMessageOnUI(control, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error, logger);
        }

        /// <summary>
        /// Shows a warning message on the UI thread with automatic thread marshalling.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="message">The warning message</param>
        /// <param name="title">The dialog title (default: "Warning")</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void ShowWarningOnUI(
            Control control,
            string message,
            string title = "Warning",
            ILogger? logger = null)
        {
            ShowMessageOnUI(control, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning, logger);
        }

        /// <summary>
        /// Shows an information message on the UI thread with automatic thread marshalling.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="message">The information message</param>
        /// <param name="title">The dialog title (default: "Information")</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void ShowInfoOnUI(
            Control control,
            string message,
            string title = "Information",
            ILogger? logger = null)
        {
            ShowMessageOnUI(control, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information, logger);
        }

        /// <summary>
        /// Shows a confirmation dialog on the UI thread with automatic thread marshalling.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="message">The confirmation message</param>
        /// <param name="title">The dialog title (default: "Confirm")</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>True if user clicked Yes/OK; false otherwise</returns>
        public static bool ShowConfirmOnUI(
            Control control,
            string message,
            string title = "Confirm",
            ILogger? logger = null)
        {
            var result = ShowMessageOnUI(control, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, logger);
            return result == DialogResult.Yes;
        }

        /// <summary>
        /// Executes an action on the UI thread with automatic marshalling via BeginInvoke (fire-and-forget).
        /// Useful for fire-and-forget UI updates that don't require return values.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="action">The action to execute</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void ExecuteOnUIAsync(Control control, Action action, ILogger? logger = null)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                if (control.InvokeRequired)
                {
                    logger?.LogDebug("UIHelper: Marshalling action to UI thread via BeginInvoke");
                    control.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException ex)
            {
                logger?.LogWarning(ex, "UIHelper: Cannot execute action - control disposed");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "UIHelper: Failed to execute action on UI thread");
                throw;
            }
        }

        /// <summary>
        /// Executes an action on the UI thread synchronously with automatic marshalling via Invoke.
        /// Blocks until the action completes.
        /// </summary>
        /// <param name="control">The control to invoke on</param>
        /// <param name="action">The action to execute</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public static void ExecuteOnUI(Control control, Action action, ILogger? logger = null)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                if (control.InvokeRequired)
                {
                    logger?.LogDebug("UIHelper: Marshalling action to UI thread via Invoke");
                    control.Invoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException ex)
            {
                logger?.LogWarning(ex, "UIHelper: Cannot execute action - control disposed");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "UIHelper: Failed to execute action on UI thread");
                throw;
            }
        }
    }
}
