#nullable enable

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Helper extensions for WinForms controls to safely dispose and clear DataSource properties.
    /// Protects against known third-party control disposal bugs (e.g., Syncfusion SfComboBox UnWireEvents NRE).
    /// </summary>
    public static class ControlExtensions
    {
        /// <summary>
        /// Safely clears an object's DataSource property (if present) using reflection.
        /// Swallows common exceptions that occur when a control handle is disposed.
        /// </summary>
        public static void SafeClearDataSource(this Control? ctrl, ILogger? logger = null)
        {
            if (ctrl == null) return;

            try
            {
                var type = ctrl.GetType();
                // Look for a DataSource property (common on grids and combo-like controls)
                var prop = type.GetProperty("DataSource", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(ctrl, null);
                }
            }
            catch (TargetInvocationException tie)
            {
                // Common when a control has been partially disposed and the setter attempts to unhook events
                logger?.LogDebug(tie, "SafeClearDataSource: TargetInvocationException clearing DataSource for {Control}", ctrl?.GetType().FullName);
            }
            catch (NullReferenceException nre)
            {
                // Observed in third-party control dispose paths (Syncfusion bug); swallow
                logger?.LogDebug(nre, "SafeClearDataSource: NullReferenceException while clearing DataSource for {Control}", ctrl?.GetType().FullName);
            }
            catch (ObjectDisposedException ode)
            {
                // Control already disposed - nothing to do
                logger?.LogDebug(ode, "SafeClearDataSource: Control already disposed {Control}", ctrl?.GetType().FullName);
            }
            catch (Exception ex)
            {
                // Keep logging at debug so we don't treat this as fatal, but surface diagnostics in dev environments
                logger?.LogDebug(ex, "SafeClearDataSource: Unexpected exception for {Control}", ctrl?.GetType().FullName);
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Safely disposes a control and any known problematic sub-objects. This method will attempt
        /// to clear DataSource before disposing and swallow benign exceptions that are experienced
        /// frequently with third-party WinForms control libraries.
        /// </summary>
        public static void SafeDispose(this Control? ctrl, ILogger? logger = null)
        {
            if (ctrl == null) return;

            try
            {
                // Prefer clearing any DataSource before disposing controls (avoids UnWireEvents NRE in some libraries)
                ctrl.SafeClearDataSource(logger);

                if (!ctrl.IsDisposed)
                {
                    ctrl.Dispose();
                }
            }
            catch (NullReferenceException nre)
            {
                // Known-to-happen in third-party disposal paths -> swallow and log at debug
                logger?.LogDebug(nre, "SafeDispose swallowed NullReferenceException disposing {Control}", ctrl.GetType().FullName);
            }
            catch (ObjectDisposedException odex)
            {
                // Already disposed, ignore
                logger?.LogDebug(odex, "SafeDispose: already disposed {Control}", ctrl.GetType().FullName);
            }
            catch (Exception ex)
            {
                // Keep other exceptions visible but don't crash the host process
                logger?.LogWarning(ex, "SafeDispose: Unexpected error disposing {Control}", ctrl.GetType().FullName);
                Debug.WriteLine(ex);
            }
        }
    }
}
