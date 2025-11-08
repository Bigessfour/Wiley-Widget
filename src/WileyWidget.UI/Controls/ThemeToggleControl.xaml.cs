using System;
using System.Windows;
using System.Windows.Controls;
using Prism.Mvvm;
using WileyWidget.Services;

namespace WileyWidget.UI.Controls
{
    /// <summary>
    /// Interaction logic for ThemeToggleControl.xaml
    /// </summary>
    public partial class ThemeToggleControl : UserControl
    {
        public ThemeToggleControl()
        {
            // Designer constructor - will use InitializeComponent when XAML is compiled
        }

        public ThemeToggleControl(IThemeService themeService) : this()
        {
            if (themeService != null)
            {
                DataContext = new ThemeToggleViewModel(themeService);
            }
        }
    }

    /// <summary>
    /// ViewModel for theme toggle control.
    /// </summary>
    public class ThemeToggleViewModel : BindableBase
    {
        private readonly IThemeService _themeService;
        private bool _isDarkMode;

        public ThemeToggleViewModel(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // Initialize from current theme
            _isDarkMode = _themeService.IsDarkTheme;

            // Subscribe to theme changes
            _themeService.ThemeChanged += OnThemeChanged;
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    // Apply theme based on toggle state
                    var newTheme = value ? "FluentDark" : "FluentLight";
                    _themeService.ApplyTheme(newTheme);
                }
            }
        }

        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            // Update toggle state when theme changes externally
            var isDark = e.NewTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase);
            if (_isDarkMode != isDark)
            {
                SetProperty(ref _isDarkMode, isDark, nameof(IsDarkMode));
            }
        }
    }
}
