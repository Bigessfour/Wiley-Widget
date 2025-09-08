using System;
using System.Collections.Generic;
using System.Windows;

namespace WileyWidget.UI.Theming;

/// <summary>
/// Lazy resource dictionary loader to defer heavy XAML until actually needed.
/// </summary>
public static class ResourceLoader
{
    private static readonly HashSet<string> Loaded = new();

    /// <summary>
    /// Ensure a resource dictionary (by relative pack URI) is merged exactly once.
    /// </summary>
    /// <param name="relativeSource">Relative URI (e.g., "Resources/Features/Dashboard.Heavy.xaml").</param>
    public static void Ensure(string relativeSource)
    {
        if (string.IsNullOrWhiteSpace(relativeSource)) return;
        if (Loaded.Contains(relativeSource)) return;
        var uri = new Uri(relativeSource, UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };
        Application.Current.Resources.MergedDictionaries.Add(dict);
        Loaded.Add(relativeSource);
    }

    /// <summary>
    /// Preload several dictionaries (batch add keeps invalidations minimal).
    /// </summary>
    public static void PreloadDeferred(params string[] relativeSources)
    {
        if (relativeSources == null) return;
        foreach (var src in relativeSources)
            Ensure(src);
    }
}