using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Serilog;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Icon service that provides themed icons for UI controls with caching and robust error handling.
    /// Currently uses system icons as placeholders until custom assets are added.
    /// </summary>
    public sealed class ThemeIconService : IThemeIconService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Image?> _iconCache;
        private readonly object _disposeLock = new();
        private readonly object _cacheLock = new();
        private bool _disposed;

        public bool IsDisposed => _disposed;

        // Known icon names for validation
        private static readonly HashSet<string> KnownIconNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // File operations
            "save",
            "open",
            "load",
            "export",
            "import",
            "folder",
            "file",
            "document",
            "excel",
            "pdf",

            // Edit operations
            "add",
            "new",
            "plus",
            "delete",
            "remove",
            "minus",
            "edit",
            "pencil",
            "copy",
            "paste",
            "cut",

            // Navigation
            "home",
            "back",
            "forward",
            "up",
            "down",
            "left",
            "right",
            "refresh",
            "reload",
            "load",

            // UI controls
            "close",
            "exit",
            "minimize",
            "maximize",
            "restore",
            "settings",
            "options",
            "menu",
            "hamburger",
            "dismiss",

            // Information
            "help",
            "info",
            "warning",
            "error",
            "question",
            "notification",
            "bell",

            // Search and filter
            "search",
            "filter",
            "clear",
            "reset",

            // Business
            "dashboard",
            "chart",
            "report",
            "reports",
            "user",
            "profile",
            "chat",
            "ai",
            "assistant",
            "quickbooks",
            "accounting",
            "accounts",
            "finance",
            "customer",
            "customers",
            "invoice",
            "payment",
            "budget",
            "wallet",

            // Media and actions
            "play",
            "pause",
            "stop",
            "print",
            "printer"
        };

        public ThemeIconService(ILogger logger)
        {
            _logger = logger?.ForContext<ThemeIconService>() ?? throw new ArgumentNullException(nameof(logger));
            _iconCache = new ConcurrentDictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);

            _logger.Debug("ThemeIconService initialized with {IconCount} known icon names", KnownIconNames.Count);
        }

        /// <summary>
        /// Creates an emergency fallback icon when the service is disposed or icons cannot be loaded.
        /// Returns a simple text-based bitmap with the first letter of the icon name.
        /// </summary>
        private static Image CreateEmergencyFallbackIcon(string name, int size)
        {
            try
            {
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);


                    // Use a fallback color since ThemeColors is unavailable
                    Color bgColor = Color.LightGray;

                    using (var brush = new SolidBrush(bgColor))
                    {
                        g.FillEllipse(brush, 2, 2, size - 4, size - 4);
                    }

                    // Draw first letter or "?" if name is empty
                    var text = !string.IsNullOrWhiteSpace(name) ? name[0].ToString().ToUpperInvariant() : "?";
                    using var font = new Font("Segoe UI", size * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                    // Use black as the fallback text color since ThemeColors is unavailable
                    Color themedTextColor = Color.Black;
                    using var textBrush = new SolidBrush(themedTextColor);

                    var stringFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    var rect = new RectangleF(0, 0, size, size);
                    g.DrawString(text, font, textBrush, rect, stringFormat);
                }
                return bmp;
            }
            catch
            {
                // Last resort - return a simple colored bitmap
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.LightGray);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Gets an icon with the specified name, theme, and size.
        /// Returns an emergency fallback icon if the service is disposed.
        /// DEFENSIVE: During shutdown, the DI container may dispose singletons before
        /// UI cleanup completes, causing GetIcon to be called after disposal.
        /// </summary>
        public Image? GetIcon(string name, AppTheme theme, int size, bool disabled = false)
        {
            // Guard against shutdown race conditions where UI code calls GetIcon after disposal
            if (_disposed)
            {
                // Don't log on disposed service - use Debug output only to avoid Serilog issues
                System.Diagnostics.Debug.WriteLine($"[WARNING] GetIcon called on disposed ThemeIconService for '{name}' - returning emergency fallback");
                return CreateEmergencyFallbackIcon(name ?? "unknown", size);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.Warning("GetIcon called with null or empty icon name");
                return null;
            }

            if (size <= 0 || size > 256)
            {
                _logger.Warning("GetIcon called with invalid size {Size}, clamping to 16-256 range", size);
                size = Math.Clamp(size, 16, 256);
            }

            var cacheKey = GetCacheKey(name, theme, size);

            Image? icon = null;

            // Try to get from cache first with thread-safe locking
            if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            {
                if (cachedIcon == null)
                {
                    _logger.Verbose("Icon {IconName} found in cache as null for theme {Theme}", name, theme);
                    return null;
                }

                lock (_cacheLock)
                {
                    try
                    {
                        // Validate cached image before attempting to clone
                        // This prevents "Parameter is not valid" exceptions in ImageAnimator
                        _ = cachedIcon.Width;
                        _ = cachedIcon.Height;
                        _ = cachedIcon.PixelFormat;

                        // Create a new Bitmap to ensure validity
                        var newBitmap = new Bitmap(cachedIcon.Width, cachedIcon.Height);
                        using (var g = Graphics.FromImage(newBitmap))
                        {
                            g.DrawImage(cachedIcon, 0, 0);
                        }

                        // Validate the new bitmap
                        if (newBitmap.Width <= 0 || newBitmap.Height <= 0)
                        {
                            _logger.Warning("Invalid cached image for {IconName}", name);
                            newBitmap.Dispose();
                            // Remove corrupt entry from cache
                            _iconCache.TryRemove(cacheKey, out _);
                            return null;
                        }

                        _logger.Verbose("Icon {IconName} retrieved and validated from cache for theme {Theme}", name, theme);
                        icon = newBitmap;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Error validating cached image for {IconName} - removing from cache", name);
                        // Remove corrupt entry to force regeneration next time
                        _iconCache.TryRemove(cacheKey, out _);
                        return null;
                    }
                }
            }
            else
            {
                _logger.Debug("Generating icon {IconName} for theme {Theme} with size {Size}", name, theme, size);

                // Generate icon based on theme
                icon = theme switch
                {
                    AppTheme.Office2019Colorful => GetOffice2019ColorfulIcon(name, size),
                    AppTheme.Office2019Dark => GetOffice2019DarkIcon(name, size),
                    AppTheme.Office2019Black => GetOffice2019DarkIcon(name, size),
                    AppTheme.Office2019DarkGray => GetOffice2019DarkIcon(name, size),
                    AppTheme.Office2019White => GetOffice2019ColorfulIcon(name, size),
                    AppTheme.Dark => GetDarkThemeIcon(name, size),
                    AppTheme.Light => GetLightThemeIcon(name, size),
                    AppTheme.HighContrastBlack => GetHighContrastIcon(name, size),
                    _ => GetFallbackIcon(name, size)
                };

                if (icon == null)
                {
                    _logger.Warning("Icon {IconName} could not be generated for theme {Theme}", name, theme);

                    // Check if this is a known icon name
                    if (!KnownIconNames.Contains(name))
                    {
                        _logger.Debug("Unknown icon name '{IconName}' requested. Known names: {KnownNames}",
                            name, string.Join(", ", KnownIconNames.OrderBy(n => n).Take(10)) + "...");
                    }
                }
                else
                {
                    // DEFENSIVE: Convert any animated images to static bitmaps before caching
                    // This prevents ImageAnimator exceptions when images are used in Syncfusion controls
                    if (ImageAnimator.CanAnimate(icon))
                    {
                        var staticBitmap = ConvertToStaticBitmap(icon);
                        if (staticBitmap != null)
                        {
                            icon.Dispose(); // Dispose the animated image
                            icon = staticBitmap;
                            _logger.Debug("Converted animated image to static bitmap for {IconName}", name);
                        }
                    }

                    // Validate the generated image
                    try
                    {
                        if (icon.Width <= 0 || icon.Height <= 0)
                        {
                            _logger.Warning("Invalid image generated for {IconName}", name);
                            icon.Dispose();
                            icon = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Error validating image for {IconName}", name);
                        icon.Dispose();
                        icon = null;
                    }
                }
            }

            // Cache the result (even if null to avoid repeated failed attempts)
            // CRITICAL: Cache the ORIGINAL/GENERATED icon, but always return a CLONE to prevent shared disposal
            if (icon != null)
            {
                // Add to cache if this was a newly generated icon (not from cache)
                if (!_iconCache.ContainsKey(cacheKey))
                {
                    _iconCache.TryAdd(cacheKey, icon);
                }

                // Always return a CLONE to prevent shared disposal issues
                var clonedIcon = CloneImage(icon);

                if (disabled)
                {
                    if (clonedIcon != null)
                    {
                        var disabledIcon = ApplyDisabledEffect(clonedIcon);
                        clonedIcon.Dispose(); // Dispose the non-disabled clone
                        return disabledIcon;
                    }
                    else
                    {
                        return null;
                    }
                }

                return clonedIcon;
            }
            else
            {
                _iconCache.TryAdd(cacheKey, null);
                return null;
            }
        }

        /// <summary>
        /// Gets an icon asynchronously with the specified name, theme, and size.
        /// Returns an emergency fallback icon if the service is disposed.
        /// DEFENSIVE: During shutdown, the DI container may dispose singletons before
        /// UI cleanup completes, causing GetIconAsync to be called after disposal.
        /// </summary>
        public Task<Image?> GetIconAsync(string name, AppTheme theme, int size, bool disabled = false)
        {
            // Guard against shutdown race conditions where UI code calls GetIconAsync after disposal
            if (_disposed)
            {
                // Don't log on disposed service - use Debug output only to avoid Serilog issues
                System.Diagnostics.Debug.WriteLine($"[WARNING] GetIconAsync called on disposed ThemeIconService for '{name}' - returning emergency fallback");
                return Task.FromResult<Image?>(CreateEmergencyFallbackIcon(name ?? "unknown", size));
            }

            return Task.FromResult(GetIcon(name, theme, size, disabled));
        }

        /// <summary>
        /// Applies a disabled effect to an icon (grayscale with reduced opacity).
        /// </summary>
        private Image? ApplyDisabledEffect(Image source)
        {
            if (source == null) return null;

            try
            {
                var disabledBitmap = new Bitmap(source.Width, source.Height);
                using (var g = Graphics.FromImage(disabledBitmap))
                {
                    // Create color matrix for grayscale + 50% opacity
                    var colorMatrix = new ColorMatrix
                    {
                        Matrix00 = 0.299f, // Red to grayscale
                        Matrix01 = 0.299f,
                        Matrix02 = 0.299f,
                        Matrix10 = 0.587f, // Green
                        Matrix11 = 0.587f,
                        Matrix12 = 0.587f,
                        Matrix20 = 0.114f, // Blue
                        Matrix21 = 0.114f,
                        Matrix22 = 0.114f,
                        Matrix33 = 0.5f    // 50% opacity
                    };

                    var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(colorMatrix);

                    g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                }

                return disabledBitmap;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to apply disabled effect to icon");
                return source; // Return original if effect fails
            }
        }

        /// <summary>
        /// Checks if an icon name is recognized by the service.
        /// </summary>
        public bool IsKnownIcon(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && KnownIconNames.Contains(name);
        }

        /// <summary>
        /// Gets all known icon names.
        /// </summary>
        public IReadOnlyCollection<string> GetKnownIconNames()
        {
            return KnownIconNames.OrderBy(n => n).ToList();
        }

        /// <summary>
        /// Clears the icon cache.
        /// </summary>
        public void ClearCache()
        {
            _logger.Information("Clearing icon cache with {Count} entries", _iconCache.Count);

            // Dispose all cached images
            foreach (var kvp in _iconCache)
            {
                kvp.Value?.Dispose();
            }

            _iconCache.Clear();
        }

        private static string GetCacheKey(string name, AppTheme theme, int size)
        {
            return $"{name.ToLowerInvariant()}_{theme}_{size}";
        }

        private Image? GetOffice2019ColorfulIcon(string name, int size)
        {
            return GetSystemIconImage(name, size);
        }

        private Image? GetOffice2019DarkIcon(string name, int size)
        {
            var baseIcon = GetSystemIconImage(name, size);
            return baseIcon != null ? ApplyDarkThemeFilter(baseIcon) : null;
        }

        private Image? GetDarkThemeIcon(string name, int size)
        {
            var baseIcon = GetSystemIconImage(name, size);
            return baseIcon != null ? ApplyDarkThemeFilter(baseIcon) : null;
        }

        private Image? GetLightThemeIcon(string name, int size)
        {
            return GetSystemIconImage(name, size);
        }

        private Image? GetHighContrastIcon(string name, int size)
        {
            var baseIcon = GetSystemIconImage(name, size);
            return baseIcon != null ? ApplyHighContrastFilter(baseIcon) : null;
        }

        private Image? GetFallbackIcon(string name, int size)
        {
            _logger.Debug("Using fallback icon for {IconName}", name);
            return GetSystemIconImage(name, size);
        }

        private Image? GetSystemIconImage(string name, int size)
        {
            try
            {
                Icon? systemIcon = name.ToLowerInvariant() switch
                {
                    // Settings and configuration
                    "settings" or "options" or "configure" => SystemIcons.Application,

                    // Information and help
                    "help" or "info" or "information" => SystemIcons.Information,
                    "question" => SystemIcons.Question,
                    "warning" or "alert" => SystemIcons.Warning,
                    "error" or "critical" => SystemIcons.Error,

                    // Window controls
                    "close" or "exit" or "quit" => SystemIcons.Error,
                    "minimize" => SystemIcons.Warning,
                    "maximize" or "restore" => SystemIcons.Question,

                    // File operations
                    "save" or "disk" => SystemIcons.Shield,
                    "open" or "folder" or "load" or "file" => SystemIcons.Shield,
                    "export" or "download" => SystemIcons.Shield,
                    "import" or "upload" => SystemIcons.Shield,
                    "document" or "report" or "reports" => SystemIcons.Application,
                    "excel" or "xls" or "xlsx" or "spreadsheet" => SystemIcons.Application,
                    "pdf" => SystemIcons.Application,

                    // Edit operations
                    "refresh" or "reload" or "sync" => SystemIcons.Shield,
                    "search" or "find" or "filter" => SystemIcons.Shield,
                    "add" or "plus" or "new" or "create" => SystemIcons.Shield,
                    "delete" or "remove" or "minus" or "trash" => SystemIcons.Shield,
                    "edit" or "pencil" or "modify" => SystemIcons.Shield,
                    "copy" or "duplicate" => SystemIcons.Shield,
                    "paste" or "insert" => SystemIcons.Shield,
                    "cut" => SystemIcons.Shield,
                    "clear" or "reset" or "undo" => SystemIcons.Shield,
                    "dismiss" or "cancel" => SystemIcons.Shield,

                    // Navigation
                    "home" or "dashboard" => SystemIcons.Application,
                    "back" or "left" or "previous" => SystemIcons.Shield,
                    "forward" or "right" or "next" => SystemIcons.Shield,
                    "up" => SystemIcons.Shield,
                    "down" => SystemIcons.Shield,

                    // Business and data
                    "chart" or "graph" or "analytics" => SystemIcons.Application,
                    "user" or "profile" or "account" or "customer" or "customers" => SystemIcons.Application,
                    "notification" or "bell" or "alert" => SystemIcons.Application,
                    "menu" or "hamburger" or "options" => SystemIcons.Application,
                    "chat" or "message" or "ai" or "assistant" => SystemIcons.Information,
                    "quickbooks" or "accounting" or "accounts" or "finance" or "money" or "budget" => SystemIcons.Shield,
                    "invoice" or "bill" or "payment" => SystemIcons.Shield,
                    "wallet" or "purse" or "funds" => SystemIcons.Shield,

                    // Media and printing
                    "play" or "start" or "run" => SystemIcons.Application,
                    "pause" or "stop" => SystemIcons.Shield,
                    "print" or "printer" => SystemIcons.Application,

                    // Fallback for unknown icons
                    _ => null
                };

                if (systemIcon == null)
                {
                    return null;
                }

                // Convert icon to bitmap
                using var bitmap = systemIcon.ToBitmap();

                // Create a high-quality resized version
                return ResizeImage(bitmap, size, size);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating system icon for {IconName} with size {Size}", name, size);
                return null;
            }
        }

        /// <summary>
        /// Resizes an image with high quality settings.
        /// </summary>
        private static Image? ResizeImage(Image? sourceImage, int width, int height)
        {
            if (sourceImage == null)
            {
                return null;
            }

            try
            {
                var destRect = new Rectangle(0, 0, width, height);
                var destImage = new Bitmap(width, height);

                destImage.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using var wrapMode = new ImageAttributes();
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(sourceImage, destRect, 0, 0, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, wrapMode);
                }

                return destImage;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Applies a dark theme filter to an image (inverts colors).
        /// </summary>
        private static Image? ApplyDarkThemeFilter(Image? sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }

            Image? result = null;
            try
            {
                var bitmap = new Bitmap(sourceImage.Width, sourceImage.Height);

                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Create a color matrix that inverts colors
                    var colorMatrix = new ColorMatrix(new float[][]
                    {
                        new float[] {-1, 0, 0, 0, 0},
                        new float[] {0, -1, 0, 0, 0},
                        new float[] {0, 0, -1, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {1, 1, 1, 0, 1}
                    });

                    using var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(colorMatrix);

                    graphics.DrawImage(sourceImage,
                        new Rectangle(0, 0, sourceImage.Width, sourceImage.Height),
                        0, 0, sourceImage.Width, sourceImage.Height,
                        GraphicsUnit.Pixel, attributes);
                }

                result = bitmap;
            }
            catch
            {
                // result remains null
            }
            finally
            {
                sourceImage.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Applies a high contrast filter to an image (converts to grayscale with enhanced contrast).
        /// </summary>
        private static Image? ApplyHighContrastFilter(Image? sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }

            Image? result = null;
            try
            {
                var bitmap = new Bitmap(sourceImage.Width, sourceImage.Height);

                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Create a grayscale color matrix
                    var colorMatrix = new ColorMatrix(new float[][]
                    {
                        new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                        new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                        new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                    using var attributes = new ImageAttributes();
                    attributes.SetColorMatrix(colorMatrix);

                    graphics.DrawImage(sourceImage,
                        new Rectangle(0, 0, sourceImage.Width, sourceImage.Height),
                        0, 0, sourceImage.Width, sourceImage.Height,
                        GraphicsUnit.Pixel, attributes);
                }

                result = bitmap;
            }
            catch
            {
                // result remains null
            }
            finally
            {
                sourceImage.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Converts an animated image to a static bitmap by drawing the first frame.
        /// </summary>
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
            catch (Exception ex)
            {
                // If conversion fails, dispose the animated image and return null to prevent ImageAnimator exceptions
                System.Diagnostics.Debug.WriteLine($"Failed to convert animated image to static bitmap: {ex.Message}");
                try
                {
                    animatedImage.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                return null;
            }
        }

        /// <summary>
        /// Clones an image to create a completely independent copy.
        /// This prevents shared disposal issues when images are used across multiple controls.
        /// </summary>
        private static Image? CloneImage(Image? sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }

            try
            {
                // Validate source before cloning
                _ = sourceImage.Width;
                _ = sourceImage.Height;
                _ = sourceImage.PixelFormat;

                // Create a new bitmap with the same dimensions
                var clonedBitmap = new Bitmap(sourceImage.Width, sourceImage.Height, sourceImage.PixelFormat);

                // Copy the image data
                using (var g = Graphics.FromImage(clonedBitmap))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(sourceImage, 0, 0, sourceImage.Width, sourceImage.Height);
                }

                return clonedBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clone image: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _logger.Debug("Disposing ThemeIconService");

                // Dispose all cached images
                foreach (var kvp in _iconCache)
                {
                    try
                    {
                        kvp.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Error disposing cached icon {IconKey}", kvp.Key);
                    }
                }

                _iconCache.Clear();
                _disposed = true;
            }
        }
    }
}
