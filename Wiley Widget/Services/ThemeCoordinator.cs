using System;
using System.Collections.ObjectModel;
using System.Linq;
using WileyWidget.UI.Theming;
using WileyWidget.Configuration;

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
        private readonly ObservableCollection<string> _themes;
        public event EventHandler<string> ThemeChanged;

        public ThemeCoordinator(SettingsService settings)
        {
            _settings = settings;
            _themes = new ObservableCollection<string>(ThemeService.GetSupportedThemes());
            ThemeService.ThemeChanged += (_, e) => ThemeChanged?.Invoke(this, e.ToTheme);
        }

        public ReadOnlyObservableCollection<string> Themes => new(_themes);

        public string Current
        {
            get => ThemeService.CurrentTheme;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var normalized = ThemeService.NormalizeTheme(value);
                if (normalized == ThemeService.CurrentTheme) return;
                ThemeService.ChangeTheme(normalized, true);
                _settings.Current.Theme = normalized;
                _settings.Save();
                ThemeChanged?.Invoke(this, normalized);
            }
        }
    }
}
