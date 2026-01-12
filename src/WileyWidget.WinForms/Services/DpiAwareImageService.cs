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

        LoadIconsAsync();
    }

    /// <summary>
    /// Loads all application icons into the ImageListAdv with multiple DPI variants.
    /// </summary>
    /// <remarks>
    /// For production: Load from embedded resources or theme-specific icon files.
    /// Current implementation uses system icons as placeholders.
    /// 
    /// DPI Mapping:
    /// - DPI96: 16x16 (100% scaling, default)
    /// - DPI120: 20x20 (125% scaling)
    /// - DPI144: 24x24 (150% scaling)
    /// - DPI192: 32x32 (200% scaling)
    /// </remarks>
    private void LoadIconsAsync()
    {
        try
        {
            // Define all known icons with their system icon fallbacks
            var iconDefinitions = new Dictionary<string, Icon>
            {
                // File operations
                ["save"] = SystemIcons.WinLogo,
                ["open"] = SystemIcons.Application,
                ["export"] = SystemIcons.Shield,
                ["import"] = SystemIcons.Information,
                ["print"] = SystemIcons.Question,

                // Navigation
                ["home"] = SystemIcons.Application,
                ["back"] = SystemIcons.Hand,
                ["forward"] = SystemIcons.Asterisk,
                ["refresh"] = SystemIcons.Exclamation,

                // Data operations
                ["add"] = SystemIcons.Information,
                ["edit"] = SystemIcons.Question,
                ["delete"] = SystemIcons.Error,
                ["search"] = SystemIcons.Question,
                ["filter"] = SystemIcons.Shield,

                // Dashboard
                ["dashboard"] = SystemIcons.Application,
                ["chart"] = SystemIcons.Information,
                ["gauge"] = SystemIcons.Shield,
                ["kpi"] = SystemIcons.Asterisk,

                // Reports
                ["report"] = SystemIcons.Application,
                ["pdf"] = SystemIcons.Shield,
                ["excel"] = SystemIcons.Information,

                // Settings
                ["settings"] = SystemIcons.Shield,
                ["config"] = SystemIcons.Information,
                ["theme"] = SystemIcons.Asterisk,

                // Status
                ["success"] = SystemIcons.Information,
                ["warning"] = SystemIcons.Warning,
                ["error"] = SystemIcons.Error,
                ["info"] = SystemIcons.Information,

                // QuickBooks
                ["quickbooks"] = SystemIcons.Application,
                ["sync"] = SystemIcons.Shield,

                // Utilities
                ["calculator"] = SystemIcons.Application,
                ["calendar"] = SystemIcons.Information,
                ["email"] = SystemIcons.Shield,
                ["help"] = SystemIcons.Question
            };

            int index = 0;
            foreach (var (iconName, fallbackIcon) in iconDefinitions)
            {
                // Add base DPI96 image (16x16) to Images collection
                var icon96 = GetIconBitmap(fallbackIcon, 16);
                _imageList.Images.Add(iconName, icon96);

                // Create DPIAwareImage instance and configure for different DPI levels
                var dpiAwareImage = new DPIAwareImage
                {
                    Index = index, // Map to the Images collection index
                    DPI120Image = GetIconBitmap(fallbackIcon, 20),  // 125%
                    DPI144Image = GetIconBitmap(fallbackIcon, 24),  // 150%
                    DPI192Image = GetIconBitmap(fallbackIcon, 32)   // 200%
                };
                _imageList.DPIImages.Add(dpiAwareImage);

                // Map name to index for lookup
                _iconNameToIndex[iconName] = index;
                index++;
            }

            _logger.LogInformation("Loaded {Count} icons with multi-DPI support", iconDefinitions.Count);
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
    /// Gets a DPI-scaled image by icon name.
    /// </summary>
    /// <param name="iconName">Name of the icon (e.g., "save", "open", "dashboard").</param>
    /// <returns>DPI-scaled bitmap, or null if icon not found.</returns>
    /// <remarks>
    /// The returned image is automatically scaled by ImageListAdv based on current monitor DPI.
    /// </remarks>
    public Image? GetImage(string iconName)
    {
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
