using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Utilities
{
    /// <summary>
    /// Helper utility for handling async operations in WinForms event handlers with proper
    /// cancellation support, error handling, and user feedback.
    ///
    /// Provides standardized patterns for:
    /// - CancellationToken propagation in async void handlers
    /// - OperationCanceledException graceful handling
    /// - User-friendly error messaging via MessageBox or status bars
    /// - Logging integration with structured diagnostics
    ///
    /// Usage:
    /// <code>
    /// private CancellationTokenSource? _cts;
    ///
    /// Load += async (s, e) => await AsyncEventHelper.ExecuteAsync(
    ///     async ct => await _viewModel.LoadDataAsync(ct),
    ///     _cts,
    ///     this,
    ///     _logger,
    ///     "Loading data...");
    /// </code>
    /// </summary>
    public static class AsyncEventHelper
    {
        /// <summary>
        /// Executes an async operation with cancellation support, error handling, and UI feedback.
        /// Designed for WinForms async void event handlers (Form.Load, Button.Click, etc.).
        /// </summary>
        /// <typeparam name="T">Return type of the async operation</typeparam>
        /// <param name="operation">The async operation to execute, accepting a CancellationToken</param>
        /// <param name="cancellationTokenSource">CancellationTokenSource to use for cancellation (can be null for non-cancellable)</param>
        /// <param name="control">WinForms control for UI thread marshalling (typically 'this' from the form)</param>
        /// <param name="logger">Logger instance for structured logging (can be null)</param>
        /// <param name="operationName">Human-readable operation name for logging and error messages</param>
        /// <param name="showErrorDialog">Whether to show MessageBox on errors (default: true)</param>
        /// <param name="statusLabel">Optional ToolStripStatusLabel to update with status messages</param>
        /// <returns>Task representing the async operation (for await support in async void handlers)</returns>
        public static async Task<T?> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationTokenSource? cancellationTokenSource,
            Control control,
            ILogger? logger = null,
            string operationName = "Operation",
            bool showErrorDialog = true,
            ToolStripStatusLabel? statusLabel = null)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (control == null) throw new ArgumentNullException(nameof(control));

            var cancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                // Update status label if provided
                UpdateStatusLabel(statusLabel, control, $"{operationName}...");

                logger?.LogDebug("{OperationName} started", operationName);
                var result = await operation(cancellationToken).ConfigureAwait(true);
                logger?.LogDebug("{OperationName} completed successfully", operationName);

                UpdateStatusLabel(statusLabel, control, "Ready");
                return result;
            }
            catch (OperationCanceledException oce)
            {
                // Graceful cancellation - suppress logging during normal app lifecycle (Debug only)
                logger?.LogDebug(oce, "{OperationName} was canceled", operationName);
                UpdateStatusLabel(statusLabel, control, $"{operationName} canceled");
                return default;
            }
            catch (Exception ex)
            {
                // Unexpected error - log at Error level and optionally show dialog
                logger?.LogError(ex, "{OperationName} failed: {ErrorMessage}", operationName, ex.Message);
                UpdateStatusLabel(statusLabel, control, $"{operationName} failed");

                if (showErrorDialog && control.InvokeRequired == false && Application.MessageLoop)
                {
                    MessageBox.Show(
                        control,
                        $"{operationName} failed: {ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return default;
            }
        }

        /// <summary>
        /// Executes an async operation (void return) with cancellation support and error handling.
        /// Overload for operations that don't return a value (e.g., data loading into UI controls).
        /// </summary>
        public static async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationTokenSource? cancellationTokenSource,
            Control control,
            ILogger? logger = null,
            string operationName = "Operation",
            bool showErrorDialog = true,
            ToolStripStatusLabel? statusLabel = null)
        {
            await ExecuteAsync<object?>(
                async ct =>
                {
                    await operation(ct).ConfigureAwait(true);
                    return null;
                },
                cancellationTokenSource,
                control,
                logger,
                operationName,
                showErrorDialog,
                statusLabel).ConfigureAwait(true);
        }

        /// <summary>
        /// Executes a synchronous dialog operation with timeout protection.
        /// Wraps ShowDialog() in Task.Run with configurable timeout to prevent UI thread blocking.
        /// </summary>
        /// <param name="showDialogAction">Action that shows the dialog (e.g., () => dialog.ShowDialog(owner))</param>
        /// <param name="timeout">Maximum time to wait for dialog (default: 30 seconds)</param>
        /// <param name="logger">Logger instance for timeout warnings</param>
        /// <param name="dialogName">Human-readable dialog name for logging</param>
        /// <returns>DialogResult if completed, DialogResult.Cancel if timed out</returns>
        public static async Task<DialogResult> ExecuteDialogWithTimeoutAsync(
            Func<DialogResult> showDialogAction,
            TimeSpan? timeout = null,
            ILogger? logger = null,
            string dialogName = "Dialog")
        {
            if (showDialogAction == null) throw new ArgumentNullException(nameof(showDialogAction));

            var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);

            try
            {
                // Run dialog on background thread to avoid UI blocking
                var dialogTask = Task.Run(showDialogAction);
                var completedTask = await Task.WhenAny(dialogTask, Task.Delay(actualTimeout)).ConfigureAwait(true);

                if (completedTask == dialogTask)
                {
                    // Dialog completed normally
                    return await dialogTask.ConfigureAwait(true);
                }
                else
                {
                    // Timeout occurred
                    logger?.LogWarning("{DialogName} timed out after {TimeoutSeconds} seconds", dialogName, actualTimeout.TotalSeconds);
                    return DialogResult.Cancel;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "{DialogName} failed: {ErrorMessage}", dialogName, ex.Message);
                return DialogResult.Cancel;
            }
        }

        /// <summary>
        /// Updates a ToolStripStatusLabel with thread-safe invocation if needed.
        /// </summary>
        private static void UpdateStatusLabel(ToolStripStatusLabel? statusLabel, Control control, string text)
        {
            if (statusLabel == null) return;

            if (control.InvokeRequired)
            {
                control.Invoke(() => statusLabel.Text = text);
            }
            else
            {
                statusLabel.Text = text;
            }
        }

        /// <summary>
        /// Ensures the provided action executes on the UI thread associated with the given control.
        /// Safe to call from background threads.
        /// </summary>
        public static void EnsureOnUiThread(Control control, Action action)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (control.IsDisposed || control.Disposing) return;

            if (control.InvokeRequired)
            {
                if (control.IsHandleCreated)
                {
                    try { control.BeginInvoke(action); } catch { }
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Creates a new CancellationTokenSource with optional timeout.
        /// Useful for form-level operations that should auto-cancel after a duration.
        /// </summary>
        /// <param name="timeout">Optional timeout duration</param>
        /// <returns>New CancellationTokenSource instance</returns>
        public static CancellationTokenSource CreateCancellationTokenSource(TimeSpan? timeout = null)
        {
            return timeout.HasValue
                ? new CancellationTokenSource(timeout.Value)
                : new CancellationTokenSource();
        }

        /// <summary>
        /// Cancels and disposes a CancellationTokenSource safely (null-safe).
        /// Typical usage: FormClosing event to cancel pending operations.
        /// </summary>
        /// <param name="cancellationTokenSource">CancellationTokenSource to cancel and dispose</param>
        public static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }
}
