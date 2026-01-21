using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Extensions
{
    public static class SyncfusionExtensions
    {
        /// <summary>
        /// Validates and converts ribbon images to prevent Syncfusion v32.1.19
        /// ImageAnimator disposal issues. Call after ribbon is fully initialized.
        /// </summary>
        /// <remarks>
        /// Syncfusion v32.1.19 has known issues with animated images and disposal.
        /// This method pre-validates images to catch issues early.
        /// Reference: https://help.syncfusion.com/windowsforms/overview
        /// </remarks>
        public static void ValidateAndConvertImages(this RibbonControlAdv ribbon, ILogger? logger = null)
        {
            if (ribbon == null) return;

            try
            {
                int invalidCount = 0;
                int consolidatedCount = 0;

                foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
                {
                    if (tab.Panel != null)
                    {
                        foreach (Control control in tab.Panel.Controls)
                        {
                            if (control is ToolStripEx toolStrip)
                            {
                                foreach (ToolStripItem item in toolStrip.Items)
                                {
                                    if (item.Image != null)
                                    {
                                        consolidatedCount++;
                                        if (!IsImageValid(item.Image))
                                        {
                                            logger?.LogWarning("Removing invalid image from ribbon item: {ItemName}", item.Name);
                                            item.Image = null;
                                            invalidCount++;
                                        }
                                        else
                                        {
                                            // Optional: Convert to static bitmap if animated to prevent disposal issues
                                            var staticImage = ConvertToStaticBitmap(item.Image);
                                            if (staticImage != item.Image && staticImage != null)
                                            {
                                                item.Image = staticImage;
                                            }
                                        }
                                    }

                                    // Check nested items in panels
                                    if (item is ToolStripPanelItem panelItem)
                                    {
                                        foreach (ToolStripItem panelSubItem in panelItem.Items)
                                        {
                                            if (panelSubItem.Image != null)
                                            {
                                                consolidatedCount++;
                                                if (!IsImageValid(panelSubItem.Image))
                                                {
                                                    logger?.LogWarning("Removing invalid image from ribbon panel item: {ItemName}", panelSubItem.Name);
                                                    panelSubItem.Image = null;
                                                    invalidCount++;
                                                }
                                                else
                                                {
                                                     var staticImage = ConvertToStaticBitmap(panelSubItem.Image);
                                                     if (staticImage != panelSubItem.Image && staticImage != null)
                                                     {
                                                         panelSubItem.Image = staticImage;
                                                     }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (invalidCount > 0)
                {
                    logger?.LogWarning("Ribbon images validated: {InvalidCount} invalid images removed", invalidCount);
                }
                else
                {
                    logger?.LogDebug("Ribbon images validated successfully");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error validating ribbon images; continuing");
            }
        }

        /// <summary>
        /// Validates images in a MenuStrip and its items.
        /// </summary>
        public static void ValidateAndConvertImages(this MenuStrip menu, ILogger? logger = null)
        {
            if (menu == null) return;

            try
            {
                int invalidCount = 0;
                
                foreach (ToolStripItem topLevelItem in menu.Items)
                {
                    if (topLevelItem is ToolStripMenuItem topLevelMenu)
                    {
                        // Check top-level
                        if (topLevelMenu.Image != null)
                        {
                            if (!IsImageValid(topLevelMenu.Image))
                            {
                                logger?.LogWarning("Removing invalid image from top-level menu item: {ItemName}", topLevelMenu.Name);
                                topLevelMenu.Image = null;
                                invalidCount++;
                            }
                            else
                            {
                                var staticImage = ConvertToStaticBitmap(topLevelMenu.Image);
                                if (staticImage != topLevelMenu.Image && staticImage != null)
                                    topLevelMenu.Image = staticImage;
                            }
                        }

                        // Check sub-items
                        foreach (ToolStripItem subItem in topLevelMenu.DropDownItems)
                        {
                            if (subItem.Image != null)
                            {
                                if (!IsImageValid(subItem.Image))
                                {
                                    logger?.LogWarning("Removing invalid image from menu item: {ItemName}", subItem.Name);
                                    subItem.Image = null;
                                    invalidCount++;
                                }
                                else
                                {
                                    var staticImage = ConvertToStaticBitmap(subItem.Image);
                                    if (staticImage != subItem.Image && staticImage != null)
                                        subItem.Image = staticImage;
                                }
                            }
                        }
                    }
                }

                if (invalidCount > 0)
                {
                    logger?.LogWarning("Menu images validated: {InvalidCount} invalid images removed", invalidCount);
                    menu.Refresh();
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error validating menu images");
            }
        }

        private static bool IsImageValid(Image? image)
        {
            if (image == null) return false;
            try
            {
                // Simple property access to check validity
                var size = image.Size;
                var pixelFormat = image.PixelFormat;
                
                // Additional check for animated images
                if (ImageAnimator.CanAnimate(image))
                {
                    return true;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Image? ConvertToStaticBitmap(Image animatedImage)
        {
            if (animatedImage == null || !ImageAnimator.CanAnimate(animatedImage))
            {
                return animatedImage;
            }

            try
            {
                // Create a new bitmap with the same dimensions
                var staticBitmap = new Bitmap(animatedImage.Width, animatedImage.Height);

                // Draw the animated image onto the static bitmap (this captures the current/first frame)
                using (var g = Graphics.FromImage(staticBitmap))
                {
                    g.DrawImage(animatedImage, 0, 0, animatedImage.Width, animatedImage.Height);
                }

                return staticBitmap;
            }
            catch
            {
                // If conversion fails, return null or original? Returning null to be safe if original is bad.
                // But if original works, maybe return it?
                // Logic in MainForm.UI.cs said "dispose the animated image and return null"
                try { animatedImage.Dispose(); } catch {}
                return null; 
            }
        }
    }
}
