using Prism.Dialogs;
using Prism.Mvvm;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Base class for dialog ViewModels that implement IDialogAware.
    /// </summary>
    public abstract class DialogViewModelBase : BindableBase, IDialogAware
    {
        public string Title { get; set; } = string.Empty;
        public DialogCloseListener RequestClose { get; set; }

        public virtual bool CanCloseDialog() => true;

        public virtual void OnDialogClosed() { }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue("Title", out string title))
            {
                Title = title;
            }
        }

        protected virtual void CloseDialog(ButtonResult result)
        {
            RequestClose.Invoke(new DialogResult(result));
        }
    }
}
