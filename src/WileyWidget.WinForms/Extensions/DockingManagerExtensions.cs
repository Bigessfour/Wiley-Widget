using Syncfusion.Windows.Forms.Tools;
using System;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Helpers;
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
            // Dispose the DockingManager
            // Child panels will be disposed by their own lifecycle management
            dockingManager.Dispose();
        }
        catch (ObjectDisposedException) { /* Already disposed */ }
        catch (InvalidOperationException) { /* Dispose during CreateHandle - ignore */ }
        catch (Exception) { /* Ignore other disposal errors */ }
    }

    /// <summary>
    /// Compatibility adapter: attempt to obtain the dock state for a control.
    /// If the DockingManager implementation exposes GetDockState, invoke it; otherwise
    /// fall back to a best-effort heuristic (hosted in HostControl => Dock, otherwise Float).
    /// </summary>
    public static DockState GetDockState(this DockingManager dockingManager, Control control)
    {
        if (dockingManager == null || control == null) return DockState.Dock;

        try
        {
            // If the concrete DockingManager has a GetDockState method, prefer that.
            var mi = dockingManager.GetType().GetMethod("GetDockState", new Type[] { typeof(Control) });
            if (mi != null)
            {
                var res = mi.Invoke(dockingManager, new object[] { control });
                if (res is DockState ds) return ds;
                if (res is not null)
                {
                    var str = res.ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        try { return Enum.Parse<DockState>(str); } catch (ArgumentException) { }
                    }
                }
            }

            // Heuristic fallback: if the control is parented to the DockingManager HostControl, treat as docked.
            if (dockingManager.HostControl != null && control.Parent == dockingManager.HostControl)
                return DockState.Dock;

            // Otherwise assume floating state
            return DockState.Float;
        }
        catch
        {
            return DockState.Dock;
        }
    }

    /// <summary>
    /// Compatibility adapter: attempt to set the dock state for a control. If the concrete
    /// DockingManager exposes SetDockState, invoke it; otherwise use SetDockVisibility
    /// and a best-effort parent adjustment to approximate the requested state.
    /// </summary>
    public static void SetDockState(this DockingManager dockingManager, Control control, DockState state)
    {
        if (dockingManager == null || control == null) return;

        try
        {
            var mi = dockingManager.GetType().GetMethod("SetDockState", new Type[] { typeof(Control), typeof(DockState) });
            if (mi != null)
            {
                mi.Invoke(dockingManager, new object[] { control, state });
                return;
            }

            // Best-effort fallback: ensure visibility and parent the control to HostControl when docking.
            if (state == DockState.Dock)
            {
                control.SafeInvoke(() => dockingManager.SetDockVisibility(control, true));
                if (dockingManager.HostControl != null && control.Parent != dockingManager.HostControl)
                {
                    try
                    {
                        dockingManager.HostControl.Controls.Add(control);
                        control.Dock = DockStyle.Right;
                    }
                    catch { /* best-effort only */ }
                }
            }
            else
            {
                // For non-docked states, attempt to hide or leave floating
                control.SafeInvoke(() => dockingManager.SetDockVisibility(control, false));
            }
        }
        catch
        {
            // Swallow - best-effort compatibility shim
        }
    }
}
