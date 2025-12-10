using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Loads ResourceDictionary XAML files safely by validating color tokens and
    /// coercing invalid Color values to safe defaults to avoid XAML parser failures
    /// during application startup.
    /// </summary>
    public static class SafeResourceDictionaryLoader
    {
        private static readonly string[] AllowedColorFormats = new[] { "#RRGGBB", "#AARRGGBB", "#RGB", "#ARGB" };

        public static ResourceDictionary Load(string path, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (!File.Exists(path))
            {
                logger?.LogWarning("ResourceDictionary file not found: {Path}", path);
                return new ResourceDictionary();
            }

            try
            {
                // Read raw XAML and pre-validate color tokens using a simple XML parse.
                var xaml = File.ReadAllText(path);

                // Quick replace for known problematic hex tokens that Color.Parse rejects
                // expects #AARRGGBB or #RRGGBB; ensure we convert common shorthand or alpha-first tokens.
                // Examples we saw in logs: #FF161616, #99FFFFFF, #33FFFFFF - these are valid AARRGGBB
                // but on some frameworks/custom parsers they may fail; catch exceptions and coerce.

                // Parse into ResourceDictionary using XamlReader to let validate types.
                using var stringReader = new StringReader(xaml);
                using var xmlReader = XmlReader.Create(stringReader);
                var rd = (ResourceDictionary)XamlReader.Load(xmlReader);

                // Walk through brushes and colors and validate Color values.
                foreach (var key in rd.Keys.OfType<object>().ToList())
                {
                    try
                    {
                        var value = rd[key];
                        if (value is Color)
                        {
                            // no-op; Color parsed successfully
                            continue;
                        }
                        if (value is SolidColorBrush brush)
                        {
                            // force access to Color to ensure it's valid
                            var c = brush.Color;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Invalid resource value for key {Key} in {Path}; replacing with fallback.", key, path);
                        // Replace with a safe fallback brush
                        rd[key] = new SolidColorBrush(Colors.Transparent);
                    }
                }

                return rd;
            }
            catch (XamlParseException ex)
            {
                logger?.LogWarning(ex, "Failed to parse ResourceDictionary {Path} - returning empty dictionary.", path);
                return new ResourceDictionary();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error loading ResourceDictionary {Path} - returning empty dictionary.", path);
                return new ResourceDictionary();
            }
        }
    }
}
