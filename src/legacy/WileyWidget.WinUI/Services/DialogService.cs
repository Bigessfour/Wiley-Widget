using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WileyWidget.WinUI.Services
{
    /// <summary>
    /// Service for displaying error and confirmation dialogs using WinUI 3 ContentDialog.
    /// Replaces legacy modal patterns with async/await ContentDialog.
    /// </summary>
    public interface IDialogService
    {
        Task ShowErrorAsync(string title, string message, Exception? exception = null);
        Task ShowInfoAsync(string title, string message);
        Task<bool> ShowConfirmationAsync(string title, string message);
    }

    public class DialogService : IDialogService
    {
        private readonly ILogger<DialogService> _logger;
        private readonly Microsoft.UI.Xaml.XamlRoot? _xamlRoot;

        public DialogService(ILogger<DialogService> logger, Microsoft.UI.Xaml.XamlRoot? xamlRoot = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _xamlRoot = xamlRoot;
        }

        public async Task ShowErrorAsync(string title, string message, Exception? exception = null)
        {
            _logger.LogError(exception, "Displaying error dialog: {Title} - {Message}", title, message);

            var errorMessage = message;
            if (exception != null)
            {
                errorMessage += $"\n\nDetails: {exception.Message}";
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = errorMessage,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show error dialog");
            }
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            _logger.LogInformation("Displaying info dialog: {Title}", title);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show info dialog");
            }
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            _logger.LogInformation("Displaying confirmation dialog: {Title}", title);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            try
            {
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show confirmation dialog");
                return false;
            }
        }
    }
}
