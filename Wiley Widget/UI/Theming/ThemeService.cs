using System;
using System.Linq;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Infrastructure.Logging;
using System.Reflection;

namespace WileyWidget.UI.Theming;

// Centralized service for applying and managing Syncfusion + WPF Fluent themes.
// Refactored to use consistent theming strategy and proper resource management.
public static class ThemeService
{
    // Supported theme names for Syncfusion WPF 30.2.4 (see Syncfusion docs: themes/getting-started)
    private static readonly string[] SupportedThemes =
    [
        "FluentDark",
        "FluentLight",
        "MaterialDark",
        "MaterialLight",
        "Office2019Colorful",
        "Office365",
        "HighContrast"
    ];

    private static string _currentTheme = "FluentDark";
    private static bool _isInitialized;
    private static bool _fallbackNotificationShown;
    private static bool _fallbackTelemetrySent;
    private static int _fallbackAttemptCount;
    private const int MaxFallbackAttempts = 3;

    // Event raised when the application theme changes
    public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    // Gets the currently applied theme name
    public static string CurrentTheme => _currentTheme;

    // Gets whether the theme system has been initialized
    public static bool IsInitialized => _isInitialized;

    // Initialize the theme system with global settings
    public static void Initialize()
    {
        if (_isInitialized)
        {
            Log.Warning("ThemeService already initialized");
            return;
        }

        try
        {
            Log.Information("🎨 Initializing ThemeService...");

            // Set global theme application mode - Syncfusion documented approach
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            Log.Information("✅ SfSkinManager.ApplyThemeAsDefaultStyle enabled");

            _isInitialized = true;
            Log.Information("✅ ThemeService initialized successfully");
            LogThemeAssemblyDiagnostics();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ ThemeService initialization failed");
            throw;
        }
    }

    // Apply a theme to the entire application using Syncfusion documented approach
    public static void ApplyApplicationTheme(string themeName)
    {
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        Log.Information("🎨 Applying Syncfusion application theme: {Theme}", theme);

        try
        {
            // Store previous theme for event
            var previousTheme = _currentTheme;

            // CRITICAL: Syncfusion WPF 30.2.4 documented approach
            // ONLY use SfSkinManager - DO NOT manually load WPF resource dictionaries
            // https://help.syncfusion.com/wpf/themes/getting-started#apply-theme-to-application
            SfSkinManager.ApplicationTheme = new Theme(theme);

            _currentTheme = theme;
            StructuredLogger.LogThemeChange(previousTheme, theme, false);

            // Raise theme changed event
            OnThemeChanged(new ThemeChangedEventArgs(previousTheme, theme, false));

            Log.Information("✅ Syncfusion application theme applied successfully: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to apply Syncfusion application theme: {Theme}", theme);
            // Basic fallback: try FluentLight then default WPF if original not FluentLight
        if (!string.Equals(theme, "FluentLight", StringComparison.OrdinalIgnoreCase) && CanAttemptFallback())
            {
                try
                {
                    Log.Warning("Fallback: attempting FluentLight theme after failure applying {Theme}", theme);
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                    _currentTheme = "FluentLight";
                    StructuredLogger.LogThemeChange(theme, "FluentLight", false);
                    OnThemeChanged(new ThemeChangedEventArgs(theme, "FluentLight", false));
                    Log.Information("✅ Fallback FluentLight theme applied successfully");
            RecordFallback(theme, ex);
                    return; // swallow original after successful fallback
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "Fallback FluentLight theme also failed; continuing without Syncfusion theme");
            RecordFallback(theme, ex, fallbackEx, success:false);
                }
            }
            throw; // rethrow if fallback unsuccessful
        }
    }

    // Apply a theme to a specific window
    public static void ApplyWindowTheme(Window window, string themeName)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        Log.Information("🎨 Applying window theme: {Theme} to {Window}", theme, window.GetType().Name);

        try
        {
            // Apply per-window Syncfusion theme
#pragma warning disable CA2000
            SfSkinManager.SetTheme(window, new Theme(theme));
#pragma warning restore CA2000

            Log.Information("✅ Window theme applied successfully: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to apply window theme: {Theme} to {Window}", theme, window.GetType().Name);
            throw;
        }
    }

    // Apply theme to both application and specific window
    public static void ApplyCompleteTheme(Window window, string themeName)
    {
        ApplyApplicationTheme(themeName);
        ApplyWindowTheme(window, themeName);
    }

    // Change theme initiated by user action (e.g., from UI)
    public static void ChangeTheme(string themeName, bool userInitiated = true)
    {
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        var previousTheme = _currentTheme;

        if (string.Equals(previousTheme, theme, StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("Theme change requested but already applied: {Theme}", theme);
            return;
        }

        Log.Information("🎨 User-initiated theme change: {FromTheme} → {ToTheme}", previousTheme, theme);

        try
        {
            // Apply Syncfusion theme globally - documented approach
            SfSkinManager.ApplicationTheme = new Theme(theme);

            _currentTheme = theme;
            StructuredLogger.LogThemeChange(previousTheme, theme, userInitiated);

            // Raise theme changed event
            OnThemeChanged(new ThemeChangedEventArgs(previousTheme, theme, userInitiated));

            Log.Information("✅ Theme changed successfully: {FromTheme} → {ToTheme}", previousTheme, theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to change theme: {FromTheme} → {ToTheme}", previousTheme, theme);
        if (!string.Equals(theme, "FluentLight", StringComparison.OrdinalIgnoreCase) && CanAttemptFallback())
            {
                try
                {
                    Log.Warning("Attempting fallback to FluentLight after failed theme change");
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
                    _currentTheme = "FluentLight";
                    StructuredLogger.LogThemeChange(previousTheme, "FluentLight", userInitiated);
                    OnThemeChanged(new ThemeChangedEventArgs(previousTheme, "FluentLight", userInitiated));
                    Log.Information("✅ Fallback FluentLight theme applied");
            RecordFallback(theme, ex);
                    return;
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "Fallback FluentLight theme failed during theme change");
            RecordFallback(theme, ex, fallbackEx, success:false);
                }
            }
            throw;
        }
    }

    // Perform a lightweight diagnostic to ensure required Syncfusion theme assemblies are loadable.
    // Logs warnings if expected assemblies are not present. Safe to call multiple times.
    public static void RunDiagnostics()
    {
        try
        {
            var required = new[]
            {
                "Syncfusion.SfSkinManager.WPF",
                "Syncfusion.Themes.FluentDark.WPF",
                "Syncfusion.Themes.FluentLight.WPF"
            };

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var r in required)
            {
                if (!loaded.Contains(r))
                {
                    Log.Warning("⚠️ ThemeDiagnostics: Expected assembly not loaded: {Assembly}", r);
                }
            }

            Log.Information("🩺 ThemeDiagnostics complete. Loaded Syncfusion assemblies: {Count}", loaded.Count(n => n.StartsWith("Syncfusion", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ThemeDiagnostics encountered an error");
        }
    }

    // Get list of supported theme names
    public static string[] GetSupportedThemes() => SupportedThemes;

    // Check if a theme is supported
    public static bool IsThemeSupported(string themeName) =>
        SupportedThemes.Any(t => string.Equals(t, themeName, StringComparison.OrdinalIgnoreCase));

    // Normalize a theme name to a supported canonical theme
    public static string NormalizeTheme(string raw)
    {
        var original = raw;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (!string.Equals(original, "FluentDark", StringComparison.OrdinalIgnoreCase))
                Log.Debug("Theme normalization: null/empty -> FluentDark");
            return "FluentDark";
        }

        raw = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
        var matched = SupportedThemes.FirstOrDefault(t =>
            string.Equals(t, raw, StringComparison.OrdinalIgnoreCase)) ?? "FluentDark";
        if (!string.Equals(original, matched, StringComparison.OrdinalIgnoreCase))
            Log.Debug("Theme normalization adjusted '{Original}' -> '{Matched}'", original, matched);
        return matched;
    }

    // Event arguments for theme change notifications
    public class ThemeChangedEventArgs : EventArgs
    {
        public string FromTheme { get; }
        public string ToTheme { get; }
        public bool UserInitiated { get; }
        public DateTime Timestamp { get; }

        public ThemeChangedEventArgs(string fromTheme, string toTheme, bool userInitiated)
        {
            FromTheme = fromTheme;
            ToTheme = toTheme;
            UserInitiated = userInitiated;
            Timestamp = DateTime.UtcNow;
        }
    }

    private static void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThemeChanged?.Invoke(null, e);
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        try
        {
            Log.Debug("ThemeService auto-initializing via EnsureInitialized()");
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            _isInitialized = true;
            RunDiagnostics();
            LogThemeAssemblyDiagnostics();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed implicit ThemeService initialization");
            throw; // preserve original failure semantics
        }
    }

    public static void LogThemeAssemblyDiagnostics()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Syncfusion.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.GetName().Name)
                .Select(a => $"{a.GetName().Name} v{a.GetName().Version}")
                .ToList();
            if (assemblies.Count == 0)
            {
                Log.Warning("⚠️ No Syncfusion assemblies loaded yet (controls not instantiated)");
            }
            else
            {
                Log.Information("🧩 Loaded Syncfusion assemblies ({Count}): {Assemblies}", assemblies.Count, string.Join(", ", assemblies));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to log Syncfusion assembly diagnostics");
        }
    }

    private static void RecordFallback(string originalTheme, Exception primary, Exception secondary = null, bool success = true)
    {
        _fallbackAttemptCount++;
        if (!_fallbackTelemetrySent)
        {
            Log.Information("TELEMETRY|ThemeFallback Applied={Success} Attempts={Attempts} From={Original} To=FluentLight", success, _fallbackAttemptCount, originalTheme);
            _fallbackTelemetrySent = true; // only once per run
        }

        if (success && !_fallbackNotificationShown)
        {
            _fallbackNotificationShown = true;
            TryNotifyUser(originalTheme, secondary == null ? primary : secondary);
        }

        if (_fallbackAttemptCount > MaxFallbackAttempts)
        {
            Log.Warning("Theme fallback attempts exceeded limit ({Max}); further fallbacks suppressed", MaxFallbackAttempts);
        }
    }

    private static bool CanAttemptFallback() => _fallbackAttemptCount < MaxFallbackAttempts;

    private static void TryNotifyUser(string originalTheme, Exception ex)
    {
        try
        {
            // Non-intrusive: show once. If dispatcher available, make async to avoid blocking.
            void show()
            {
                try
                {
                    System.Windows.MessageBox.Show($"The theme '{originalTheme}' failed and the application switched to FluentLight. Details logged.", "Theme Fallback", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { /* swallow UI errors */ }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke((Action)show);
            }
            else
            {
                show();
            }
        }
        catch (Exception notifyEx)
        {
            Log.Debug(notifyEx, "User notification for theme fallback failed (non-critical)");
        }
    }
}
