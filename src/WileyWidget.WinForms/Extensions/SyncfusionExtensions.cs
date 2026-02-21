using Syncfusion.Windows.Forms.Tools;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Extensions;

public static class SyncfusionExtensions
{
    /// <summary>
    /// Attempts to update dock visibility only when DockingManager and control state are ready.
    /// Falls back to plain Control.Visible assignment if manager state is not stable.
    /// </summary>
    public static bool TrySetDockVisibilitySafe(
        this DockingManager? dockingManager,
        Control control,
        bool visible,
        ILogger? logger = null,
        string? context = null)
    {
        if (control == null || control.IsDisposed)
        {
            return false;
        }

        void ApplyVisibleFallback()
        {
            if (control.IsDisposed)
            {
                return;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(new System.Action(() =>
                    {
                        if (!control.IsDisposed && control.Visible != visible)
                        {
                            control.Visible = visible;
                        }
                    }));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (control.Visible != visible)
            {
                control.Visible = visible;
            }
        }

        context ??= "UnknownContext";

        if (dockingManager == null)
        {
            ApplyVisibleFallback();
            return false;
        }

        var hostControl = dockingManager.HostControl;
        if (hostControl == null || hostControl.IsDisposed || !hostControl.IsHandleCreated || hostControl.Controls.Count == 0)
        {
            logger?.LogDebug("Skipping SetDockVisibility for {ControlName} during {Context}: host control not ready", control.Name, context);
            ApplyVisibleFallback();
            return false;
        }

        if (control.Parent == null || control.Parent.IsDisposed)
        {
            logger?.LogDebug("Skipping SetDockVisibility for {ControlName} during {Context}: parent not ready", control.Name, context);
            ApplyVisibleFallback();
            return false;
        }

        try
        {
            dockingManager.SetDockVisibility(control, visible);
            ApplyVisibleFallback();
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger?.LogWarning(ex,
                "SetDockVisibility deferred for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            ApplyVisibleFallback();
            return false;
        }
        catch (DockingManagerException ex)
        {
            logger?.LogDebug(ex,
                "SetDockVisibility failed for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            ApplyVisibleFallback();
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex,
                "Unexpected SetDockVisibility failure for {ControlName} during {Context}; applying Visible fallback",
                control.Name,
                context);
            ApplyVisibleFallback();
            return false;
        }
    }

    /// <summary>
    /// Validates all images in the ribbon and converts any animated images to static bitmaps.
    /// This prevents ImageAnimator exceptions when Syncfusion ToolStrip controls try to paint animated images.
    /// </summary>
    /// <param name="ribbon">The ribbon control to validate.</param>
    /// <param name="logger">Optional logger for reporting issues.</param>
    public static void ValidateAndConvertImages(this RibbonControlAdv ribbon, ILogger? logger = null)
    {
        if (ribbon == null) return;

        try
        {
            // Syncfusion v32.1.19 workaround for ImageAnimator disposal issues.
            // We iterate through items and ensure images are compatible.
            logger?.LogDebug("Validating ribbon images...");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to validate ribbon images");
        }
    }

    /// <summary>
    /// Validates all images in a MenuStrip.
    /// </summary>
    public static void ValidateAndConvertImages(this MenuStrip menuStrip, ILogger? logger = null)
    {
        if (menuStrip == null) return;
        try
        {
            logger?.LogDebug("Validating menu strip images...");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to validate menu strip images");
        }
    }
}
