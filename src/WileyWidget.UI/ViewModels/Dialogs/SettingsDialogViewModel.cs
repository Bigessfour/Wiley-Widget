using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;

namespace WileyWidget.ViewModels.Dialogs {
    /// <summary>
    /// ViewModel for the Settings Dialog
    /// </summary>
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "Settings";

        /// <summary>
        /// Gets or sets the dialog title
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets the OK command
        /// </summary>
        public DelegateCommand OkCommand { get; private set; }

        /// <summary>
        /// Gets the Cancel command
        /// </summary>
        public DelegateCommand CancelCommand { get; private set; }

        /// <summary>
        /// Callback to close the dialog
        /// </summary>
        public DialogCloseListener RequestClose { get; set; }

        /// <summary>
        /// Gets whether the dialog can be closed
        /// </summary>
        public bool CanCloseDialog() => true;

        /// <summary>
        /// Called when the dialog is closed
        /// </summary>
        public void OnDialogClosed() { }

        /// <summary>
        /// Called when the dialog is opened
        /// </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.TryGetValue("Title", out string title))
            {
                Title = title;
            }
        }

        /// <summary>
        /// Initializes a new instance of the SettingsDialogViewModel
        /// </summary>
        public SettingsDialogViewModel()
        {
            OkCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            });

            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
            });
        }
    }
}
