using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;

namespace SyncfusionMcpServer.Services;

/// <summary>
/// Service for validating Syncfusion theme configuration
/// </summary>
public class ThemeValidationService
{
    private readonly ILogger<ThemeValidationService> _logger;
    private static readonly Dictionary<string, string> KnownThemes = new()
    {
        ["FluentDark"] = "Syncfusion.Themes.FluentDark.WinUI",
        ["FluentLight"] = "Syncfusion.Themes.FluentLight.WinUI",
        ["MaterialDark"] = "Syncfusion.Themes.MaterialDark.WinUI",
        ["MaterialLight"] = "Syncfusion.Themes.MaterialLight.WinUI",
        ["Office2019Black"] = "Syncfusion.Themes.Office2019Black.WinUI",
        ["Office2019Colorful"] = "Syncfusion.Themes.Office2019Colorful.WinUI",
        ["Office2019White"] = "Syncfusion.Themes.Office2019White.WinUI"
    };

    public ThemeValidationService(ILogger<ThemeValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<ThemeValidationResult> ValidateThemeAsync(string themeName, string? targetAssembly, string? appXamlPath)
    {
        var result = new ThemeValidationResult
        {
            IsValid = true,
            ThemeName = themeName
        };

        try
        {
            // Check if theme is known
            if (KnownThemes.ContainsKey(themeName))
            {
                result.TargetAssembly = targetAssembly ?? KnownThemes[themeName];
                _logger.LogInformation("Validating known theme: {Theme}", themeName);
            }
            else
            {
                result.Warnings.Add($"Unknown theme name: {themeName}. This may be a custom theme.");
            }

            // Check App.xaml.cs for theme registration
            if (!string.IsNullOrEmpty(appXamlPath) && File.Exists(appXamlPath))
            {
                var appContent = await File.ReadAllTextAsync(appXamlPath);
                result.ThemesRegistered = await AnalyzeThemeRegistrationAsync(appContent);

                if (!result.ThemesRegistered.Contains(themeName, StringComparer.OrdinalIgnoreCase))
                {
                    result.Warnings.Add($"Theme '{themeName}' not found in SfSkinManager registration");
                    result.AppliedSuccessfully = false;
                }
                else
                {
                    result.AppliedSuccessfully = true;
                }
            }

            // Check for theme resource dictionaries
            if (!string.IsNullOrEmpty(appXamlPath))
            {
                var appDir = Path.GetDirectoryName(appXamlPath);
                if (appDir != null)
                {
                    var resourceDictionaries = await FindThemeResourcesAsync(appDir, themeName);
                    if (resourceDictionaries.Count == 0)
                    {
                        result.MissingResources.Add($"No resource dictionaries found for theme: {themeName}");
                    }
                }
            }

            _logger.LogInformation("Theme validation complete: {Theme}, Success: {Success}",
                themeName, result.AppliedSuccessfully);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating theme: {Theme}", themeName);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    private async Task<List<string>> AnalyzeThemeRegistrationAsync(string appContent)
    {
        var themes = new List<string>();

        // Look for SfSkinManager.SetTheme patterns
        var themePattern = System.Text.RegularExpressions.Regex.Matches(
            appContent,
            @"SfSkinManager\.SetTheme.*?new\s+(\w+Theme)\(\)",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in themePattern)
        {
            var themeClass = match.Groups[1].Value;
            // Convert FluentDarkTheme -> FluentDark
            var themeName = themeClass.Replace("Theme", "");
            themes.Add(themeName);
        }

        // Also look for direct theme name references
        foreach (var knownTheme in KnownThemes.Keys)
        {
            if (appContent.Contains(knownTheme, StringComparison.OrdinalIgnoreCase))
            {
                if (!themes.Contains(knownTheme, StringComparer.OrdinalIgnoreCase))
                {
                    themes.Add(knownTheme);
                }
            }
        }

        await Task.CompletedTask;
        return themes;
    }

    private async Task<List<string>> FindThemeResourcesAsync(string appDir, string themeName)
    {
        var resources = new List<string>();

        try
        {
            // Look for theme XAML files
            var xamlFiles = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories)
                .Where(f => f.Contains(themeName, StringComparison.OrdinalIgnoreCase) ||
                           f.Contains("Theme", StringComparison.OrdinalIgnoreCase))
                .ToList();

            resources.AddRange(xamlFiles);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for theme resources in: {Dir}", appDir);
        }

        await Task.CompletedTask;
        return resources;
    }

    public List<string> GetKnownThemes()
    {
        return KnownThemes.Keys.ToList();
    }

    public string? GetThemeAssembly(string themeName)
    {
        return KnownThemes.GetValueOrDefault(themeName);
    }
}
