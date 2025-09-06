using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WileyWidget.UI.Theming;
using WileyWidget.Configuration;
using Syncfusion.SfSkinManager;

namespace WileyWidget.Services
{
    /// <summary>
    /// Coordinator that exposes bindable theme list &amp; selection instead of imperative button updates.
    /// </summary>
    public interface IThemeCoordinator
    {
        ReadOnlyObservableCollection<string> Themes { get; }
        string Current { get; set; }
        event EventHandler<string> ThemeChanged; // new theme name
    }

    public sealed class ThemeCoordinator : IThemeCoordinator
    {
        private readonly SettingsService _settings;
        private readonly IThemeService _themeService;
        private readonly ObservableCollection<string> _themes;
        public event EventHandler<string> ThemeChanged;

        public ThemeCoordinator(ISettingsService settings, IThemeService themeService = null)
        {
            _settings = (SettingsService)settings;
            _themeService = themeService ?? new ThemeService(); // Fallback to instance if not injected
            _themes = new ObservableCollection<string>(WileyWidget.UI.Theming.ThemeService.GetSupportedThemes());
        }

        public ReadOnlyObservableCollection<string> Themes => new(_themes);

    public string Current
    {
        get => WileyWidget.UI.Theming.ThemeService.CurrentTheme;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var normalized = WileyWidget.UI.Theming.ThemeService.NormalizeTheme(value);
            if (normalized == WileyWidget.UI.Theming.ThemeService.CurrentTheme) return;

            // Apply theme using the proper IThemeService instance
            try
            {
                // Update state synchronously for immediate UI response
                WileyWidget.UI.Theming.ThemeService.SetCurrentTheme(normalized);
                _settings.Current.Theme = normalized;
                _settings.Save();
                Serilog.Log.Information("Theme changed to {Theme}", normalized);
                ThemeChanged?.Invoke(this, normalized);

                // Apply theme asynchronously (fire-and-forget for UI responsiveness)
                _ = ApplyThemeAsync(normalized);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - theme change should be non-blocking
                Serilog.Log.Error(ex, "Failed to change theme to {Theme}", normalized);
            }
        }
    }

    private async Task ApplyThemeAsync(string themeName)
    {
        if (_themeService != null)
        {
            await _themeService.ApplyThemeAsync(themeName);
            // State updates are now handled synchronously in the setter
        }
        else
        {
            Serilog.Log.Warning("ThemeService not available for theme application");
        }
    }
    }
}
