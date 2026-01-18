using System;
using System.ComponentModel;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Safe dispose helpers to avoid exceptions in shutdown code paths.
    /// Repo-wide solution for Syncfusion NullReferenceExceptions during Dispose (e.g., UnWireEvents).
    /// </summary>
    public static class ControlSafeExtensions
    {
        #region DataSource Clearing

        public static void SafeClearDataSource(this SfDataGrid? grid)
        {
            if (grid == null) return;
            try { grid.DataSource = null; } catch { }
        }

        public static void SafeClearDataSource(this SfComboBox? combo)
        {
            if (combo == null) return;
            try { combo.DataSource = null; } catch { }
        }

        public static void SafeClearDataSource(this SfListView? list)
        {
            if (list == null) return;
            try { list.DataSource = null; } catch { }
        }

        #endregion

        #region Safe Dispose - Syncfusion Specific

        /// <summary>
        /// Safely disposes SfDataGrid, swallowing any internal Syncfusion exceptions during shutdown.
        /// </summary>
        public static void SafeDispose(this SfDataGrid? grid)
        {
            if (grid == null || grid.IsDisposed) return;

            try
            {
                grid.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for SfDataGrid: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore - these are non-fatal Syncfusion dispose quirks
            }
        }

        /// <summary>
        /// Safely disposes SfComboBox, swallowing any internal Syncfusion exceptions during shutdown.
        /// Handles known UnWireEvents NullReferenceException in older Syncfusion versions.
        /// </summary>
        public static void SafeDispose(this SfComboBox? combo)
        {
            if (combo == null || combo.IsDisposed) return;

            try
            {
                combo.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for SfComboBox: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore - these are non-fatal Syncfusion dispose quirks
            }
        }

        /// <summary>
        /// Safely disposes SfListView, swallowing any internal Syncfusion exceptions during shutdown.
        /// </summary>
        public static void SafeDispose(this SfListView? list)
        {
            if (list == null || list.IsDisposed) return;

            try
            {
                list.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for SfListView: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore - these are non-fatal Syncfusion dispose quirks
            }
        }

        #endregion

        #region Safe Dispose - Generic Control/Component

        /// <summary>
        /// Safely disposes any Control, swallowing exceptions (useful for Syncfusion controls during shutdown).
        /// </summary>
        public static void SafeDispose(this Control? control)
        {
            if (control == null || control.IsDisposed) return;

            try
            {
                control.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for {control.GetType().Name}: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore - these are non-fatal dispose quirks
            }
        }

        /// <summary>
        /// Safely disposes any IComponent, swallowing exceptions.
        /// </summary>
        public static void SafeDispose(this IComponent? component)
        {
            if (component == null) return;

            // Check if disposed (Control-specific check)
            if (component is Control control && control.IsDisposed)
                return;

            try
            {
                component.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for {component.GetType().Name}: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore
            }
        }

        /// <summary>
        /// Safely disposes any IContainer (e.g., components field in forms).
        /// Handles Syncfusion BackStageView/BackStage NullReferenceException during container disposal.
        /// </summary>
        public static void SafeDispose(this IContainer? container)
        {
            if (container == null) return;

            try
            {
                container.Dispose();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SafeDispose] Swallowed exception for {container.GetType().Name}: {ex.Message}");
#endif
                _ = ex;
                // Silently ignore - Syncfusion controls in container may have disposal quirks
            }
        }

        #endregion

        #region Task Timeout Extensions

        /// <summary>
        /// Applies a timeout to an async Task operation using Task.WhenAny pattern.
        /// Throws TimeoutException if the task doesn't complete within the specified timeout.
        /// Note: Does not cancel the underlying task—only prevents waiting beyond timeout.
        /// The task continues executing in background (fire-and-forget after timeout).
        /// </summary>
        public static async System.Threading.Tasks.Task<T> WithTimeout<T>(
            this System.Threading.Tasks.Task<T> task,
            TimeSpan timeout)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var delayTask = System.Threading.Tasks.Task.Delay(timeout);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(task, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds} seconds");
            }

            // Task completed first, return its result
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Applies a timeout to an async Task operation (non-generic) using Task.WhenAny pattern.
        /// Throws TimeoutException if the task doesn't complete within the specified timeout.
        /// Note: Does not cancel the underlying task—only prevents waiting beyond timeout.
        /// </summary>
        public static async System.Threading.Tasks.Task WithTimeout(
            this System.Threading.Tasks.Task task,
            TimeSpan timeout)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var delayTask = System.Threading.Tasks.Task.Delay(timeout);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(task, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Task did not complete within {timeout.TotalSeconds} seconds");
            }

            // Task completed first, await it to propagate any exceptions
            await task.ConfigureAwait(false);
        }

        #endregion
    }
}
