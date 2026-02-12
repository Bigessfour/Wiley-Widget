using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Provides DPI-aware images using Syncfusion's <see cref="ImageListAdv"/> component.
/// Automatically switches between DPI96 (100%), DPI120 (125%), DPI144 (150%), and DPI192 (200%)
/// image sets based on current monitor scaling.
/// </summary>
/// <remarks>
/// Implementation follows Syncfusion guidelines:
/// https://help.syncfusion.com/windowsforms/highdpi-support#automatically-change-images-based-on-dpi-through-imagelistadv-component
///
/// Usage pattern:
/// 1. Initialize once per application via DI container
/// 2. Access images via GetImage(iconName) or GetImageIndex(iconName)
/// 3. ImageListAdv automatically selects correct DPI variant
///
/// Architecture:
/// - Single ImageListAdv instance holds all icons at multiple DPIs
/// - Icons stored in Images collection (default DPI96)
/// - DPIImages collection holds DPI120/144/192 variants
/// - Index-based lookup for toolbar/button assignments
/// - Name-based lookup for dynamic icon retrieval
/// </remarks>
public sealed class DpiAwareImageService : IDisposable
{
    private readonly ILogger<DpiAwareImageService> _logger;
    private readonly ImageListAdv _imageList;
    private readonly Dictionary<string, int> _iconNameToIndex = new();
    private bool _disposed;

    /// <summary>
    /// Gets the underlying ImageListAdv component for direct assignment to toolbars/buttons.
    /// </summary>
    /// <remarks>
    /// Use this property when configuring ToolStrip.ImageList, Button.ImageList, etc.
    /// The ImageListAdv will automatically provide DPI-scaled images.
    /// </remarks>
    public ImageListAdv ImageList => _imageList;

    public DpiAwareImageService(ILogger<DpiAwareImageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize ImageListAdv with base size (96 DPI / 100% scaling)
        _imageList = new ImageListAdv
        {
            ImageSize = new Size(16, 16) // Base size for toolbars/buttons
        };

        // [PERF] Do not load icons eagerly in constructor.
        // This avoids blocking the UI thread during startup (~200ms saved).
        // Icons will be loaded on-demand via GetImage/GetImageIndex.
    }

    private void EnsureIconsLoaded()
    {
        if (_iconNameToIndex.Count > 0) return;

        lock (_iconNameToIndex)
        {
            if (_iconNameToIndex.Count > 0) return;
            LoadIconsInternal();
        }
    }

    private void LoadIconsInternal()
    {
        try
        {
            _logger.LogInformation("DPI-aware image service: performing deferred icon loading...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Icon definitions with resource name mappings
            // Maps icon keys to embedded resource names (without "flat.png" suffix)
            var iconResourceMappings = new Dictionary<string, string>
            {
                // File operations
                ["save"] = "save",
                ["open"] = "open",
                ["export"] = "export",
                ["import"] = "import",
                ["print"] = "print",

                // Navigation
                ["home"] = "home",
                ["back"] = "back",
                ["forward"] = "forward",
                ["refresh"] = "reset",  // Use resetflat.png for refresh
                ["pin"] = "pin",        // Need to add pinflat.png
                ["pin_filled"] = "pin_filled", // Need to add pin_filledflat.png
                ["close"] = "close",    // Need to add closeflat.png

                // Data operations
                ["add"] = "add",
                ["edit"] = "edit",
                ["delete"] = "delete",
                ["search"] = "search",
                ["filter"] = "filter",

                // Dashboard and analytics
                ["dashboard"] = "dashboard",
                ["chart"] = "chart",
                ["charts"] = "charts",
                ["gauge"] = "gauge",
                ["kpi"] = "kpi",
                ["analytics"] = "analytics",
                ["insights"] = "insights",
                ["deptsummary"] = "deptsummary",
                ["insightfeed"] = "insightfeed",

                // Accounts and financial
                ["accounts"] = "accounts",
                ["budget"] = "budget",
                ["budgetoverview"] = "budgetoverview",
                // 'rates' reuses recommendedcharge icon to avoid missing resource; add a dedicated rates icon later
                ["rates"] = "recommendedcharge",
                ["customers"] = "customers",
                ["utilitybill"] = "utilitybill",
                ["revenuetrends"] = "revenuetrends",
                ["recommendedcharge"] = "recommendedcharge",

                // Reports and audit
                ["report"] = "report",
                ["reports"] = "reports",
                ["pdf"] = "pdf",
                ["excel"] = "excel",
                ["audit"] = "audit",
                ["auditlog"] = "auditlog",
                ["activitylog"] = "activitylog",

                // Settings and preferences
                ["settings"] = "settings",
                ["config"] = "config",
                ["theme"] = "theme",

                // Status indicators
                ["success"] = "success",
                ["warning"] = "warning",
                ["error"] = "error",
                ["info"] = "info",

                // External integrations
                ["quickbooks"] = "quickbooks",
                ["jarvis"] = "jarvis",
                ["sync"] = "sync",
                ["warroom"] = "warroom",

                // Utilities
                ["calculator"] = "calculator",
                ["calendar"] = "calendar",
                ["email"] = "email",
                ["help"] = "help"  // Need to add helpflat.png
            };

            int index = 0;
            foreach (var (iconName, resourceBaseName) in iconResourceMappings)
            {
                // Try to load from embedded resources first
                Image? baseImage = LoadEmbeddedIcon(resourceBaseName, 16);
                if (baseImage == null)
                {
                    // Fallback to SystemIcons for missing resources
                    _logger.LogWarning("Embedded icon not found: {ResourceName}, using SystemIcon fallback for {IconName}", resourceBaseName, iconName);
                    baseImage = GetSystemIconFallback(iconName, 16);
                }

                // Add base DPI96 image (16x16) to Images collection
                _imageList.Images.Add(iconName, baseImage);

                // Create DPI variants by loading larger embedded resources or scaling
                Image? dpi120Image = LoadEmbeddedIcon(resourceBaseName, 20) ?? ScaleImage(baseImage, 20);
                Image? dpi144Image = LoadEmbeddedIcon(resourceBaseName, 24) ?? ScaleImage(baseImage, 24);
                Image? dpi192Image = LoadEmbeddedIcon(resourceBaseName, 32) ?? ScaleImage(baseImage, 32);

                // Create DPIAwareImage instance and configure for different DPI levels
                var dpiAwareImage = new DPIAwareImage
                {
                    Index = index,
                    DPI120Image = dpi120Image,
                    DPI144Image = dpi144Image,
                    DPI192Image = dpi192Image
                };
                _imageList.DPIImages.Add(dpiAwareImage);

                // Map name to index for lookup
                _iconNameToIndex[iconName] = index;
                index++;
            }

            _logger.LogInformation("DPI-aware image service: loaded {Count} icons with automatic scaling for 100%, 125%, 150%, 200% DPI", iconResourceMappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load icons for DPI-aware image service");
        }
    }

    /// <summary>
    /// Converts a system icon to a bitmap at the specified size.
    /// </summary>
    private static Bitmap GetIconBitmap(Icon sourceIcon, int size)
    {
        using var resizedIcon = new Icon(sourceIcon, size, size);
        return resizedIcon.ToBitmap();
    }

    /// <summary>
    /// Loads an icon from embedded resources at the specified size.
    /// </summary>
    /// <param name="resourceBaseName">Base name without "flat.png" suffix.</param>
    /// <param name="size">Target size (16, 20, 24, 32).</param>
    /// <returns>Bitmap if found, null otherwise.</returns>
    private static Image? LoadEmbeddedIcon(string resourceBaseName, int size)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = $"WileyWidget.WinForms.Resources.FlatIcons.{resourceBaseName}flat.png";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            var originalImage = Image.FromStream(stream);

            // If the image is already the target size, return it
            if (originalImage.Size == new Size(size, size))
            {
                return originalImage;
            }

            // Scale to target size with high quality
            var scaledImage = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(scaledImage))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(originalImage, 0, 0, size, size);
            }

            originalImage.Dispose();
            return scaledImage;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scales an image to the specified size.
    /// </summary>
    private static Image ScaleImage(Image sourceImage, int size)
    {
        var scaledImage = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(scaledImage))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(sourceImage, 0, 0, size, size);
        }
        return scaledImage;
    }

    /// <summary>
    /// Gets a system icon fallback for when embedded resources are missing.
    /// </summary>
    private static Image GetSystemIconFallback(string iconName, int size)
    {
        var fallbackIcon = iconName switch
        {
            "refresh" => SystemIcons.Exclamation,
            "pin" => SystemIcons.Hand,
            "pin_filled" => SystemIcons.Hand,
            "close" => SystemIcons.Error,
            "help" => SystemIcons.Question,
            "save" => SystemIcons.WinLogo,
            "open" => SystemIcons.Application,
            "export" => SystemIcons.Shield,
            "import" => SystemIcons.Information,
            "print" => SystemIcons.Question,
            "home" => SystemIcons.Application,
            "back" => SystemIcons.Hand,
            "forward" => SystemIcons.Asterisk,
            "add" => SystemIcons.Information,
            "edit" => SystemIcons.Question,
            "delete" => SystemIcons.Error,
            "search" => SystemIcons.Question,
            "filter" => SystemIcons.Shield,
            "dashboard" => SystemIcons.Application,
            "chart" => SystemIcons.Information,
            "charts" => SystemIcons.Information,
            "gauge" => SystemIcons.Shield,
            "kpi" => SystemIcons.Asterisk,
            "analytics" => SystemIcons.Information,
            "insights" => SystemIcons.Exclamation,
            "deptsummary" => SystemIcons.Information,
            "insightfeed" => SystemIcons.Exclamation,
            "accounts" => SystemIcons.Application,
            "budget" => SystemIcons.Information,
            "budgetoverview" => SystemIcons.Information,
            "customers" => SystemIcons.Application,
            "utilitybill" => SystemIcons.Application,
            "revenuetrends" => SystemIcons.Information,
            "recommendedcharge" => SystemIcons.Information,
            "report" => SystemIcons.Application,
            "reports" => SystemIcons.Application,
            "pdf" => SystemIcons.Shield,
            "excel" => SystemIcons.Information,
            "audit" => SystemIcons.Question,
            "auditlog" => SystemIcons.Question,
            "activitylog" => SystemIcons.Information,
            "settings" => SystemIcons.Shield,
            "config" => SystemIcons.Information,
            "theme" => SystemIcons.Asterisk,
            "success" => SystemIcons.Information,
            "warning" => SystemIcons.Warning,
            "error" => SystemIcons.Error,
            "info" => SystemIcons.Information,
            "quickbooks" => SystemIcons.Application,
            "jarvis" => SystemIcons.Question,
            "sync" => SystemIcons.Shield,
            "warroom" => SystemIcons.Exclamation,
            "calculator" => SystemIcons.Application,
            "calendar" => SystemIcons.Information,
            "email" => SystemIcons.Shield,
            _ => SystemIcons.Application
        };

        return GetIconBitmap(fallbackIcon, size);
    }

    /// <summary>
    /// Gets a DPI-scaled image by icon name.
    /// </summary>
    /// <param name="iconName">Name of the icon (e.g., "save", "open", "dashboard").</param>
    /// <returns>DPI-scaled bitmap, or null if icon not found.</returns>
    /// <remarks>
    /// The returned image is automatically scaled by ImageListAdv based on current monitor DPI.
    /// </remarks>
    public Image? GetImage(string iconName)
    {
        EnsureIconsLoaded();

        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        if (_iconNameToIndex.TryGetValue(iconName, out int index))
        {
            return _imageList.Images[index];
        }

        _logger.LogWarning("Icon not found: {IconName}", iconName);
        return null;
    }

    /// <summary>
    /// Gets a DPI-aware scaled image at a specific target size, selecting the best DPI variant.
    /// High-DPI optimization: Avoids blurring on 125%/150%/200% displays by selecting the appropriate
    /// pre-scaled variant from DPIImages collection instead of upscaling a small base image.
    /// </summary>
    /// <param name="iconName">Name of the icon (e.g., "dashboard", "accounts").</param>
    /// <param name="targetSize">Target size for the image (e.g., new Size(32, 32) for ribbon large buttons).</param>
    /// <returns>Optimally scaled bitmap for current display DPI, or null if icon not found.</returns>
    /// <remarks>
    /// DPI Selection Logic:
    /// - 96 DPI (100%): Returns 16x16 base image, scaled to targetSize if needed
    /// - 120 DPI (125%): Returns 20x20 DPI120Image if available
    /// - 144 DPI (150%): Returns 24x24 DPI144Image if available
    /// - 192 DPI (200%): Returns 32x32 DPI192Image if available
    ///
    /// This avoids the Syncfusion ribbon blurring issue where 32px icons upscaled from 16px sources
    /// appear fuzzy on non-100% displays.
    /// </remarks>
    public Image? GetScaledImage(string iconName, Size targetSize)
    {
        EnsureIconsLoaded();

        if (string.IsNullOrWhiteSpace(iconName) || targetSize.Width <= 0 || targetSize.Height <= 0)
        {
            return null;
        }

        if (!_iconNameToIndex.TryGetValue(iconName, out int index))
        {
            _logger.LogWarning("Icon not found for scaled retrieval: {IconName}", iconName);
            return null;
        }

        try
        {
            // Detect current display DPI (in pixels per inch)
            // Using a temporary bitmap to measure actual screen DPI
            int dpiX = 96;  // Default fallback
            try
            {
                using (var bmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(bmp))
                {
                    dpiX = (int)g.DpiX;  // Read actual screen DPI
                }
            }
            catch
            {
                // If DPI detection fails, use 96 (100%) as safe default
            }

            // Select the best-matched DPI image variant to minimize scaling artifacts
            Image? selectedImage = null;
            int selectedDpi = 96;

            if (dpiX >= 192)  // 200% scaling
            {
                if (_imageList.DPIImages.Count > index && _imageList.DPIImages[index]?.DPI192Image != null)
                {
                    selectedImage = _imageList.DPIImages[index].DPI192Image;
                    selectedDpi = 192;
                }
            }
            else if (dpiX >= 144)  // 150% scaling
            {
                if (_imageList.DPIImages.Count > index && _imageList.DPIImages[index]?.DPI144Image != null)
                {
                    selectedImage = _imageList.DPIImages[index].DPI144Image;
                    selectedDpi = 144;
                }
            }
            else if (dpiX >= 120)  // 125% scaling
            {
                if (_imageList.DPIImages.Count > index && _imageList.DPIImages[index]?.DPI120Image != null)
                {
                    selectedImage = _imageList.DPIImages[index].DPI120Image;
                    selectedDpi = 120;
                }
            }

            // Fallback to base image if no DPI variant is available
            selectedImage ??= _imageList.Images[index];

            // If target size matches selected image size, return as-is (no scaling needed)
            if (selectedImage != null && selectedImage.Size == targetSize)
            {
                _logger.LogDebug("Selected DPI {Dpi} image for {IconName} at native size {Size}", selectedDpi, iconName, targetSize);
                return selectedImage;
            }

            // Scale to target size if needed (minimal upscaling from pre-scaled variant)
            if (selectedImage != null)
            {
                var scaled = new Bitmap(selectedImage, targetSize);
                _logger.LogDebug("Scaled DPI {Dpi} image {IconName} from {SourceSize} to {TargetSize}", selectedDpi, iconName, selectedImage.Size, targetSize);
                return scaled;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get scaled image for icon {IconName} at size {TargetSize}", iconName, targetSize);
            return null;
        }
    }

    /// <summary>
    /// Gets the image index for an icon name.
    /// </summary>
    /// <param name="iconName">Name of the icon.</param>
    /// <returns>Image index, or -1 if not found.</returns>
    /// <remarks>
    /// Use this when assigning ImageIndex to toolbar buttons, tree nodes, etc.
    /// Example: button.ImageIndex = dpiImageService.GetImageIndex("save");
    /// </remarks>
    public int GetImageIndex(string iconName)
    {
        EnsureIconsLoaded();
        return _iconNameToIndex.TryGetValue(iconName, out int index) ? index : -1;
    }

    /// <summary>
    /// Checks if an icon exists by name.
    /// </summary>
    public bool HasIcon(string iconName)
    {
        return !string.IsNullOrWhiteSpace(iconName) && _iconNameToIndex.ContainsKey(iconName);
    }

    /// <summary>
    /// Gets all available icon names.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableIcons()
    {
        return _iconNameToIndex.Keys;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _imageList?.Dispose();
        _disposed = true;
    }
}
