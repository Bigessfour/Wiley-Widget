using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinUI.ViewModels.Main;
using System;

namespace WileyWidget.WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ILogger<SettingsPage> _logger;

        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();

            // Resolve dependencies from DI container
            _logger = App.Services?.GetRequiredService<ILogger<SettingsPage>>()
                ?? throw new InvalidOperationException("Logger not available from DI container");

            ViewModel = App.Services?.GetRequiredService<SettingsViewModel>()
                ?? throw new InvalidOperationException("SettingsViewModel not available from DI container");

            // Set DataContext for data binding
            this.DataContext = ViewModel;

            _logger.LogInformation("SettingsPage initialized with DataContext");
            LoadSettings();
        }


        private void LoadSettings()
        {
            // TODO: Load settings from configuration/database
            _logger.LogDebug("Loading settings from storage");
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Saving settings");

                // TODO: Validate and save settings
                // Simulate async save
                await System.Threading.Tasks.Task.Delay(500);

                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = "Success";
                StatusInfoBar.Message = "Settings saved successfully";
                StatusInfoBar.IsOpen = true;

                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.Title = "Error";
                StatusInfoBar.Message = $"Failed to save settings: {ex.Message}";
                StatusInfoBar.IsOpen = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Settings cancelled, reloading");
            LoadSettings();
            StatusInfoBar.IsOpen = false;
        }
    }
}
