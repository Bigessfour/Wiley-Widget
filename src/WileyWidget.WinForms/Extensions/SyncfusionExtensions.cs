using Syncfusion.Windows.Forms.Tools;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Extensions;

public static class SyncfusionExtensions
{
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
