using System;
using System.Windows;
using System.Windows.Controls;
using Prism.Mvvm;
using Syncfusion.SfSkinManager;

namespace WileyWidget.UI.Controls
{
    /// <summary>
    /// Interaction logic for ThemeToggleControl.xaml
    /// Manages theme toggling via SfSkinManager.ApplicationTheme directly.
    /// </summary>
    public partial class ThemeToggleControl : UserControl
    {
        public ThemeToggleControl()
        {
            DataContext = new ThemeToggleViewModel();
        }
    }

    /// <summary>
    /// ViewModel for theme toggle control.
    /// Uses SfSkinManager.ApplicationTheme directly for global theme management.
    /// </summary>
    public class ThemeToggleViewModel : BindableBase
    {
        private bool _isDarkMode;

        public ThemeToggleViewModel()
        {
            // Initialize from current global theme
            var currentTheme = SfSkinManager.ApplicationTheme?.ThemeName ?? "FluentLight";
            _isDarkMode = currentTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    // Apply theme globally via SfSkinManager.ApplicationTheme
                    var newTheme = value ? "FluentDark" : "FluentLight";
                    SfSkinManager.ApplicationTheme = new Theme(newTheme);

                    // Note: Theme persistence to settings handled by SettingsViewModel
                    // This control only manages the UI toggle state
                }
            }
        }
    }
}
