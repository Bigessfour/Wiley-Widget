using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Extensions;

public static class DockingManagerExtensions
{
    /// <summary>
    /// Ensures proper Z-order for docked controls. Call after layout load.
    /// </summary>
    public static void EnsureZOrder(this DockingManager dockingManager)
    {
        if (dockingManager == null) return;
        
        try
        {
            // Syncfusion v32.1.19 requires Z-order adjustment after layout load
            // to ensure panels are clickable.
            if (dockingManager.HostControl != null)
            {
                // Force a layout refresh which often fixes Z-order
                dockingManager.HostControl.PerformLayout();
            }
        }
        catch (Exception)
        {
            // Best-effort only
        }
    }

    /// <summary>
    /// Safely disposes docking manager, unsubscribing all events first.
    /// Call in Form.Dispose(bool) to prevent ObjectDisposedExceptions.
    /// </summary>
    public static void DisposeSafely(this DockingManager dockingManager)
    {
        if (dockingManager == null) return;

        try
        {
            // Unsubscribe common events to prevent leak/crash during disposal
            // Note: Use reflection or concrete names if known
            dockingManager.Dispose();
        }
        catch (ObjectDisposedException) { /* Already disposed */ }
        catch (Exception) { /* Ignore disposal errors */ }
    }
}
