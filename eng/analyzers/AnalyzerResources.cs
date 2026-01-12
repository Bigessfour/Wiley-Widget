using System.Resources;

namespace WileyWidget.Analyzers
{
    /// <summary>
    /// Resource strings for diagnostic messages.
    /// Auto-generated support for localizable messages in analyzers.
    /// </summary>
    public class AnalyzerResources
    {
        public static ResourceManager ResourceManager { get; } = new ResourceManager(
            typeof(AnalyzerResources).FullName!,
            typeof(AnalyzerResources).Assembly);

        // WW0001 - Avoid Color.FromArgb usage
        public const string WW0001_Title = "Avoid Color.FromArgb usage";
        public const string WW0001_MessageFormat = "Use SfSkinManager or ThemeColors instead of Color.FromArgb to maintain theme consistency";
        public const string WW0001_Description = "Manual color assignments via Color.FromArgb can break theme cascade and prevent runtime theme switching. Use SfSkinManager.SetVisualStyle() or ThemeColors for consistent theming.";

        // WW0002 - MemoryCacheEntryOptions missing Size property
        public const string WW0002_Title = "MemoryCacheEntryOptions missing required Size property";
        public const string WW0002_MessageFormat = "MemoryCacheEntryOptions created without Size property. When SizeLimit is configured on MemoryCache, Size must be explicitly set (default: Size = 1)";
        public const string WW0002_Description = "When a MemoryCache instance has SizeLimit configured, all MemoryCacheEntryOptions objects must explicitly set the Size property. " +
            "Without Size set, the entry may be rejected if the sum of cache entry sizes exceeds SizeLimit. " +
            "Per Microsoft: 'An entry won't be cached if the sum of the cached entry sizes exceeds the value specified by SizeLimit. " +
            "If no cache size limit is set, the cache size set on the entry is ignored.' " +
            "Reference: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size";
    }
}
