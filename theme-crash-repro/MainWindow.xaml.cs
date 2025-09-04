using System;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.FluentLight.WPF;

namespace ThemeCrashRepro
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Test: Defer theme application to Loaded event
            // DO NOT apply theme in constructor
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Applying FluentLight theme...";
                
                // Test: Disable animations first
                FluentLightThemeSettings.HoverEffectMode = HoverEffect.None;
                FluentLightThemeSettings.PressedEffectMode = PressedEffect.None;
                
                // Test: Apply theme after window is loaded
                SfSkinManager.SetTheme(this, new FluentLightTheme());
                
                StatusText.Text = "Theme applied successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Theme crash: {ex.Message}";
                MessageBox.Show($"Theme application failed: {ex}", "Theme Crash", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Switching theme...";
                
                // Test: Switch theme at runtime
                SfSkinManager.SetTheme(this, new FluentLightTheme());
                
                StatusText.Text = "Theme switched successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Theme switch crash: {ex.Message}";
                MessageBox.Show($"Theme switch failed: {ex}", "Theme Crash", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}