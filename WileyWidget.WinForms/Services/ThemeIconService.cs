using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using Svg;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// A simple theme-aware icon service that loads SVG or PNG assets from output folder
/// at runtime and renders them into System.Drawing.Image instances.
///
/// Naming convention (Assets/Icons): {name}_light.svg, {name}_dark.svg
/// Will fall back to PNG if SVG is missing: {name}_light.png / {name}_dark.png
/// </summary>
public class ThemeIconService : IThemeIconService, IDisposable
{
    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, Image> _cache = new();

    public ThemeIconService()
    {
        // Assets are copied to the application's output folder at build-time
        _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");
    }

    public Image? GetIcon(string name, AppTheme theme, int size = 24)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var key = $"{name}|{theme}|{size}".ToLowerInvariant();
        if (_cache.TryGetValue(key, out var cached)) return (Image)cached.Clone();

        // Resolve filename
        var themeSuffix = theme == AppTheme.Dark ? "dark" : "light";
        var svgPath = Path.Combine(_basePath, $"{name}_{themeSuffix}.svg");
        var pngPath = Path.Combine(_basePath, $"{name}_{themeSuffix}.png");

        Image? img = null;

        try
        {
            if (File.Exists(svgPath))
            {
                var svgDoc = SvgDocument.Open(svgPath);
                img = svgDoc.Draw(size, size);
            }
            else if (File.Exists(pngPath))
            {
                img = Image.FromFile(pngPath);
            }
        }
        catch
        {
            // swallow and let fallback be null
            img = null;
        }

        if (img != null)
        {
            // store a clone in cache and return a clone
            _cache[key] = (Image)img.Clone();
            return img;
        }

        return null;
    }

    public void Preload(IEnumerable<string> names, AppTheme theme, int size = 24)
    {
        foreach (var name in names)
        {
            try
            {
                var _ = GetIcon(name, theme, size);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        foreach (var kv in _cache)
        {
            try { kv.Value.Dispose(); } catch { }
        }

        _cache.Clear();
    }
}
