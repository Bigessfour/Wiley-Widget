using System;
using System.Threading.Tasks;
using System.Windows;
using Prism;
using Prism.Dialogs;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.ViewModels;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for handling common interaction requests between ViewModels and Views.
    /// Provides standardized ways to show confirmations, notifications, and custom dialogs.
    /// </summary>
    public class InteractionRequestService : IInteractionRequestService
    {
        private readonly IDialogService _dialogService;
        private readonly ILogger<InteractionRequestService> _logger;

        public InteractionRequestService(IDialogService dialogService, ILogger<InteractionRequestService> logger)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Shows a confirmation dialog and returns the user's response.
        /// </summary>
        public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText = "Yes", string cancelButtonText = "No")
        {
            var result = await ShowDialogAsync<ConfirmationDialogViewModel>(title, new DialogParameters
            {
                { "Message", message },
                { "ConfirmButtonText", confirmButtonText },
                { "CancelButtonText", cancelButtonText }
            });

            return result?.Result == ButtonResult.Yes;
        }

        /// <summary>
        /// Shows an information notification dialog.
        /// </summary>
        public async Task ShowInformationAsync(string title, string message, string buttonText = "OK")
        {
            await ShowDialogAsync<NotificationDialogViewModel>(title, new DialogParameters
            {
                { "Message", message },
                { "ButtonText", buttonText }
            });
        }

        /// <summary>
        /// Shows a warning notification dialog.
        /// </summary>
        public async Task ShowWarningAsync(string title, string message, string buttonText = "OK")
        {
            await ShowDialogAsync<WarningDialogViewModel>(title, new DialogParameters
            {
                { "Message", message },
                { "ButtonText", buttonText }
            });
        }

        /// <summary>
        /// Shows an error notification dialog.
        /// </summary>
        public async Task ShowErrorAsync(string title, string message, string buttonText = "OK")
        {
            await ShowDialogAsync<ErrorDialogViewModel>(title, new DialogParameters
            {
                { "Message", message },
                { "ButtonText", buttonText }
            });
        }

        /// <summary>
        /// Shows a custom dialog with the specified ViewModel type.
        /// </summary>
        public async Task<IDialogResult?> ShowDialogAsync<TViewModel>(string title, IDialogParameters parameters) where TViewModel : IDialogAware
        {
            var dialogName = typeof(TViewModel).Name.Replace("ViewModel", "View");
            var result = await ShowDialogAsync(dialogName, title, parameters);
            return result;
        }

        /// <summary>
        /// Shows a dialog by name with the specified parameters.
        /// </summary>
        public async Task<IDialogResult?> ShowDialogAsync(string dialogName, string title, IDialogParameters parameters)
        {
            var tcs = new TaskCompletionSource<IDialogResult?>();

            var dialogParams = new DialogParameters
            {
                { "Title", title }
            };
            foreach (var param in parameters)
            {
                dialogParams.Add(param.Key, param.Value);
            }

            _dialogService.ShowDialog(dialogName, dialogParams, result =>
            {
                tcs.SetResult(result);
            });

            var dialogResult = await tcs.Task;

            if (dialogResult?.Result == ButtonResult.OK || dialogResult?.Result == ButtonResult.Yes)
            {
                _logger.LogInformation("Dialog '{DialogName}' completed successfully", dialogName);
            }
            else if (dialogResult?.Result == ButtonResult.Cancel || dialogResult?.Result == ButtonResult.No)
            {
                _logger.LogInformation("Dialog '{DialogName}' was cancelled", dialogName);
            }
            else
            {
                _logger.LogWarning("Dialog '{DialogName}' closed with unexpected result: {Result}", dialogName, dialogResult?.Result);
            }

            return dialogResult;
        }
    }
}
