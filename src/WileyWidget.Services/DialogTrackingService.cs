using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Centralized service for tracking and managing dialog windows.
    /// Prevents memory leaks and NullReferenceException during shutdown by ensuring
    /// all dialogs are properly closed before container disposal.
    /// </summary>
    public interface IDialogTrackingService
    {
        /// <summary>
        /// Registers a dialog window for tracking.
        /// </summary>
        void RegisterDialog(Window dialog);

        /// <summary>
        /// Unregisters a dialog window (called when it closes normally).
        /// </summary>
        void UnregisterDialog(Window dialog);

        /// <summary>
        /// Gets the count of currently open dialogs.
        /// </summary>
        int OpenDialogCount { get; }

        /// <summary>
        /// Closes all tracked dialog windows gracefully.
        /// Should be called before container disposal during shutdown.
        /// </summary>
        void CloseAllDialogs();

        /// <summary>
        /// Gets a snapshot of currently open dialog types.
        /// </summary>
        IReadOnlyList<string> GetOpenDialogTypes();
    }

    /// <summary>
    /// Implementation of dialog tracking service.
    /// Thread-safe for concurrent dialog operations.
    /// </summary>
    public class DialogTrackingService : IDialogTrackingService
    {
        private readonly ConcurrentDictionary<int, WeakReference<Window>> _trackedDialogs = new();
        private int _nextDialogId = 0;

        /// <inheritdoc/>
        public void RegisterDialog(Window dialog)
        {
            if (dialog == null)
            {
                Log.Warning("Attempted to register null dialog");
                return;
            }

            var dialogId = System.Threading.Interlocked.Increment(ref _nextDialogId);
            _trackedDialogs[dialogId] = new WeakReference<Window>(dialog);

            // Automatically unregister when dialog closes
            dialog.Closed += (s, e) =>
            {
                _trackedDialogs.TryRemove(dialogId, out _);
                Log.Debug("Dialog {DialogType} (ID: {DialogId}) closed and unregistered",
                    dialog.GetType().Name, dialogId);
            };

            Log.Debug("Registered dialog {DialogType} (ID: {DialogId}). Total tracked: {Count}",
                dialog.GetType().Name, dialogId, OpenDialogCount);
        }

        /// <inheritdoc/>
        public void UnregisterDialog(Window dialog)
        {
            if (dialog == null) return;

            var entry = _trackedDialogs.FirstOrDefault(kvp =>
            {
                if (kvp.Value.TryGetTarget(out var target))
                {
                    return ReferenceEquals(target, dialog);
                }
                return false;
            });

            if (entry.Key != 0)
            {
                _trackedDialogs.TryRemove(entry.Key, out _);
                Log.Debug("Unregistered dialog {DialogType}", dialog.GetType().Name);
            }
        }

        /// <inheritdoc/>
        public int OpenDialogCount
        {
            get
            {
                // Clean up dead references and count active ones
                CleanupDeadReferences();
                return _trackedDialogs.Count;
            }
        }

        /// <inheritdoc/>
        public void CloseAllDialogs()
        {
            Log.Information("Closing all tracked dialogs (Count: {Count})", OpenDialogCount);

            var dialogsToClose = new List<Window>();

            // Collect all living dialogs
            foreach (var kvp in _trackedDialogs)
            {
                if (kvp.Value.TryGetTarget(out var dialog))
                {
                    dialogsToClose.Add(dialog);
                }
            }

            // Close each dialog gracefully
            foreach (var dialog in dialogsToClose)
            {
                try
                {
                    if (dialog.IsLoaded)
                    {
                        // Try to set DialogResult for modal dialogs
                        try { dialog.DialogResult = false; } catch { /* Not modal */ }
                        dialog.Close();
                        Log.Debug("Closed dialog {DialogType}", dialog.GetType().Name);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Dialog may not be modal or already closed
                    try { dialog.Close(); } catch { /* Ignore */ }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error closing dialog {DialogType} during shutdown", dialog.GetType().Name);
                }
            }

            // Clear all tracked dialogs
            _trackedDialogs.Clear();
            Log.Information("All dialogs closed and cleared");
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetOpenDialogTypes()
        {
            var dialogTypes = new List<string>();

            foreach (var kvp in _trackedDialogs)
            {
                if (kvp.Value.TryGetTarget(out var dialog))
                {
                    dialogTypes.Add(dialog.GetType().Name);
                }
            }

            return dialogTypes.AsReadOnly();
        }

        /// <summary>
        /// Removes weak references to dialogs that have been garbage collected.
        /// </summary>
        private void CleanupDeadReferences()
        {
            var deadKeys = new List<int>();

            foreach (var kvp in _trackedDialogs)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    deadKeys.Add(kvp.Key);
                }
            }

            foreach (var key in deadKeys)
            {
                _trackedDialogs.TryRemove(key, out _);
            }

            if (deadKeys.Count > 0)
            {
                Log.Debug("Cleaned up {Count} dead dialog references", deadKeys.Count);
            }
        }
    }
}
