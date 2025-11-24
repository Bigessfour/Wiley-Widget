using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using System;

namespace WileyWidget.WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ILogger<SettingsPage> _logger;

        public SettingsPage()
        {
            this.InitializeComponent();
            _logger = App.Services?.GetService(typeof(ILogger<SettingsPage>)) as ILogger<SettingsPage>
                ?? throw new InvalidOperationException("Logger not available");

            _logger.LogInformation("SettingsPage initialized");
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
