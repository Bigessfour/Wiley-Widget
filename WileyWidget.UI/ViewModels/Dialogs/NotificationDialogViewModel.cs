using Prism.Commands;
using Prism.Dialogs;

namespace WileyWidget.ViewModels.Dialogs {
    /// <summary>
    /// ViewModel for notification dialogs.
    /// </summary>
    public class NotificationDialogViewModel : DialogViewModelBase
    {
        private string _message = string.Empty;
        private string _buttonText = "OK";

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string ButtonText
        {
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            base.OnDialogOpened(parameters);

            if (parameters.TryGetValue("Message", out string message))
                Message = message;
            if (parameters.TryGetValue("ButtonText", out string buttonText))
                ButtonText = buttonText;
        }

        public DelegateCommand CloseCommand => new DelegateCommand(() =>
            CloseDialog(ButtonResult.OK));
    }
}
