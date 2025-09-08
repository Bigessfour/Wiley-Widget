using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using WileyWidget.UI.Theming;
using WileyWidget.Services; // access IThemeManager / IThemeService
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
        private readonly IThemeService _themeService; // underlying simple theme service
        private readonly ObservableCollection<string> _themes;
        public event EventHandler<string> ThemeChanged;

        public ThemeCoordinator(ISettingsService settings, IThemeService themeService = null)
        {
            _settings = (SettingsService)settings;
            _themeService = themeService ?? new ThemeService();
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
                try
                {
                    WileyWidget.UI.Theming.ThemeService.SetCurrentTheme(normalized);
                    _settings.Current.Theme = normalized;
                    _settings.Save();
                    Serilog.Log.Information("Theme changed to {Theme}", normalized);
                    ThemeChanged?.Invoke(this, normalized);
                    _ = ApplyThemeAsync(normalized); // fire & forget
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to change theme to {Theme}", normalized);
                }
            }
        }

        private async Task ApplyThemeAsync(string themeName)
        {
            if (_themeService != null)
            {
                try
                {
                    await _themeService.ApplyThemeAsync(themeName);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Async theme apply failed for {Theme}", themeName);
                }
            }
        }
    }
}
