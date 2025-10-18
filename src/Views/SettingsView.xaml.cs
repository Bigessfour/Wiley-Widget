using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using Serilog;

#nullable enable

namespace WileyWidget
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // Apply current theme
            TryApplyTheme(SettingsService.Instance.Current.Theme);

            // DataContext will be auto-wired by Prism ViewModelLocator

            // Load settings when window opens
            Loaded += SettingsView_Loaded;
        }

        private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                await viewModel.LoadSettingsAsync();
            }
        }

        /// <summary>
        /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails.
        /// </summary>
        private void TryApplyTheme(string themeName)
        {
            // Theme application is handled at the Window level for UserControls
            // Services.ThemeUtility.TryApplyTheme(this, themeName);
        }
    }
}