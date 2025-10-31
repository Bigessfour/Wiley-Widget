using Prism.Commands;
using Prism.Dialogs;

namespace WileyWidget.ViewModels.Dialogs {
    /// <summary>
    /// ViewModel for confirmation dialogs.
    /// </summary>
    public class ConfirmationDialogViewModel : DialogViewModelBase
    {
        private string _message = string.Empty;
        private string _confirmButtonText = "Yes";
        private string _cancelButtonText = "No";

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string ConfirmButtonText
        {
            get => _confirmButtonText;
            set => SetProperty(ref _confirmButtonText, value);
        }

        public string CancelButtonText
        {
            get => _cancelButtonText;
            set => SetProperty(ref _cancelButtonText, value);
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
            if (parameters.TryGetValue("ConfirmButtonText", out string confirmText))
                ConfirmButtonText = confirmText;
            if (parameters.TryGetValue("CancelButtonText", out string cancelText))
                CancelButtonText = cancelText;
        }

        public DelegateCommand ConfirmCommand => new DelegateCommand(() =>
            CloseDialog(ButtonResult.Yes));

        public DelegateCommand CancelCommand => new DelegateCommand(() =>
            CloseDialog(ButtonResult.No));
    }
}
