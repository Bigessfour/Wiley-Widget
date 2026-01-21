using System;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Extensions
{
    public static class DockingManagerExtensions
    {
        /// <summary>
        /// Ensures proper Z-order for docked controls. Call after layout load.
        /// </summary>
        public static void EnsureZOrder(this DockingManager dockingManager)
        {
            try
            {
                if (dockingManager?.HostControl is Control host)
                {
                    host.BringToFront();
                }
            }
            catch
            {
                // Ignore z-order errors
            }
        }

        /// <summary>
        /// Safely disposes docking manager, unsubscribing all events first.
        /// Call in Form.Dispose(bool) to prevent ObjectDisposedExceptions.
        /// Handles Syncfusion v32.1.19 quirks where disposal can throw if
        /// events aren't fully unsubscribed.
        /// </summary>
        public static void DisposeSafely(this DockingManager dockingManager)
        {
            if (dockingManager == null) return;

            try
            {
                // Unhook host control first to prevent layout logic running during disposal
                dockingManager.HostControl = null;
                dockingManager.PersistState = false;

                dockingManager.Dispose();
            }
            catch (ObjectDisposedException)
            {
                /* already disposed */
            }
            catch (Exception)
            {
                // Ignore other disposal errors (common in Syncfusion v32.1.19)
            }
        }
    }
}
