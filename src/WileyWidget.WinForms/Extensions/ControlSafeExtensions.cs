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
    /// <summary>
    /// Represents a class for controlsafeextensions.
    /// </summary>
    /// <summary>
    /// Represents a class for controlsafeextensions.
    /// </summary>
    /// <summary>
    /// Represents a class for controlsafeextensions.
    /// </summary>
    /// <summary>
    /// Represents a class for controlsafeextensions.
    /// </summary>
    public static class ControlSafeExtensions
    {
        #region DataSource Clearing
        /// <summary>
        /// Performs safecleardatasource. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>

        /// <summary>
        /// Safely clears DataSource on SfDataGrid with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Performs safecleardatasource. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safecleardatasource. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        public static void SafeClearDataSource(this SfDataGrid? grid)
        {
            if (grid == null) return;
            try
            {
                if (grid.InvokeRequired)
                    grid.Invoke(() => grid.DataSource = null);
                else
                    grid.DataSource = null;
            }
            catch { }
        }

        /// <summary>
        /// Safely clears DataSource on SfComboBox with proper thread marshaling.
        /// </summary>
        public static void SafeClearDataSource(this SfComboBox? combo)
        {
            if (combo == null) return;
            try
            {
                if (combo.InvokeRequired)
                    combo.Invoke(() => combo.DataSource = null);
                else
                    combo.DataSource = null;
            }
            catch { }
        }

        /// <summary>
        /// Safely clears DataSource on SfListView with proper thread marshaling.
        /// </summary>
        public static void SafeClearDataSource(this SfListView? list)
        {
            if (list == null) return;
            try
            {
                if (list.InvokeRequired)
                    list.Invoke(() => list.DataSource = null);
                else
                    list.DataSource = null;
            }
            catch { }
        }

        #endregion

        #region Safe Dispose - Syncfusion Specific

        /// <summary>
        /// Safely disposes SfDataGrid, swallowing any internal Syncfusion exceptions during shutdown.
        /// </summary>
        /// <summary>
        /// Performs safedispose. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safedispose. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safedispose. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safedispose. Parameters: control.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs safedispose. Parameters: component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <summary>
        /// Performs safedispose. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safedispose. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safedispose. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safedispose. Parameters: control.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs safedispose. Parameters: component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <summary>
        /// Performs safedispose. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safedispose. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safedispose. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safedispose. Parameters: control.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs safedispose. Parameters: component.
        /// </summary>
        /// <param name="component">The component.</param>
        /// <summary>
        /// Performs safedispose. Parameters: grid.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <summary>
        /// Performs safedispose. Parameters: combo.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <summary>
        /// Performs safedispose. Parameters: list.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <summary>
        /// Performs safedispose. Parameters: control.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <summary>
        /// Performs safedispose. Parameters: component.
        /// </summary>
        /// <param name="component">The component.</param>
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
                // Silently ignore
            }
        }

        #endregion

        #region Thread-Safe Invocation (WW1005 Analyzer Compliance)

        /// <summary>
        /// Executes an action on the UI thread, marshaling via Invoke if needed.
        /// Provides real thread affinity protection for cross-thread UI access.
        /// </summary>
        /// <typeparam name="TControl">The control type.</typeparam>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="action">The action to execute.</param>
        public static void SafeInvoke<TControl>(this TControl control, Action<TControl> action)
            where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(action);
            if (control == null || control.IsDisposed) return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(() => action(control));
                }
                catch (ObjectDisposedException)
                {
                    // Control disposed during invoke - safe to ignore
                }
                catch (InvalidOperationException)
                {
                    // Handle closing/disposed during cross-thread call
                }
            }
            else
            {
                action(control);
            }
        }

        /// <summary>
        /// Executes an action on the UI thread, marshaling via Invoke if needed.
        /// Provides real thread affinity protection for cross-thread UI access.
        /// </summary>
        /// <typeparam name="TControl">The control type.</typeparam>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="action">The action to execute.</param>
        public static void SafeInvoke<TControl>(this TControl control, Action action)
            where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(action);
            if (control == null || control.IsDisposed) return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control disposed during invoke - safe to ignore
                }
                catch (InvalidOperationException)
                {
                    // Handle closing/disposed during cross-thread call
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result, marshaling via Invoke if needed.
        /// Provides real thread affinity protection for cross-thread UI access.
        /// </summary>
        /// <typeparam name="TControl">The control type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function, or default(TResult) if control is disposed.</returns>
        public static TResult SafeInvoke<TControl, TResult>(this TControl control, Func<TControl, TResult> func)
            where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(func);

            if (control == null || control.IsDisposed) return default!;

            if (control.InvokeRequired)
            {
                try
                {
                    return control.Invoke(() => func(control));
                }
                catch (ObjectDisposedException)
                {
                    return default!;
                }
                catch (InvalidOperationException)
                {
                    return default!;
                }
            }
            else
            {
                return func(control);
            }
        }

        /// <summary>
        /// Safely sets a control property with proper thread marshaling.
        /// Generic helper for any property assignment.
        /// </summary>
        /// <typeparam name="TControl">The control type.</typeparam>
        /// <param name="control">The control to modify.</param>
        /// <param name="setter">The property setter action.</param>
        public static void SafeSetProperty<TControl>(this TControl control, Action<TControl> setter)
            where TControl : Control
        {
            control.SafeInvoke(setter);
        }

        /// <summary>
        /// Asynchronously invokes an action on the UI thread (fire-and-forget).
        /// Use when you don't need to wait for completion.
        /// </summary>
        /// Use when you don't need to wait for completion.
        /// </summary>
        /// <typeparam name="TControl">The control type.</typeparam>
        /// <param name="control">The control to invoke on.</param>
        /// <param name="action">The action to execute.</param>
        /// <summary>
        /// Asynchronously invokes an action on the UI thread (fire-and-forget).
        /// Use when you don't need to wait for completion.
        /// </summary>
        public static void SafeBeginInvoke<TControl>(this TControl control, Action<TControl> action)
            where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(action);
            if (control == null || control.IsDisposed) return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(() =>
                    {
                        if (!control.IsDisposed)
                            action(control);
                    });
                }
                catch (ObjectDisposedException)
                {
                    // Control disposed - safe to ignore
                }
                catch (InvalidOperationException)
                {
                    // Handle closing/disposed
                }
            }
            else
            {
                action(control);
            }
        }

        /// <summary>
        /// Safely sets DataSource on SfDataGrid with proper thread marshaling.
        /// Common pattern for binding grids from async methods.
        /// </summary>
        /// <summary>
        /// Safely sets DataSource on SfDataGrid with proper thread marshaling.
        /// Common pattern for binding grids from async methods.
        /// </summary>
        /// <summary>
        /// Performs safesetdatasource. Parameters: grid, dataSource.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <param name="dataSource">The dataSource.</param>
        /// <summary>
        /// Performs safesetdatasource. Parameters: combo, dataSource.
        /// </summary>
        /// <param name="combo">The combo.</param>
        /// <param name="dataSource">The dataSource.</param>
        /// <summary>
        /// Performs safesetdatasource. Parameters: list, dataSource.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="dataSource">The dataSource.</param>
        public static void SafeSetDataSource(this SfDataGrid grid, object? dataSource)
        {
            grid.SafeInvoke(g => g.DataSource = dataSource);
        }

        /// <summary>
        /// Safely sets DataSource on SfComboBox with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Safely sets DataSource on SfComboBox with proper thread marshaling.
        /// </summary>
        public static void SafeSetDataSource(this SfComboBox combo, object? dataSource)
        {
            combo.SafeInvoke(c => c.DataSource = dataSource);
        }

        /// <summary>
        /// Safely sets DataSource on SfListView with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Safely sets DataSource on SfListView with proper thread marshaling.
        /// </summary>
        public static void SafeSetDataSource(this SfListView list, object? dataSource)
        {
            list.SafeInvoke(l => l.DataSource = dataSource);
        }

        /// <summary>
        /// Safely sets Text property with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Safely sets Text property with proper thread marshaling.
        /// Common pattern for updating labels, buttons, etc. from async methods.
        /// </summary>
        /// <summary>
        /// Performs safesettext. Parameters: control, text.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="text">The text.</param>
        public static void SafeSetText(this Control control, string text)
        {
            control.SafeInvoke(c => c.Text = text);
        }

        /// <summary>
        /// Safely sets Enabled property with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Safely sets Enabled property with proper thread marshaling.
        /// Common pattern for enabling/disabling controls from async methods.
        /// </summary>
        /// <summary>
        /// Performs safesetenabled. Parameters: control, enabled.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="enabled">The enabled.</param>
        public static void SafeSetEnabled(this Control control, bool enabled)
        {
            control.SafeInvoke(c => c.Enabled = enabled);
        }

        /// <summary>
        /// Safely sets Visible property with proper thread marshaling.
        /// </summary>
        /// <summary>
        /// Safely sets Visible property with proper thread marshaling.
        /// Common pattern for showing/hiding controls from async methods.
        /// </summary>
        /// <summary>
        /// Performs safesetvisible. Parameters: control, visible.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="visible">The visible.</param>
        public static void SafeSetVisible(this Control control, bool visible)
        {
            control.SafeInvoke(c => c.Visible = visible);
        }

        #endregion
    }
}
