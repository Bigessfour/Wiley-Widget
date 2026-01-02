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
        private bool _disposed;

        // Known icon names for validation
        private static readonly HashSet<string> KnownIconNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // File operations
            "save", "open", "load", "export", "import", "folder", "file", "document", "excel", "pdf",

            // Edit operations
            "add", "new", "plus", "delete", "remove", "minus", "edit", "pencil", "copy", "paste", "cut",

            // Navigation
            "home", "back", "forward", "up", "down", "left", "right", "refresh", "reload", "load",

            // UI controls
            "close", "exit", "minimize", "maximize", "restore", "settings", "options", "menu", "hamburger", "dismiss",

            // Information
            "help", "info", "warning", "error", "question", "notification", "bell",

            // Search and filter
            "search", "filter", "clear", "reset",

            // Business
            "dashboard", "chart", "report", "reports", "user", "profile", "chat", "ai", "assistant",
            "quickbooks", "accounting", "accounts", "finance", "customer", "customers", "invoice", "payment", "budget", "wallet",
            
            // Media and actions
            "play", "pause", "stop", "print", "printer"
        };

        public ThemeIconService(ILogger logger)
        {
            _logger = logger?.ForContext<ThemeIconService>() ?? throw new ArgumentNullException(nameof(logger));
            _iconCache = new ConcurrentDictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);

            _logger.Debug("ThemeIconService initialized with {IconCount} known icon names", KnownIconNames.Count);
        }

        /// <summary>
        /// Gets an icon with the specified name, theme, and size.
        /// Returns null if the icon cannot be found or generated.
        /// </summary>
        public Image? GetIcon(string name, AppTheme theme, int size)
        {
            if (_disposed)
            {
                _logger.Warning("GetIcon called on disposed ThemeIconService");
                return null;
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

            // Try to get from cache first
            if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            {
                _logger.Verbose("Icon {IconName} found in cache for theme {Theme}", name, theme);
                return cachedIcon;
            }

            _logger.Debug("Generating icon {IconName} for theme {Theme} with size {Size}", name, theme, size);

            // Generate icon based on theme
            Image? icon = theme switch
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
                    _logger.Information("Unknown icon name '{IconName}' requested. Known names: {KnownNames}",
                        name, string.Join(", ", KnownIconNames.OrderBy(n => n).Take(10)) + "...");
                }
            }

            // Cache the result (even if null to avoid repeated failed attempts)
            _iconCache.TryAdd(cacheKey, icon);

            return icon;
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

                sourceImage.Dispose();
                return bitmap;
            }
            catch
            {
                return sourceImage;
            }
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

                sourceImage.Dispose();
                return bitmap;
            }
            catch
            {
                return sourceImage;
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
